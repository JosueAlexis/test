using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.Text.Json;

namespace ProyectoRH2025.Pages.Sellos
{
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // PROPIEDADES PARA ESTADÍSTICAS
        // ==========================================
        public int TotalSellos { get; set; }
        public int SellosDisponibles { get; set; }
        public int SellosEnTramite { get; set; }
        public int SellosEnUso { get; set; }
        public int SellosUtilizados { get; set; }
        public int SellosDefectuosos { get; set; }
        public int SellosEnPlanta { get; set; }
        public int SellosExtraviados { get; set; }
        public int SellosAsignadosSupervisor { get; set; }

        public string EstadisticasJSON { get; set; }
        public List<SelloDetalleDTO> TodosLosSellos { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string FiltroEstado { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FiltroBusqueda { get; set; }

        // ==========================================
        // HANDLER: CARGAR PÁGINA
        // ==========================================
        public async Task<IActionResult> OnGetAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null)
            {
                return RedirectToPage("/Login");
            }

            await CargarEstadisticasAsync();
            await CargarSellosAsync();

            return Page();
        }

        // ==========================================
        // CARGAR ESTADÍSTICAS
        // ==========================================
        private async Task CargarEstadisticasAsync()
        {
            TotalSellos = await _context.TblSellos.CountAsync();
            SellosDisponibles = await _context.TblSellos.CountAsync(s => s.Status == 1);
            SellosAsignadosSupervisor = await _context.TblSellos.CountAsync(s => s.Status == 14);
            SellosEnTramite = await _context.TblAsigSellos.CountAsync(a => a.Status == 4);
            SellosEnUso = await _context.TblAsigSellos.CountAsync(a => a.Status == 3);
            SellosUtilizados = await _context.TblSellos.CountAsync(s => s.Status == 12);
            SellosDefectuosos = await _context.TblSellos.CountAsync(s => s.Status == 6);
            SellosEnPlanta = await _context.TblSellos.CountAsync(s => s.Status == 11);
            SellosExtraviados = await _context.TblSellos.CountAsync(s => s.Status == 8);

            var estadisticas = new
            {
                labels = new[] { "Disponibles", "Asignados a Sup.", "En Trámite", "En Uso", "Utilizados", "Defectuosos", "En Planta", "Extraviados" },
                data = new[] { SellosDisponibles, SellosAsignadosSupervisor, SellosEnTramite, SellosEnUso, SellosUtilizados, SellosDefectuosos, SellosEnPlanta, SellosExtraviados },
                colors = new[] { "#28a745", "#17a2b8", "#ffc107", "#007bff", "#6c757d", "#dc3545", "#fd7e14", "#343a40" }
            };

            EstadisticasJSON = JsonSerializer.Serialize(estadisticas);
        }

