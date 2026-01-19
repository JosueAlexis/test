using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;

namespace ProyectoRH2025.Pages.Sellos
{
    public class HistorialSellosModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public HistorialSellosModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<TblSellosHistorial> Historial { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FiltroSello { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroSupervisor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FiltroTipoMovimiento { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FiltroFechaDesde { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FiltroFechaHasta { get; set; }

        public SelectList ListaSupervisores { get; set; }
        public SelectList ListaTiposMovimiento { get; set; }

        public async Task OnGetAsync()
        {
            // Cargar filtros
            await CargarFiltros();

            // Consulta base
            var query = _context.TblSellosHistorial
                .Include(h => h.Sello)
                .AsQueryable();

            // Aplicar filtros
            if (!string.IsNullOrEmpty(FiltroSello))
            {
                query = query.Where(h => h.NumeroSello.Contains(FiltroSello));
            }

            if (FiltroSupervisor.HasValue)
            {
                query = query.Where(h => h.SupervisorIdNuevo == FiltroSupervisor.Value ||
                                        h.SupervisorIdAnterior == FiltroSupervisor.Value);
            }

            if (!string.IsNullOrEmpty(FiltroTipoMovimiento))
            {
                query = query.Where(h => h.TipoMovimiento == FiltroTipoMovimiento);
            }

            if (FiltroFechaDesde.HasValue)
            {
                query = query.Where(h => h.FechaMovimiento >= FiltroFechaDesde.Value);
            }

            if (FiltroFechaHasta.HasValue)
            {
                query = query.Where(h => h.FechaMovimiento <= FiltroFechaHasta.Value.AddDays(1));
            }

            Historial = await query
                .OrderByDescending(h => h.FechaMovimiento)
                .Take(200)
                .ToListAsync();
        }

        private async Task CargarFiltros()
        {
            var supervisores = await _context.TblUsuarios
                .Where(u => u.idRol == 2 && u.Status == 1)
                .ToListAsync();

            ListaSupervisores = new SelectList(supervisores, "idUsuario", "UsuarioNombre");

            ListaTiposMovimiento = new SelectList(new[]
            {
                new { Value = "Asignacion", Text = "Asignación" },
                new { Value = "Desasignacion", Text = "Desasignación" },
                new { Value = "Importacion", Text = "Importación" }
            }, "Value", "Text");
        }
    }
}