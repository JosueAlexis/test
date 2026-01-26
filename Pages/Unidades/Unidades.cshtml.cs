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

        // ✅ NUEVO: Filtro para mostrar inactivas
        [BindProperty(SupportsGet = true)]
        public bool MostrarInactivas { get; set; } = false;

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
                    .Include(u => u.Status)
                    .AsQueryable();

                // ✅ FILTRO POR STATUS: Por defecto solo activas (IdStatus = 1)
                if (!MostrarInactivas)
                {
                    query = query.Where(u => u.IdStatus == 1);
                }
                else
                {
                    // Si se solicita ver inactivas, mostrar solo las inactivas
                    query = query.Where(u => u.IdStatus == 2);
                }

                if (!string.IsNullOrWhiteSpace(Busqueda))
                {
                    if (int.TryParse(Busqueda, out int numBusqueda))
                    {
                        query = query.Where(u =>
                            u.NumUnidad == numBusqueda ||
                            u.Placas.Contains(Busqueda)
                        );
                    }
                    else
                    {
                        query = query.Where(u => u.Placas.Contains(Busqueda));
                    }
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
                    query = query.Where(u => u.IdSucursal == FiltroSucursal.Value);
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

        // ✅ SOFT DELETE: Desactivar en lugar de eliminar
        public async Task<IActionResult> OnPostDesactivarAsync(int idEliminar)
        {
            try
            {
                var unidad = await _context.TblUnidades
                    .FirstOrDefaultAsync(u => u.Id == idEliminar);

                if (unidad == null)
                {
                    TempData["Error"] = "Unidad no encontrada";
                    return RedirectToPage();
                }

                // Verificar si tiene sellos asignados activos
                bool enUso = await _context.TblAsigSellos
                    .AnyAsync(a => a.idUnidad == idEliminar);

                if (enUso)
                {
                    TempData["Error"] = $"No se puede desactivar la unidad {unidad.NumUnidad} porque tiene sellos asignados activos";
                    return RedirectToPage();
                }

                // ✅ SOFT DELETE: Cambiar status a Inactivo (2)
                unidad.IdStatus = 2;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ Unidad {unidad.NumUnidad} desactivada correctamente. Se conserva el historial para auditoría.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al desactivar: {ex.Message}";
                return RedirectToPage();
            }
        }

        // ✅ NUEVO: Reactivar unidad
        public async Task<IActionResult> OnPostReactivarAsync(int id)
        {
            try
            {
                var unidad = await _context.TblUnidades
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (unidad == null)
                {
                    TempData["Error"] = "Unidad no encontrada";
                    return RedirectToPage(new { MostrarInactivas = true });
                }

                // ✅ Reactivar: Cambiar status a Activo (1)
                unidad.IdStatus = 1;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ Unidad {unidad.NumUnidad} reactivada correctamente";
                return RedirectToPage(new { MostrarInactivas = false });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al reactivar: {ex.Message}";
                return RedirectToPage(new { MostrarInactivas = true });
            }
        }
    }
}