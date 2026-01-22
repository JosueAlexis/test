using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;

namespace ProyectoRH2025.Pages.Catalogos
{
    public class UnidadesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public UnidadesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<TblUnidades> Unidades { get; set; } = new();
        public List<TblCuentas> Cuentas { get; set; } = new();
        public List<TblPool> pools { get; set; } = new();
        public List<TblClientes> Clientes { get; set; } = new();
        public List<TblSucursal> Sucursales { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Busqueda { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroIdCuenta { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroPool { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroCliente { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroSucursal { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroAno { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                Cuentas = await _context.TblCuentas
                    .Where(c => c.EsActiva)
                    .OrderBy(c => c.OrdenVisualizacion)
                    .ThenBy(c => c.CodigoCuenta)
                    .ToListAsync();

                pools = await _context.TblPool
                    .OrderBy(p => p.Pool)
                    .ToListAsync();

                Clientes = await _context.TblClientes
                    .OrderBy(c => c.Cliente)
                    .ToListAsync();

                Sucursales = await _context.TblSucursal
                    .OrderBy(s => s.Sucursal)
                    .ToListAsync();

                var query = _context.TblUnidades
                    .Include(u => u.Cuenta)
                    .Include(u => u.PoolNavigation)
                    .Include(u => u.Cliente)
                    .Include(u => u.Sucursal)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(Busqueda))
                {
                    query = query.Where(u =>
                        u.NumUnidad.ToString().Contains(Busqueda) ||
                        (u.Placas != null && u.Placas.Contains(Busqueda))
                    );
                }

                if (FiltroIdCuenta.HasValue)
                {
                    query = query.Where(u => u.IdCuenta == FiltroIdCuenta.Value);
                }

                if (FiltroPool.HasValue)
                {
                    query = query.Where(u => u.Pool == FiltroPool.Value);
                }

                if (FiltroCliente.HasValue)
                {
                    query = query.Where(u => u.CodCliente == FiltroCliente.Value);
                }

                if (FiltroSucursal.HasValue)
                {
                    query = query.Where(u => u.idSucursal == FiltroSucursal.Value);
                }

                if (FiltroAno.HasValue)
                {
                    query = query.Where(u => u.AnoUnidad == FiltroAno.Value);
                }

                Unidades = await query
                    .OrderBy(u => u.NumUnidad)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar unidades: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync(int idEliminar)
        {
            try
            {
                var unidad = await _context.TblUnidades
                    .FirstOrDefaultAsync(u => u.id == idEliminar);

                if (unidad == null)
                {
                    TempData["Error"] = "Unidad no encontrada";
                    return RedirectToPage();
                }

                bool enUso = await _context.TblAsigSellos
                    .AnyAsync(a => a.idUnidad == idEliminar);

                if (enUso)
                {
                    TempData["Error"] = $"No se puede eliminar la unidad {unidad.NumUnidad} porque tiene sellos asignados";
                    return RedirectToPage();
                }

                _context.TblUnidades.Remove(unidad);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Unidad {unidad.NumUnidad} eliminada correctamente";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}