        // ==========================================
        // CARGAR SELLOS CON INFO - USANDO SP
        // ==========================================
        private async Task CargarSellosAsync()
        {
            var filtroEstado = !string.IsNullOrEmpty(FiltroEstado) && int.TryParse(FiltroEstado, out int estado)
                ? (int?)estado
                : null;

            var filtroBusqueda = string.IsNullOrEmpty(FiltroBusqueda) ? (object)DBNull.Value : FiltroBusqueda;

            TodosLosSellos = await _context.Database
                .SqlQuery<SelloDetalleDTO>($@"
                    EXEC sp_Dashboard_ObtenerSellosCompleto 
                        @FiltroEstado = {filtroEstado}, 
                        @FiltroBusqueda = {filtroBusqueda}")
                .ToListAsync();
        }

        // ==========================================
        // OBTENER TEXTO DE ESTADO
        // ==========================================
        private string ObtenerTextoEstado(int status)
        {
            return status switch
            {
                1 => "Disponible",
                3 => "En Uso",
                4 => "En Trámite",
                6 => "Defectuoso",
                8 => "Extraviado",
                11 => "En Planta",
                12 => "Utilizado",
                14 => "Asignado a Supervisor",
                _ => "Desconocido"
            };
        }

        // ==========================================
        // HANDLER: OBTENER DETALLE DE SELLO
        // ==========================================
        public async Task<IActionResult> OnGetDetalleSelloAsync(int idSello)
        {
            var sello = await _context.TblSellos
                .FirstOrDefaultAsync(s => s.Id == idSello);

            if (sello == null)
            {
                return new JsonResult(new { error = "Sello no encontrado" });
            }

            // ✅ Obtener supervisor por separado
            string supervisorNombre = null;
            if (sello.SupervisorId.HasValue)
            {
                var supervisor = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == sello.SupervisorId.Value);
                supervisorNombre = supervisor?.NombreCompleto;
            }

            // Historial del sello
            var historial = await _context.TblSellosHistorial
                .Where(h => h.SelloId == idSello)
                .OrderByDescending(h => h.FechaMovimiento)
                .Select(h => new
                {
                    h.TipoMovimiento,
                    h.FechaMovimiento,
                    h.UsuarioNombre,
                    h.SupervisorNombreAnterior,
                    h.SupervisorNombreNuevo,
                    h.Comentario
                })
                .Take(20)
                .ToListAsync();

            // Asignaciones del sello
            var asignaciones = await _context.TblAsigSellos
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Where(a => a.idSello == idSello)
                .OrderByDescending(a => a.Fentrega)
                .Select(a => new
                {
                    a.id,
                    a.Status,
                    Operador = a.Operador != null ? $"{a.Operador.Names} {a.Operador.Apellido}" : null,
                    Operador2 = a.Operador2 != null ? $"{a.Operador2.Names} {a.Operador2.Apellido}" : null,
                    Unidad = a.Unidad != null ? a.Unidad.NumUnidad.ToString() : null,
                    a.Ruta,
                    a.Fentrega,
                    a.FechaEntrega,
                    a.FechaDevolucion,
                    a.QR_Code,
                    a.StatusEvidencia
                })
                .Take(10)
                .ToListAsync();

            // Evidencias del sello
            var evidencias = await _context.TblImagenAsigSellos
                .Where(e => asignaciones.Select(a => a.id).Contains(e.idTabla))
                .OrderByDescending(e => e.FSubidaEvidencia)
                .Select(e => new
                {
                    e.id,
                    e.TipoArchivo,
                    e.FSubidaEvidencia,
                    e.TamanoComprimido,
                    e.TamanoOriginal,
                    ImagenThumbnail = e.ImagenThumbnail ?? e.Imagen,
                    Imagen = e.Imagen,
                    TieneImagen = !string.IsNullOrEmpty(e.Imagen)
                })
                .Take(10)
                .ToListAsync();

            return new JsonResult(new
            {
                sello = new
                {
                    sello.Id,
                    sello.Sello,
                    sello.Status,
                    StatusTexto = ObtenerTextoEstado(sello.Status),
                    Supervisor = supervisorNombre,
                    sello.FechaAsignacion,
                    sello.Alta
                },
                historial,
                asignaciones,
                evidencias
            });
        }

        // ==========================================
        // DTO PARA SELLOS - ACTUALIZADO PARA SP
        // ==========================================
        public class SelloDetalleDTO
        {
            public int Id { get; set; }
            public string NumeroSello { get; set; }
            public int Status { get; set; }
            public string StatusTexto { get; set; }
            public string? SupervisorNombre { get; set; }
            public DateTime? FechaUltimaAsignacion { get; set; }

            // Datos de la última asignación
            public int? AsignacionId { get; set; }
            public int? AsignacionStatus { get; set; }
            public string? OperadorNombre { get; set; }
            public string? Operador2Nombre { get; set; }
            public int? NumUnidad { get; set; }
            public string? Ruta { get; set; }
        }
    }
}
