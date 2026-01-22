using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.ComponentModel.DataAnnotations;

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
        public TblUnidades Unidad { get; set; } = new();

        [BindProperty]
        [Required(ErrorMessage = "El número de unidad es obligatorio")]
        [Range(1, 999999, ErrorMessage = "Número de unidad inválido")]
        [Display(Name = "Número de Unidad")]
        public int NumUnidad { get; set; }

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
                bool existe = await _context.TblUnidades
                    .AnyAsync(u => u.NumUnidad == NumUnidad);

                if (existe)
                {
                    MensajeError = $"Ya existe una unidad con el número {NumUnidad}";
                    await CargarCatalogos();
                    return Page();
                }

                var cuentaValida = await _context.TblCuentas
                    .AnyAsync(c => c.Id == Unidad.IdCuenta && c.EsActiva);

                if (!cuentaValida)
                {
                    MensajeError = "La cuenta seleccionada no es válida";
                    await CargarCatalogos();
                    return Page();
                }

                Unidad.NumUnidad = NumUnidad;
                Unidad.Placas = Unidad.Placas?.Trim().ToUpper();
                Unidad.AnoUnidad = Math.Clamp(Unidad.AnoUnidad, 1990, DateTime.Now.Year + 1);

                _context.TblUnidades.Add(Unidad);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Unidad {Unidad.NumUnidad} creada correctamente";
                return RedirectToPage("/Unidades/Unidades");
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al guardar: {ex.Message}";
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