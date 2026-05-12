using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.Text.Json;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProyectoRH2025.Pages.Sellos
{
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int TotalSellos { get; set; }
        public int SellosDisponibles { get; set; }
        public int SellosEnTramite { get; set; }
        public int SellosEnUso { get; set; }
        public int SellosUtilizados { get; set; }
        public int SellosDefectuosos { get; set; }
        public int SellosEnPlanta { get; set; }
        public int SellosExtraviados { get; set; }
        public int SellosAsignadosCuenta { get; set; }

        public string EstadisticasJSON { get; set; }
        public string SupervisorCuentasJSON { get; set; } // ✅ Mapa JSON para JavaScript

        public List<SelloDetalleDTO> TodosLosSellos { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string FiltroEstado { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FiltroBusqueda { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroSupervisor { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroCuenta { get; set; }

        public List<SelectListItem> ListaSupervisores { get; set; } = new();
        public List<SelectListItem> ListaCuentas { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) return RedirectToPage("/Login");

            var misCuentasIds = await _context.TblUsuariosCuentas
                .Where(uc => uc.IdUsuario == idUsuario && uc.EsActivo)
                .Select(uc => uc.IdCuenta).ToListAsync();

            bool esSuperUsuario = misCuentasIds.Contains(7);

            // 1. Cargar Cuentas Permitidas
            var queryCuentas = _context.TblCuentas.Where(c => c.EsActiva);
            if (!esSuperUsuario) queryCuentas = queryCuentas.Where(c => misCuentasIds.Contains(c.Id));
            var cuentasPermitidas = await queryCuentas.ToListAsync();

            ListaCuentas = cuentasPermitidas
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.NombreCuenta })
                .ToList();

            // 2. Cargar Supervisores y Mapeo de sus Cuentas
            var todosUsuariosCuentas = await _context.TblUsuariosCuentas
                .Where(uc => uc.EsActivo)
                .ToListAsync();

            var supervisorIdsPermitidos = todosUsuariosCuentas
                .Where(uc => esSuperUsuario || misCuentasIds.Contains(uc.IdCuenta))
                .Select(uc => uc.IdUsuario)
                .Distinct()
                .ToList();

            var supervisores = await _context.TblUsuarios
                .Where(u => supervisorIdsPermitidos.Contains(u.idUsuario) && u.idRol == 2) // Asumiendo Rol 2 = Supervisor
                .ToListAsync();

            ListaSupervisores = supervisores
                .Select(u => new SelectListItem { Value = u.idUsuario.ToString(), Text = u.NombreCompleto ?? u.UsuarioNombre })
                .ToList();

            // Generar Mapa JSON: { SupervisorId : [IdCuenta1, IdCuenta2, ...] }
            var mapaSupCuentas = todosUsuariosCuentas
                .GroupBy(uc => uc.IdUsuario)
                .ToDictionary(g => g.Key, g => g.Select(uc => uc.IdCuenta).ToList());

            SupervisorCuentasJSON = JsonSerializer.Serialize(mapaSupCuentas);

            await CargarEstadisticasAsync(misCuentasIds, esSuperUsuario);
            await CargarSellosAsync(idUsuario.Value);

            return Page();
        }

        private async Task CargarEstadisticasAsync(List<int> misCuentasIds, bool esSuperUsuario)
        {
            IQueryable<TblSellos> querySellos = _context.TblSellos;
            IQueryable<TblAsigSellos> queryAsig = _context.TblAsigSellos.Include(a => a.Sello);

            if (!esSuperUsuario)
            {
                querySellos = querySellos.Where(s => (s.IdCuenta != null && misCuentasIds.Contains(s.IdCuenta.Value)) || s.Status == 1);
                queryAsig = queryAsig.Where(a => a.Sello.IdCuenta != null && misCuentasIds.Contains(a.Sello.IdCuenta.Value));
            }

            TotalSellos = await querySellos.CountAsync();
            SellosDisponibles = await querySellos.CountAsync(s => s.Status == 1);
            SellosAsignadosCuenta = await querySellos.CountAsync(s => s.Status == 14);
            SellosEnTramite = await queryAsig.CountAsync(a => a.Status == 4);
            SellosEnUso = await queryAsig.CountAsync(a => a.Status == 3);
            SellosUtilizados = await querySellos.CountAsync(s => s.Status == 12);
            SellosDefectuosos = await querySellos.CountAsync(s => s.Status == 6);
            SellosEnPlanta = await querySellos.CountAsync(s => s.Status == 11);
            SellosExtraviados = await querySellos.CountAsync(s => s.Status == 8);

            var estadisticas = new
            {
                labels = new[] { "Disponibles", "Asignados Local", "En Trámite", "En Uso", "Utilizados", "Defectuosos", "En Planta", "Extraviados" },
                data = new[] { SellosDisponibles, SellosAsignadosCuenta, SellosEnTramite, SellosEnUso, SellosUtilizados, SellosDefectuosos, SellosEnPlanta, SellosExtraviados },
                colors = new[] { "#28a745", "#17a2b8", "#ffc107", "#007bff", "#6c757d", "#dc3545", "#fd7e14", "#343a40" }
            };

            EstadisticasJSON = JsonSerializer.Serialize(estadisticas);
        }

        private async Task CargarSellosAsync(int idUsuario)
        {
            var filtroEstadoObj = !string.IsNullOrEmpty(FiltroEstado) && int.TryParse(FiltroEstado, out int estado) ? (int?)estado : null;
            var filtroBusquedaObj = string.IsNullOrEmpty(FiltroBusqueda) ? (object)DBNull.Value : FiltroBusqueda;
            var filtroSupObj = FiltroSupervisor.HasValue ? (object)FiltroSupervisor.Value : DBNull.Value;
            var filtroCuentaObj = FiltroCuenta.HasValue ? (object)FiltroCuenta.Value : DBNull.Value;

            TodosLosSellos = await _context.Database
                .SqlQuery<SelloDetalleDTO>($@"
                    EXEC sp_Dashboard_ObtenerSellosCompleto 
                        @IdUsuarioLogueado = {idUsuario},
                        @FiltroEstado = {filtroEstadoObj}, 
                        @FiltroBusqueda = {filtroBusquedaObj},
                        @FiltroSupervisor = {filtroSupObj},
                        @FiltroCuenta = {filtroCuentaObj}")
                .ToListAsync();
        }

        private string ObtenerTextoEstado(int status)
        {
            return status switch { 1 => "Disponible", 3 => "En Uso", 4 => "En Trámite", 6 => "Defectuoso", 8 => "Extraviado", 11 => "En Planta", 12 => "Utilizado", 14 => "Asignado a Cuenta", _ => "Desconocido" };
        }

        public async Task<IActionResult> OnGetDetalleSelloAsync(int idSello)
        {
            var sello = await _context.TblSellos.Include(s => s.Cuenta).FirstOrDefaultAsync(s => s.Id == idSello);
            if (sello == null) return new JsonResult(new { error = "Sello no encontrado" });

            var historial = await _context.TblSellosHistorial.Where(h => h.SelloId == idSello).OrderByDescending(h => h.FechaMovimiento)
                .Select(h => new { h.TipoMovimiento, h.FechaMovimiento, h.UsuarioNombre, h.SupervisorNombreAnterior, h.SupervisorNombreNuevo, h.Comentario }).Take(20).ToListAsync();

            var asignaciones = await _context.TblAsigSellos.Include(a => a.Operador).Include(a => a.Operador2).Include(a => a.Unidad).Where(a => a.idSello == idSello).OrderByDescending(a => a.Fentrega)
                .Select(a => new { a.id, a.Status, Operador = a.Operador != null ? $"{a.Operador.Names} {a.Operador.Apellido}" : null, Operador2 = a.Operador2 != null ? $"{a.Operador2.Names} {a.Operador2.Apellido}" : null, Unidad = a.Unidad != null ? a.Unidad.NumUnidad.ToString() : null, a.Ruta, a.Fentrega, a.FechaEntrega, a.FechaDevolucion, a.QR_Code, a.StatusEvidencia, a.UsuarioDevolucionId, a.FechaDevolucionRegistro, a.UsuarioEvidenciaId, a.FechaEvidenciaRegistro }).Take(10).ToListAsync();

            var usuarioIds = asignaciones.SelectMany(a => new[] { a.UsuarioDevolucionId, a.UsuarioEvidenciaId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var usuarios = usuarioIds.Count > 0 ? await _context.TblUsuarios.Where(u => usuarioIds.Contains(u.idUsuario)).ToDictionaryAsync(u => u.idUsuario, u => u.NombreCompleto) : new Dictionary<int, string>();

            var asignacionesConUsuarios = asignaciones.Select(a => new { a.id, a.Status, a.Operador, a.Operador2, a.Unidad, a.Ruta, a.Fentrega, a.FechaEntrega, a.FechaDevolucion, a.QR_Code, a.StatusEvidencia, a.UsuarioDevolucionId, UsuarioDevolucionNombre = a.UsuarioDevolucionId.HasValue && usuarios.ContainsKey(a.UsuarioDevolucionId.Value) ? usuarios[a.UsuarioDevolucionId.Value] : null, a.FechaDevolucionRegistro, a.UsuarioEvidenciaId, UsuarioEvidenciaNombre = a.UsuarioEvidenciaId.HasValue && usuarios.ContainsKey(a.UsuarioEvidenciaId.Value) ? usuarios[a.UsuarioEvidenciaId.Value] : null, a.FechaEvidenciaRegistro }).ToList();

            var evidencias = await _context.TblImagenAsigSellos.Where(e => asignacionesConUsuarios.Select(a => a.id).Contains(e.idTabla)).OrderByDescending(e => e.FSubidaEvidencia)
                .Select(e => new { e.id, e.TipoArchivo, e.FSubidaEvidencia, e.TamanoComprimido, e.TamanoOriginal, ImagenThumbnail = e.ImagenThumbnail ?? e.Imagen, Imagen = e.Imagen, TieneImagen = !string.IsNullOrEmpty(e.Imagen) }).Take(10).ToListAsync();

            return new JsonResult(new
            {
                sello = new { sello.Id, sello.Sello, sello.Status, StatusTexto = ObtenerTextoEstado(sello.Status), Cuenta = sello.Cuenta?.NombreCuenta ?? "Central", sello.FechaAsignacion, sello.Alta },
                historial,
                asignaciones = asignacionesConUsuarios,
                evidencias
            });
        }

        public async Task<IActionResult> OnGetGenerarReporteAsync(string FiltroEstado, string FiltroBusqueda, int? FiltroSupervisor, int? FiltroCuenta)
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) return RedirectToPage("/Login");

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            var filtroEstadoInt = !string.IsNullOrEmpty(FiltroEstado) && int.TryParse(FiltroEstado, out int estado) ? (int?)estado : null;
            var filtroBusquedaObj = string.IsNullOrEmpty(FiltroBusqueda) ? (object)DBNull.Value : FiltroBusqueda;
            var filtroSupObj = FiltroSupervisor.HasValue ? (object)FiltroSupervisor.Value : DBNull.Value;
            var filtroCuentaObj = FiltroCuenta.HasValue ? (object)FiltroCuenta.Value : DBNull.Value;

            var sellosFiltrados = await _context.Database.SqlQuery<SelloDetalleDTO>($@"EXEC sp_Dashboard_ObtenerSellosCompleto @IdUsuarioLogueado = {idUsuario.Value}, @FiltroEstado = {filtroEstadoInt}, @FiltroBusqueda = {filtroBusquedaObj}, @FiltroSupervisor = {filtroSupObj}, @FiltroCuenta = {filtroCuentaObj}").ToListAsync();

            var idsSellos = sellosFiltrados.Select(s => s.Id).ToList();
            var evidencias = await (from e in _context.TblImagenAsigSellos join a in _context.TblAsigSellos on e.idTabla equals a.id where idsSellos.Contains(a.idSello) select new EvidenciaReporteDTO { IdSello = a.idSello, ImagenBase64 = e.ImagenThumbnail ?? e.Imagen, TipoArchivo = e.TipoArchivo, FechaSubida = e.FSubidaEvidencia }).ToListAsync();

            var archivoPdf = GenerarDocumentoPdf(sellosFiltrados, evidencias, FiltroEstado);
            return File(archivoPdf, "application/pdf", $"Reporte_Sellos_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        }

        private byte[] GenerarDocumentoPdf(List<SelloDetalleDTO> sellos, List<EvidenciaReporteDTO> evidencias, string estadoFiltro)
        {
            using var ms = new MemoryStream();
            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre); page.PageColor(Colors.White); page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));
                    page.Header().Row(row => { row.RelativeItem().Column(col => { col.Item().Text("Reporte de Control de Sellos").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2); col.Item().Text($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium); var textoFiltro = string.IsNullOrEmpty(estadoFiltro) ? "Todos los estados" : sellos.FirstOrDefault()?.StatusTexto ?? "Desconocido"; col.Item().Text($"Filtro aplicado: {textoFiltro}").FontSize(10); }); });
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        if (!sellos.Any()) { col.Item().Text("No se encontraron sellos con los filtros actuales.").Italic(); return; }
                        foreach (var sello in sellos)
                        {
                            var fotosSello = evidencias.Where(e => e.IdSello == sello.Id).ToList();
                            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(20).Column(selloCol =>
                            {
                                selloCol.Item().Row(row => { row.RelativeItem().Text($"Sello: {sello.NumeroSello}").FontSize(14).SemiBold(); row.ConstantItem(100).AlignRight().Text(sello.StatusTexto).FontColor(ObtenerColorEstadoPdf(sello.Status)).SemiBold(); });
                                selloCol.Item().PaddingTop(5).Text($"Cuenta: {sello.CuentaNombre ?? "Sin asignar"} | Sup: {sello.SupervisorNombre ?? "-"} | Última act: {(sello.FechaUltimaAsignacion.HasValue ? sello.FechaUltimaAsignacion.Value.ToString("dd/MM/yyyy HH:mm") : "-")}");
                                if (!string.IsNullOrEmpty(sello.OperadorNombre)) { selloCol.Item().Text($"Operador: {sello.OperadorNombre} (Ruta: {sello.Ruta ?? "-"}, Unidad: {sello.NumUnidad?.ToString() ?? "-"})").FontColor(Colors.Grey.Darken1); }
                                var fotosProcesadas = fotosSello.Select(f => new { FotoOriginal = f, Bytes = ObtenerBytesImagen(f.ImagenBase64, f.TipoArchivo) }).Where(f => f.Bytes != null).Take(6).ToList();
                                if (fotosProcesadas.Any()) { selloCol.Item().PaddingTop(10).Inlined(inlined => { inlined.Spacing(10); inlined.AlignLeft(); foreach (var item in fotosProcesadas) { inlined.Item().Width(150).Column(c => { c.Item().Height(100).Image(item.Bytes).FitArea(); c.Item().AlignCenter().PaddingTop(2).Text(item.FotoOriginal.FechaSubida?.ToString("dd/MM/yyyy") ?? "").FontSize(8).FontColor(Colors.Grey.Darken2); }); } }); }
                                else { selloCol.Item().PaddingTop(5).Text("Sin evidencias fotográficas para mostrar.").Italic().FontSize(9).FontColor(Colors.Grey.Medium); }
                            });
                        }
                    });
                    page.Footer().AlignCenter().Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
                });
            }).GeneratePdf(ms);
            return ms.ToArray();
        }

        private byte[] ObtenerBytesImagen(string imagenStr, string tipoArchivo)
        {
            if (string.IsNullOrEmpty(imagenStr) || tipoArchivo == "ruta_antigua" || tipoArchivo == "pdf") return null;
            try { var base64Data = imagenStr.Contains(",") ? imagenStr.Split(',')[1] : imagenStr; return Convert.FromBase64String(base64Data); } catch { return null; }
        }

        private string ObtenerColorEstadoPdf(int status) => status switch { 1 => Colors.Green.Medium, 3 => Colors.Blue.Medium, 4 => Colors.Orange.Medium, 6 => Colors.Red.Medium, _ => Colors.Grey.Darken3 };

        public class SelloDetalleDTO
        {
            public int Id { get; set; }
            public string NumeroSello { get; set; }
            public int Status { get; set; }
            public string StatusTexto { get; set; }
            public string? SupervisorNombre { get; set; } // Histórico / Quien asignó
            public string? CuentaNombre { get; set; } // ✅ Actualizado
            public DateTime? FechaUltimaAsignacion { get; set; }
            public int? AsignacionId { get; set; }
            public int? AsignacionStatus { get; set; }
            public string? OperadorNombre { get; set; }
            public string? Operador2Nombre { get; set; }
            public int? NumUnidad { get; set; }
            public string? Ruta { get; set; }
            public string? QRCode { get; set; }
        }

        public class EvidenciaReporteDTO { public int IdSello { get; set; } public string ImagenBase64 { get; set; } public string TipoArchivo { get; set; } public DateTime? FechaSubida { get; set; } }
    }
}