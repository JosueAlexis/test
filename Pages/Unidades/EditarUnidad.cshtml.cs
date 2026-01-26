using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.Data;

namespace ProyectoRH2025.Pages.Catalogos
{
    public class EditarUnidadModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditarUnidadModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public TblUnidades Unidad { get; set; } = new();

        public string? MensajeError { get; set; }
        public string? MensajeExito { get; set; }

        public List<TblCuentas> Cuentas { get; set; } = new();
        public List<TblPool> Pools { get; set; } = new();
        public List<TblClientes> Clientes { get; set; } = new();
        public List<TblSucursal> Sucursales { get; set; } = new();

        // Información de comodín (solo lectura)
        public bool EsUnidadComodin { get; set; }
        public DateTime? FechaExpiracion { get; set; }
        public int? DiasRestantes { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                var unidad = await _context.TblUnidades
                    .Include(u => u.Cuenta)
                    .Include(u => u.Status)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (unidad == null)
                {
                    TempData["Error"] = "Unidad no encontrada";
                    return RedirectToPage("/Unidades/Unidades");
                }

                Unidad = unidad;

                // Información de comodín
                EsUnidadComodin = unidad.EsComodin;
                FechaExpiracion = unidad.FechaExpiracionComodin;

                if (EsUnidadComodin && FechaExpiracion.HasValue)
                {
                    DiasRestantes = (FechaExpiracion.Value - DateTime.Now).Days;
                    if (DiasRestantes < 0) DiasRestantes = 0;
                }

                await CargarCatalogos();

                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar la unidad: {ex.Message}";
                return RedirectToPage("/Unidades/Unidades");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarCatalogos();
                return Page();
            }

            try
            {
                // Normalizar datos
                Unidad.Placas = Unidad.Placas?.Trim().ToUpper();

                // Preparar parámetros para el SP
                var parameters = new[]
                {
                    new SqlParameter("@IdUnidad", Unidad.Id),
                    new SqlParameter("@Placas", (object?)Unidad.Placas ?? DBNull.Value),
                    new SqlParameter("@Pool", Unidad.Pool),
                    new SqlParameter("@CodCliente", Unidad.CodCliente),
                    new SqlParameter("@AnoUnidad", Unidad.AnoUnidad),
                    new SqlParameter("@idSucursal", Unidad.IdSucursal),
                    new SqlParameter("@IdCuenta", Unidad.IdCuenta)
                };

                // Ejecutar el stored procedure
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_EditarUnidad @IdUnidad, @Placas, @Pool, @CodCliente, " +
                    "@AnoUnidad, @idSucursal, @IdCuenta",
                    parameters);

                TempData["Success"] = $"✅ Unidad {Unidad.NumUnidad} actualizada correctamente";
                return RedirectToPage("/Unidades/Unidades");
            }
            catch (SqlException ex)
            {
                MensajeError = ex.Message.Contains("Ya existe")
                    ? ex.Message
                    : $"Error al actualizar: {ex.Message}";

                await CargarCatalogos();
                return Page();
            }
            catch (Exception ex)
            {
                MensajeError = $"Error inesperado: {ex.Message}";
                await CargarCatalogos();
                return Page();
            }
        }

        private async Task CargarCatalogos()
        {
            // FILTRAR CUENTAS: Excluir "TODAS LAS CUENTAS" y solo traer activas
            Cuentas = await _context.TblCuentas
                .Where(c => c.EsActiva &&
                            c.CodigoCuenta != "TODAS" &&
                            !c.NombreCuenta.Contains("TODAS"))
                .OrderBy(c => c.OrdenVisualizacion)
                .ThenBy(c => c.CodigoCuenta)
                .ToListAsync();

            Pools = await _context.TblPool
                .OrderBy(p => p.Pool)
                .ToListAsync();

            Clientes = await _context.TblClientes
                .OrderBy(c => c.Cliente)
                .ToListAsync();

            Sucursales = await _context.TblSucursal
                .OrderBy(s => s.Sucursal)
                .ToListAsync();
        }
    }
}