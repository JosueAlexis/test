using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace ProyectoRH2025.Pages.Catalogos
{
    public class CrearUnidadModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CrearUnidadModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        [Required(ErrorMessage = "El número de unidad es obligatorio")]
        [Display(Name = "Número de Unidad")]
        public int NumUnidad { get; set; } // ✅ INT

        [BindProperty]
        [Required(ErrorMessage = "Las placas son obligatorias")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres")]
        public string Placas { get; set; } = string.Empty; // ✅ VARCHAR(50) NOT NULL

        [BindProperty]
        [Required(ErrorMessage = "El pool es obligatorio")]
        public int Pool { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "El cliente es obligatorio")]
        public int CodCliente { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "El año es obligatorio")]
        [Range(1990, 2100, ErrorMessage = "Año inválido")]
        public int AnoUnidad { get; set; }

        [BindProperty]
        public int? IdSucursal { get; set; } // ✅ NULLABLE

        [BindProperty]
        [Required(ErrorMessage = "La cuenta es obligatoria")]
        public int IdCuenta { get; set; }

        [BindProperty]
        public bool EsComodin { get; set; } = false;

        public string? MensajeError { get; set; }

        public List<TblCuentas> Cuentas { get; set; } = new();
        public List<TblPool> Pools { get; set; } = new();
        public List<TblClientes> Clientes { get; set; } = new();
        public List<TblSucursal> Sucursales { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await CargarCatalogos();
            return Page();
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
                // Normalizar placas
                Placas = Placas.Trim().ToUpper();

                // Preparar parámetros
                var parameters = new[]
                {
                    new SqlParameter("@NumUnidad", NumUnidad),
                    new SqlParameter("@Placas", Placas),
                    new SqlParameter("@Pool", Pool),
                    new SqlParameter("@CodCliente", CodCliente),
                    new SqlParameter("@AnoUnidad", AnoUnidad),
                    new SqlParameter("@idSucursal", (object?)IdSucursal ?? DBNull.Value),
                    new SqlParameter("@IdCuenta", IdCuenta),
                    new SqlParameter("@EsComodin", EsComodin),
                    new SqlParameter("@IdUnidadCreada", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_AltaUnidad @NumUnidad, @Placas, @Pool, @CodCliente, @AnoUnidad, " +
                    "@idSucursal, @IdCuenta, @EsComodin, @IdUnidadCreada OUTPUT",
                    parameters);

                var idUnidadCreada = (int)parameters[8].Value;

                if (EsComodin)
                {
                    var fechaExp = DateTime.Now.AddDays(5);
                    TempData["Success"] = $"✅ Unidad COMODÍN #{idUnidadCreada} ({NumUnidad}) creada exitosamente. " +
                                          $"⏰ Expira el {fechaExp:dd/MM/yyyy HH:mm}.";
                }
                else
                {
                    TempData["Success"] = $"✅ Unidad #{idUnidadCreada} ({NumUnidad}) creada exitosamente.";
                }

                return RedirectToPage("/Unidades/Unidades");
            }
            catch (SqlException ex)
            {
                MensajeError = ex.Message;
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