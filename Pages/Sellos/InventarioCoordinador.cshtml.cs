using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ProyectoRH2025.Pages.Sellos
{
    public class InventarioCoordinadorModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly QRCodeService _qrService;
        private readonly IWebHostEnvironment _environment;
        private readonly IAntiforgery _antiforgery;
        private readonly ImagenService _imagenService;

        public InventarioCoordinadorModel(
            ApplicationDbContext context,
            QRCodeService qrService,
            IWebHostEnvironment environment,
            IAntiforgery antiforgery,
            ImagenService imagenService)
        {
            _context = context;
            _qrService = qrService;
            _environment = environment;
            _antiforgery = antiforgery;
            _imagenService = imagenService;
        }

        public List<TblAsigSellos> SellosEnTramite { get; set; } = new();
        public List<TblAsigSellos> SellosEnUso { get; set; } = new();
        public List<TblAsigSellos> SellosPrioritarios { get; set; } = new();

        [TempData]
        public string Mensaje { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        [BindProperty]
        public int IdAsignacion { get; set; }

        [BindProperty]
        public int TipoAsignacion { get; set; }

        [BindProperty]
        public int IdOperador { get; set; }

        [BindProperty]
        public int? IdOperador2 { get; set; }

        [BindProperty]
        public string CodigoQR { get; set; }

        [BindProperty]
        public string TipoEvidencia { get; set; }

        [BindProperty]
        public List<IFormFile> ArchivosEvidencia { get; set; }

        [BindProperty]
        public string Comentarios { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) return RedirectToPage("/Login");

            var misCuentasIds = await _context.TblUsuariosCuentas
                .Where(uc => uc.IdUsuario == idUsuario && uc.EsActivo)
                .Select(uc => uc.IdCuenta)
                .ToListAsync();

            bool esSuperUsuario = misCuentasIds.Contains(7);

            IQueryable<TblAsigSellos> queryTramite = _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Include(a => a.Usuario)
                .Where(a => a.Status == 4);

            IQueryable<TblAsigSellos> queryUso = _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Where(a => a.Status == 3);

            if (!esSuperUsuario)
            {
                var usuariosVisibles = await _context.TblUsuariosCuentas
                    .Where(uc => misCuentasIds.Contains(uc.IdCuenta) && uc.EsActivo)
                    .Select(uc => uc.IdUsuario)
                    .Distinct()
                    .ToListAsync();

                queryTramite = queryTramite.Where(a => a.idSeAsigno != null && usuariosVisibles.Contains(a.idSeAsigno.Value));
                queryUso = queryUso.Where(a => a.idSeAsigno != null && usuariosVisibles.Contains(a.idSeAsigno.Value));
            }

            SellosEnTramite = await queryTramite.OrderBy(a => a.Fentrega).ToListAsync();
            SellosEnUso = await queryUso.OrderByDescending(a => a.FechaEntrega).ToListAsync();

            // ✅ CALCULAR SELLOS PRIORITARIOS (3+ días en el mismo estado)
            var fechaLimite = DateTime.Now.AddDays(-3);

            var prioritariosTramite = SellosEnTramite.Where(s => s.Fentrega <= fechaLimite).ToList();
            var prioritariosUso = SellosEnUso.Where(s => s.FechaEntrega.HasValue && s.FechaEntrega.Value <= fechaLimite).ToList();

            SellosPrioritarios = prioritariosTramite
                .Concat(prioritariosUso)
                .OrderBy(s => s.Fentrega) // Los más antiguos primero
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnGetConfirmarEntregaAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.id == idAsignacion);

            if (asignacion == null) return Content("<div class='alert alert-danger'>Asignación no encontrada</div>");

            if (!await UsuarioTienePermiso(asignacion.idSeAsigno))
                return Content("<div class='alert alert-danger'>⛔ No tienes permisos para ver esta asignación.</div>");

            var token = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken;

            var html = $@"
                <div class='card'>
                    <div class='card-body'>
                        <h5 class='card-title mb-4'>Información de la Asignación</h5>
                        
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Sello:</strong></div>
                            <div class='col-6'><span class='badge bg-primary fs-6'>{asignacion.Sello?.Sello}</span></div>
                        </div>
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Operador Principal:</strong></div>
                            <div class='col-6'>{asignacion.Operador?.Names} {asignacion.Operador?.Apellido}</div>
                        </div>
                        {(asignacion.TipoAsignacion == 1 && asignacion.Operador2 != null ? $@"
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Segundo Operador:</strong></div>
                            <div class='col-6'>{asignacion.Operador2?.Names} {asignacion.Operador2?.Apellido}</div>
                        </div>" : "")}
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Unidad:</strong></div>
                            <div class='col-6'>{asignacion.Unidad?.NumUnidad}</div>
                        </div>
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Ruta:</strong></div>
                            <div class='col-6'>{asignacion.Ruta ?? "N/A"}</div>
                        </div>
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Tipo:</strong></div>
                            <div class='col-6'><span class='badge bg-info'>{(asignacion.TipoAsignacion == 1 ? "Comboy" : "Individual")}</span></div>
                        </div>
                        <div class='row mb-3'>
                            <div class='col-6'><strong>Supervisor:</strong></div>
                            <div class='col-6'>{asignacion.Usuario?.UsuarioNombre ?? "N/A"}</div>
                        </div>

                        <hr/>

                        <form method='post' action='/Sellos/InventarioCoordinador?handler=GenerarQR'>
                            <input type='hidden' name='__RequestVerificationToken' value='{token}' />
                            <input type='hidden' name='IdAsignacion' value='{idAsignacion}' />
                            
                            <div class='alert alert-warning'>
                                <i class='fas fa-exclamation-triangle me-2'></i>
                                <strong>¿Confirmas la entrega?</strong>
                                <p class='mb-0 mt-2'>Se generará un código QR que debe ser entregado al operador.</p>
                            </div>

                            <div class='d-grid gap-2'>
                                <button type='submit' class='btn btn-success btn-lg'>
                                    <i class='fas fa-qrcode me-2'></i> Generar QR y Entregar
                                </button>
                            </div>
                        </form>
                    </div>
                </div>";

            return Content(html, "text/html");
        }

        public async Task<IActionResult> OnPostGenerarQRAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) { MensajeError = "Sesión expirada"; return RedirectToPage(); }

            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .FirstOrDefaultAsync(a => a.id == IdAsignacion);

            if (asignacion == null || asignacion.Status != 4)
            {
                MensajeError = "Asignación no válida";
                return RedirectToPage();
            }

            if (!await UsuarioTienePermiso(asignacion.idSeAsigno))
            {
                MensajeError = "⛔ No tienes permiso para gestionar sellos de este proyecto/cuenta.";
                return RedirectToPage();
            }

            var codigoQR = _qrService.GenerarCodigoUnico(asignacion.id, asignacion.Sello?.Sello ?? "", asignacion.idOperador);

            asignacion.QR_Code = codigoQR;
            asignacion.QR_FechaGeneracion = DateTime.Now;
            asignacion.QR_Entregado = true;
            asignacion.FechaEntrega = DateTime.Now;
            asignacion.Status = 3;
            asignacion.editor = idUsuario;

            if (asignacion.Sello != null) asignacion.Sello.Status = 3;

            await _context.SaveChangesAsync();

            Mensaje = $"QR generado y entregado exitosamente para el sello {asignacion.Sello?.Sello}";
            TempData["MostrarQR"] = codigoQR;
            TempData["IdAsignacionQR"] = asignacion.id;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetVerQRAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .FirstOrDefaultAsync(a => a.id == idAsignacion);

            if (asignacion == null || string.IsNullOrEmpty(asignacion.QR_Code))
                return Content("<div class='alert alert-danger'>QR no disponible</div>");

            if (!await UsuarioTienePermiso(asignacion.idSeAsigno))
                return Content("<div class='alert alert-danger'>⛔ Sin permisos.</div>");

            // ✅ VALIDAR QUE EL QR NO TENGA MÁS DE 1 HORA
            if (asignacion.QR_FechaGeneracion.HasValue)
            {
                var tiempoTranscurrido = DateTime.Now - asignacion.QR_FechaGeneracion.Value;

                if (tiempoTranscurrido.TotalHours > 1)
                {
                    var html = $@"
                <div class='alert alert-warning text-center'>
                    <i class='fas fa-exclamation-triangle fa-3x mb-3'></i>
                    <h5>QR Expirado</h5>
                    <p class='mb-2'>El código QR expiró hace {tiempoTranscurrido.Hours} hora(s) y {tiempoTranscurrido.Minutes} minuto(s).</p>
                    <p class='mb-0'><small>Los códigos QR solo son visibles durante 1 hora por seguridad.</small></p>
                    <hr/>
                    <p class='mb-0'><strong>Sello:</strong> {asignacion.Sello?.Sello}</p>
                    <p class='mb-0'><strong>Operador:</strong> {asignacion.Operador?.Names} {asignacion.Operador?.Apellido}</p>
                    <p class='mb-0'><small class='text-muted'>Generado: {asignacion.QR_FechaGeneracion.Value:dd/MM/yyyy HH:mm}</small></p>
                </div>";

                    return Content(html, "text/html");
                }
            }

            var qrBase64 = _qrService.GenerarQRBase64(asignacion.QR_Code);

            // Calcular tiempo restante
            var tiempoRestante = asignacion.QR_FechaGeneracion.HasValue
                ? TimeSpan.FromHours(1) - (DateTime.Now - asignacion.QR_FechaGeneracion.Value)
                : TimeSpan.Zero;

            var htmlQR = $@"
                <div class='text-center'>
                    <h5 class='mb-3'>Sello: {asignacion.Sello?.Sello}</h5>
                    <h6 class='text-muted mb-4'>Operador: {asignacion.Operador?.Names} {asignacion.Operador?.Apellido}</h6>
                    
                    <img src='data:image/png;base64,{qrBase64}' class='img-fluid mb-3' style='max-width: 300px; border: 2px solid #007bff; padding: 10px;' />
                    
                    <div class='alert alert-info mt-3'>
                        <small><strong>Código:</strong><br/>{asignacion.QR_Code}</small>
                    </div>

                    <div class='alert alert-warning mt-2'>
                        <i class='fas fa-clock me-2'></i>
                        <strong>Tiempo restante:</strong> {tiempoRestante.Minutes} min {tiempoRestante.Seconds} seg
                        <br/><small>Este QR expira 1 hora después de su generación</small>
                    </div>

                    <button class='btn btn-primary btn-sm' onclick='imprimirQR()'>
                        <i class='fas fa-print'></i> Imprimir
                    </button>
                </div>
                <script>function imprimirQR() {{ window.print(); }}</script>";

            return Content(htmlQR, "text/html");
        }

        public async Task<IActionResult> OnGetObtenerAsignacionAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos.FirstOrDefaultAsync(a => a.id == idAsignacion);
            if (asignacion == null) return new JsonResult(new { error = "No encontrada" });
            if (!await UsuarioTienePermiso(asignacion.idSeAsigno)) return new JsonResult(new { error = "Sin permisos" });

            return new JsonResult(new
            {
                tipoAsignacion = asignacion.TipoAsignacion,
                idOperador = asignacion.idOperador,
                idOperador2 = asignacion.idOperador2
            });
        }

        public async Task<IActionResult> OnGetListaOperadoresAsync()
        {
            var rawData = await _context.Empleados
                .Where(e => e.Puesto == 1 && e.Status == 1 && e.CodClientes == "1")
                .Select(e => new { e.Id, e.Reloj, e.Names, e.Apellido, e.Apellido2 })
                .ToListAsync();

            var operadores = rawData
                .Select(e => new { value = e.Id.ToString(), text = $"{e.Reloj} - {e.Names} {e.Apellido} {e.Apellido2}" })
                .OrderBy(e => e.text)
                .ToList();

            return new JsonResult(operadores);
        }

        public async Task<IActionResult> OnPostModificarOperadorAsync()
        {
            var asignacion = await _context.TblAsigSellos.FirstOrDefaultAsync(a => a.id == IdAsignacion && a.Status == 4);

            if (asignacion == null)
            {
                MensajeError = "Asignación no encontrada o ya fue entregada";
                return RedirectToPage();
            }

            if (!await UsuarioTienePermiso(asignacion.idSeAsigno))
            {
                MensajeError = "⛔ No tienes permiso para modificar este sello.";
                return RedirectToPage();
            }

            if (TipoAsignacion == 1 && IdOperador2.HasValue && IdOperador == IdOperador2.Value)
            {
                MensajeError = "No puedes seleccionar el mismo operador dos veces";
                return RedirectToPage();
            }

            asignacion.TipoAsignacion = TipoAsignacion;
            asignacion.idOperador = IdOperador;
            asignacion.idOperador2 = TipoAsignacion == 1 ? IdOperador2 : null;
            asignacion.editor = HttpContext.Session.GetInt32("idUsuario");
            await _context.SaveChangesAsync();

            Mensaje = "Operador(es) modificado(s) correctamente";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDevolverSelloAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) { MensajeError = "Sesión expirada"; return RedirectToPage(); }

            CodigoQR = CodigoQR?.Replace("'", "-").Trim();
            var (esValido, idAsignacionQR, numeroSello, idOperadorQR) = _qrService.ValidarQR(CodigoQR);

            if (!esValido)
            {
                MensajeError = "Código QR inválido o con formato incorrecto";
                return RedirectToPage();
            }

            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .FirstOrDefaultAsync(a => a.id == IdAsignacion && a.Status == 3);

            if (asignacion == null)
            {
                MensajeError = "Asignación no encontrada o no está en uso";
                return RedirectToPage();
            }

            if (!await UsuarioTienePermiso(asignacion.idSeAsigno))
            {
                MensajeError = "⛔ No tienes permiso para gestionar la devolución de este sello.";
                return RedirectToPage();
            }

            if (asignacion.QR_Code != CodigoQR) { MensajeError = "El código QR no coincide con esta asignación"; return RedirectToPage(); }
            if (asignacion.idOperador != idOperadorQR) { MensajeError = "El código QR no pertenece al operador de esta asignación"; return RedirectToPage(); }
            if (asignacion.Sello?.Sello != numeroSello) { MensajeError = "El código QR no coincide con el sello de esta asignación"; return RedirectToPage(); }
            asignacion.FechaDevolucion = DateTime.Now;
            asignacion.Status = 14;
            asignacion.editor = idUsuario; // Mantener para compatibilidad
            asignacion.UsuarioDevolucionId = idUsuario; // ✅ NUEVO: Coordinador que devolvió
            asignacion.FechaDevolucionRegistro = DateTime.Now; // ✅ NUEVO: Fecha de registro

            if (asignacion.Sello != null) asignacion.Sello.Status = 14;

            await _context.SaveChangesAsync();
            Mensaje = $"✅ Sello {numeroSello} devuelto correctamente al supervisor."; return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSubirEvidenciaAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) { MensajeError = "Sesión expirada"; return RedirectToPage(); }

            if (ArchivosEvidencia == null || ArchivosEvidencia.Count < 1) { MensajeError = "Debes seleccionar al menos 1 imagen"; return RedirectToPage(); }
            if (ArchivosEvidencia.Count > 3) { MensajeError = "No puedes subir más de 3 imágenes"; return RedirectToPage(); }

            foreach (var archivo in ArchivosEvidencia)
            {
                if (archivo.Length > 10 * 1024 * 1024) { MensajeError = $"El archivo {archivo.FileName} supera el límite de 10 MB"; return RedirectToPage(); }
            }

            var asignacion = await _context.TblAsigSellos.Include(a => a.Sello).FirstOrDefaultAsync(a => a.id == IdAsignacion && a.Status == 3);
            if (asignacion == null) { MensajeError = "Asignación no encontrada"; return RedirectToPage(); }
            if (!await UsuarioTienePermiso(asignacion.idSeAsigno)) { MensajeError = "⛔ No tienes permiso para subir evidencia a este sello."; return RedirectToPage(); }

            try
            {
                var resultado = await ProcesarMultiplesEvidencias(IdAsignacion, idUsuario.Value, ArchivosEvidencia);
                if (!resultado.Success) { MensajeError = resultado.Message; return RedirectToPage(); }

                asignacion.StatusEvidencia = TipoEvidencia;
                asignacion.Comentarios = Comentarios;

                if (asignacion.Sello != null)
                {
                    switch (TipoEvidencia)
                    {
                        case "Utilizado": asignacion.Sello.Status = 12; asignacion.Status = 12; asignacion.Sello.SupervisorId = null; asignacion.Sello.FechaAsignacion = null; break;
                        case "Defectuoso": asignacion.Sello.Status = 6; asignacion.Status = 6; asignacion.Sello.SupervisorId = null; asignacion.Sello.FechaAsignacion = null; break;
                        case "Planta": asignacion.Sello.Status = 11; asignacion.Status = 11; asignacion.Sello.SupervisorId = null; asignacion.Sello.FechaAsignacion = null; break;
                        case "Extraviado": asignacion.Sello.Status = 8; asignacion.Status = 8; asignacion.Sello.SupervisorId = null; asignacion.Sello.FechaAsignacion = null; break;
                        case "Otro": asignacion.Sello.Status = 15; asignacion.Status = 15; asignacion.Sello.SupervisorId = null; asignacion.Sello.FechaAsignacion = null; break;
                        default: asignacion.Sello.Status = 14; asignacion.Status = 14; break;
                    }
                }

                // ✅ NUEVO: Registrar quién subió la evidencia
                asignacion.UsuarioEvidenciaId = idUsuario.Value;
                asignacion.FechaEvidenciaRegistro = DateTime.Now;
                asignacion.editor = idUsuario.Value; // Mantener para compatibilidad

                await _context.SaveChangesAsync();

                Mensaje = TipoEvidencia switch
                {
                    "Utilizado" => $"✅ Sello marcado como UTILIZADO y archivado con {ArchivosEvidencia.Count} imagen(es).",
                    "Defectuoso" => $"⚠️ Sello marcado como DEFECTUOSO y archivado con {ArchivosEvidencia.Count} imagen(es).",
                    "Planta" => $"⚠️ Sello marcado como EN PLANTA y archivado con {ArchivosEvidencia.Count} imagen(es).",
                    "Extraviado" => $"❌ Sello marcado como EXTRAVIADO y archivado con {ArchivosEvidencia.Count} imagen(es).",
                    "Otro" => $"📝 Sello archivado (Otro motivo) con {ArchivosEvidencia.Count} imagen(es).",
                    _ => $"✅ Sello devuelto al supervisor con {ArchivosEvidencia.Count} imagen(es)."
                };
            }
            catch (Exception ex) { MensajeError = $"Error al subir evidencia: {ex.Message}"; }

            return RedirectToPage();
        }

        private async Task<(bool Success, string Message)> ProcesarMultiplesEvidencias(int idAsignacion, int idUsuario, List<IFormFile> archivos)
        {
            try
            {
                using (var connection = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    var dtEvidencias = new DataTable();
                    dtEvidencias.Columns.Add("Imagen", typeof(string));
                    dtEvidencias.Columns.Add("ImagenThumbnail", typeof(string));
                    dtEvidencias.Columns.Add("TamanoOriginal", typeof(int));
                    dtEvidencias.Columns.Add("TamanoComprimido", typeof(int));
                    dtEvidencias.Columns.Add("TipoArchivo", typeof(string));

                    foreach (var archivo in archivos)
                    {
                        var extension = Path.GetExtension(archivo.FileName).ToLower();
                        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                            return (false, $"Formato no válido: {archivo.FileName}. Solo se permiten JPG y PNG");
                        if (!_imagenService.EsImagenValida(archivo))
                            return (false, $"El archivo {archivo.FileName} no es una imagen válida");

                        var (imagenBase64, thumbnailBase64, tamanoOriginal, tamanoComprimido) = _imagenService.ProcesarImagen(archivo);
                        dtEvidencias.Rows.Add(imagenBase64, thumbnailBase64, tamanoOriginal, tamanoComprimido, "imagen");
                    }

                    using (var command = new SqlCommand("sp_InsertarEvidenciasInventario", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@IdAsignacion", idAsignacion);
                        command.Parameters.AddWithValue("@IdUsuario", idUsuario);

                        var tvpParam = command.Parameters.AddWithValue("@Evidencias", dtEvidencias);
                        tvpParam.SqlDbType = SqlDbType.Structured;
                        tvpParam.TypeName = "dbo.TipoEvidenciaInventario";

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                                return (reader.GetInt32(0) == 1, reader.GetString(1));
                        }
                    }
                }

                return (false, "Error al ejecutar el procedimiento");
            }
            catch (Exception ex) { return (false, $"Error: {ex.Message}"); }
        }
        public async Task<IActionResult> OnGetVerEvidenciasAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos.FindAsync(idAsignacion);
            if (asignacion != null && !await UsuarioTienePermiso(asignacion.idSeAsigno))
                return Content("<div class='alert alert-danger'>⛔ Sin permisos.</div>");

            var evidencias = await _context.TblImagenAsigSellos.Where(e => e.idTabla == idAsignacion).OrderByDescending(e => e.FSubidaEvidencia).ToListAsync();
            if (!evidencias.Any()) return Content("<div class='alert alert-info'>No hay evidencias registradas</div>");

            var html = "<div class='row'>";
            foreach (var evidencia in evidencias)
            {
                var esPDF = evidencia.TipoArchivo == "pdf";
                var esRutaAntigua = evidencia.TipoArchivo == "ruta_antigua";

                html += $"<div class='col-md-6 mb-3'><div class='card h-100'><div class='card-body'>";

                if (esRutaAntigua)
                {
                    var ext = Path.GetExtension(evidencia.Imagen).ToLower();
                    html += ext == ".pdf"
                        ? $"<a href='{evidencia.Imagen}' target='_blank' class='btn btn-outline-danger w-100'><i class='fas fa-file-pdf fa-3x mb-2'></i><br/>Ver PDF (Formato Antiguo)</a>"
                        : $"<img src='{evidencia.Imagen}' class='img-fluid rounded' alt='Evidencia' style='max-height: 250px; width: 100%; object-fit: cover;' />";
                }
                else if (esPDF)
                {
                    html += $"<div class='text-center'><i class='fas fa-file-pdf fa-5x text-danger mb-3'></i><br/><a href='data:application/pdf;base64,{evidencia.Imagen}' target='_blank' download='evidencia_{evidencia.id}.pdf' class='btn btn-danger'><i class='fas fa-download'></i> Descargar PDF</a><p class='text-muted small mt-2'>{evidencia.TamanoComprimido} KB</p></div>";
                }
                else
                {
                    var imagenMostrar = !string.IsNullOrEmpty(evidencia.ImagenThumbnail) ? evidencia.ImagenThumbnail : evidencia.Imagen;
                    html += $"<img src='data:image/jpeg;base64,{imagenMostrar}' class='img-fluid rounded shadow-sm' alt='Evidencia' style='max-height: 250px; width: 100%; object-fit: cover; cursor: pointer;' onclick='verImagenCompleta(\"{evidencia.Imagen}\", {evidencia.id})' title='Click para ver en tamaño completo' />";
                }

                html += $"<hr class='my-2'/><div class='d-flex justify-content-between align-items-center'><small class='text-muted'><i class='fas fa-clock'></i> {evidencia.FSubidaEvidencia:dd/MM/yyyy HH:mm}</small>";

                if (evidencia.TamanoOriginal.HasValue && evidencia.TamanoComprimido.HasValue && !esPDF)
                {
                    var reduccion = evidencia.TamanoOriginal.Value > 0 ? 100 - (evidencia.TamanoComprimido.Value * 100 / evidencia.TamanoOriginal.Value) : 0;
                    html += $"<small class='text-success'><i class='fas fa-compress-arrows-alt'></i> -{reduccion}%</small>";
                }

                html += "</div></div></div></div>";
            }

            html += @"</div>
        <div class='modal fade' id='modalImagenCompleta' tabindex='-1'>
            <div class='modal-dialog modal-xl modal-dialog-centered'>
                <div class='modal-content'>
                    <div class='modal-header'><h5 class='modal-title'><i class='fas fa-image me-2'></i>Evidencia Completa</h5><button type='button' class='btn-close' data-bs-dismiss='modal'></button></div>
                    <div class='modal-body text-center bg-dark'><img id='imagenCompleta' src='' class='img-fluid' style='max-height: 80vh;' /></div>
                    <div class='modal-footer'><button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Cerrar</button><a id='btnDescargarImagen' href='#' download='evidencia.jpg' class='btn btn-primary'><i class='fas fa-download'></i> Descargar</a></div>
                </div>
            </div>
        </div>
        <script>
        function verImagenCompleta(base64, id) {
            const dataUrl = 'data:image/jpeg;base64,' + base64;
            document.getElementById('imagenCompleta').src = dataUrl;
            document.getElementById('btnDescargarImagen').href = dataUrl;
            document.getElementById('btnDescargarImagen').download = 'evidencia_' + id + '.jpg';
            new bootstrap.Modal(document.getElementById('modalImagenCompleta')).show();
        }
        </script>";

            return Content(html, "text/html");
        }

        private async Task<bool> UsuarioTienePermiso(int? idCreadorAsignacion)
        {
            if (idCreadorAsignacion == null) return false;

            var idUsuarioActual = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuarioActual == null) return false;

            var misCuentasIds = await _context.TblUsuariosCuentas.Where(uc => uc.IdUsuario == idUsuarioActual && uc.EsActivo).Select(uc => uc.IdCuenta).ToListAsync();
            if (misCuentasIds.Contains(7)) return true;

            return await _context.TblUsuariosCuentas.AnyAsync(uc => uc.IdUsuario == idCreadorAsignacion.Value && misCuentasIds.Contains(uc.IdCuenta) && uc.EsActivo);
        }
    }
}