using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Services;
using Microsoft.AspNetCore.Antiforgery; // <--- CAMBIO 1: Agregar librería necesaria

namespace ProyectoRH2025.Pages.Sellos
{
    public class InventarioCoordinadorModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly QRCodeService _qrService;
        private readonly IWebHostEnvironment _environment;
        private readonly IAntiforgery _antiforgery;
        private readonly ImagenService _imagenService; // ✅ AGREGAR

        public InventarioCoordinadorModel(
            ApplicationDbContext context,
            QRCodeService qrService,
            IWebHostEnvironment environment,
            IAntiforgery antiforgery,
            ImagenService imagenService) // ✅ AGREGAR
        {
            _context = context;
            _qrService = qrService;
            _environment = environment;
            _antiforgery = antiforgery;
            _imagenService = imagenService; // ✅ AGREGAR
        }
        // PROPIEDADES
        public List<TblAsigSellos> SellosEnTramite { get; set; } = new();
        public List<TblAsigSellos> SellosEnUso { get; set; } = new();

        [TempData]
        public string Mensaje { get; set; }

        [TempData]
        public string MensajeError { get; set; }

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

            // Cargar sellos en trámite (Status 4)
            SellosEnTramite = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Include(a => a.Usuario)
                .Where(a => a.Status == 4)
                .OrderBy(a => a.Fentrega)
                .ToListAsync();

            // Cargar sellos en uso (Status 3)
            SellosEnUso = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Where(a => a.Status == 3)
                .OrderByDescending(a => a.FechaEntrega)
                .ToListAsync();

            return Page();
        }

        // ==========================================
        // HANDLER: CONFIRMAR ENTREGA (Cargar info)
        // ==========================================
        public async Task<IActionResult> OnGetConfirmarEntregaAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .Include(a => a.Operador2)
                .Include(a => a.Unidad)
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.id == idAsignacion);

            if (asignacion == null)
            {
                return Content("<div class='alert alert-danger'>Asignación no encontrada</div>");
            }

            // <--- CAMBIO 5: Obtener el Token AntiForgery manualmente
            var token = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken;

            var html = $@"
                <div class='card'>
                    <div class='card-body'>
                        <h5 class='card-title mb-4'>Información de la Asignación</h5>
                        
                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Sello:</strong>
                            </div>
                            <div class='col-6'>
                                <span class='badge bg-primary fs-6'>{asignacion.Sello?.Sello}</span>
                            </div>
                        </div>

                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Operador Principal:</strong>
                            </div>
                            <div class='col-6'>
                                {asignacion.Operador?.Names} {asignacion.Operador?.Apellido}
                            </div>
                        </div>

                        {(asignacion.TipoAsignacion == 1 && asignacion.Operador2 != null ? $@"
                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Segundo Operador:</strong>
                            </div>
                            <div class='col-6'>
                                {asignacion.Operador2?.Names} {asignacion.Operador2?.Apellido}
                            </div>
                        </div>" : "")}

                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Unidad:</strong>
                            </div>
                            <div class='col-6'>
                                {asignacion.Unidad?.NumUnidad}
                            </div>
                        </div>

                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Ruta:</strong>
                            </div>
                            <div class='col-6'>
                                {asignacion.Ruta ?? "N/A"}
                            </div>
                        </div>

                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Tipo:</strong>
                            </div>
                            <div class='col-6'>
                                <span class='badge bg-info'>{(asignacion.TipoAsignacion == 1 ? "Comboy" : "Individual")}</span>
                            </div>
                        </div>

                        <div class='row mb-3'>
                            <div class='col-6'>
                                <strong>Supervisor:</strong>
                            </div>
                            <div class='col-6'>
                                {asignacion.Usuario?.UsuarioNombre ?? "N/A"}
                            </div>
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
                </div>
            ";

            return Content(html, "text/html");
        }

        // ==========================================
        // HANDLER: GENERAR QR Y ENTREGAR
        // ==========================================
        [BindProperty]
        public int IdAsignacion { get; set; }

        public async Task<IActionResult> OnPostGenerarQRAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null)
            {
                MensajeError = "Sesión expirada";
                return RedirectToPage();
            }

            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .FirstOrDefaultAsync(a => a.id == IdAsignacion);

            if (asignacion == null || asignacion.Status != 4)
            {
                MensajeError = "Asignación no válida";
                return RedirectToPage();
            }

            // Generar código único
            var codigoQR = _qrService.GenerarCodigoUnico(
                asignacion.id,
                asignacion.Sello?.Sello ?? "",
                asignacion.idOperador
            );

            // Actualizar asignación
            asignacion.QR_Code = codigoQR;
            asignacion.QR_FechaGeneracion = DateTime.Now;
            asignacion.QR_Entregado = true;
            asignacion.FechaEntrega = DateTime.Now;
            asignacion.Status = 3; // En Uso
            asignacion.editor = idUsuario;
            // Actualizar sello
            if (asignacion.Sello != null)
            {
                asignacion.Sello.Status = 3; // En Uso
            }

            await _context.SaveChangesAsync();

            Mensaje = $"QR generado y entregado exitosamente para el sello {asignacion.Sello?.Sello}";
            TempData["MostrarQR"] = codigoQR;
            TempData["IdAsignacionQR"] = asignacion.id;

            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: VER QR
        // ==========================================
        public async Task<IActionResult> OnGetVerQRAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .FirstOrDefaultAsync(a => a.id == idAsignacion);

            if (asignacion == null || string.IsNullOrEmpty(asignacion.QR_Code))
            {
                return Content("<div class='alert alert-danger'>QR no disponible</div>");
            }

            // Generar imagen QR en base64
            var qrBase64 = _qrService.GenerarQRBase64(asignacion.QR_Code);

            var html = $@"
                <div class='text-center'>
                    <h5 class='mb-3'>Sello: {asignacion.Sello?.Sello}</h5>
                    <h6 class='text-muted mb-4'>Operador: {asignacion.Operador?.Names} {asignacion.Operador?.Apellido}</h6>
                    
                    <img src='data:image/png;base64,{qrBase64}' 
                         alt='Código QR' 
                         class='img-fluid mb-3'
                         style='max-width: 300px; border: 2px solid #007bff; padding: 10px;' />
                    
                    <div class='alert alert-info mt-3'>
                        <small><strong>Código:</strong><br/>{asignacion.QR_Code}</small>
                    </div>

                    <button class='btn btn-primary btn-sm' onclick='imprimirQR()'>
                        <i class='fas fa-print'></i> Imprimir
                    </button>
                </div>

                <script>
                function imprimirQR() {{
                    window.print();
                }}
                </script>
            ";

            return Content(html, "text/html");
        }

        // ==========================================
        // HANDLER: OBTENER ASIGNACIÓN (para modificar)
        // ==========================================
        public async Task<IActionResult> OnGetObtenerAsignacionAsync(int idAsignacion)
        {
            var asignacion = await _context.TblAsigSellos
                .FirstOrDefaultAsync(a => a.id == idAsignacion);

            if (asignacion == null)
            {
                return new JsonResult(new { error = "No encontrada" });
            }

            return new JsonResult(new
            {
                tipoAsignacion = asignacion.TipoAsignacion,
                idOperador = asignacion.idOperador,
                idOperador2 = asignacion.idOperador2
            });
        }

        // ==========================================
        // HANDLER: LISTA DE OPERADORES (para select)
        // ==========================================
        public async Task<IActionResult> OnGetListaOperadoresAsync()
        {
            var operadores = await _context.Empleados
                .Where(e => e.Puesto == 1 && e.Status == 1)
                .Select(e => new
                {
                    value = e.Id.ToString(),
                    text = $"{e.Reloj} - {e.Names} {e.Apellido} {e.Apellido2}"
                })
                .OrderBy(e => e.text)
                .ToListAsync();

            return new JsonResult(operadores);
        }

        // ==========================================
        // HANDLER: MODIFICAR OPERADOR
        // ==========================================
        [BindProperty]
        public int TipoAsignacion { get; set; }

        [BindProperty]
        public int IdOperador { get; set; }

        [BindProperty]
        public int? IdOperador2 { get; set; }

        public async Task<IActionResult> OnPostModificarOperadorAsync()
        {
            var asignacion = await _context.TblAsigSellos
                .FirstOrDefaultAsync(a => a.id == IdAsignacion && a.Status == 4);

            if (asignacion == null)
            {
                MensajeError = "Asignación no encontrada o ya fue entregada";
                return RedirectToPage();
            }

            // Validar comboy
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

        // ==========================================
        // HANDLER: DEVOLVER SELLO CON QR
        // ==========================================
        [BindProperty]
        public string CodigoQR { get; set; }

        public async Task<IActionResult> OnPostDevolverSelloAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null)
            {
                MensajeError = "Sesión expirada";
                return RedirectToPage();
            }

            // ←←← Normalizar aquí el código recibido
            CodigoQR = CodigoQR?.Replace("'", "-").Trim();

            // Validar formato del QR
            var (esValido, idAsignacionQR, numeroSello, idOperadorQR) = _qrService.ValidarQR(CodigoQR);
            if (!esValido)
            {
                MensajeError = "Código QR inválido o con formato incorrecto";
                return RedirectToPage();
            }

            // Buscar la asignación
            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .Include(a => a.Operador)
                .FirstOrDefaultAsync(a => a.id == IdAsignacion && a.Status == 3);

            if (asignacion == null)
            {
                MensajeError = "Asignación no encontrada o no está en uso";
                return RedirectToPage();
            }

            // Validar que el QR coincida con la asignación
            if (asignacion.QR_Code != CodigoQR)
            {
                MensajeError = "El código QR no coincide con esta asignación";
                return RedirectToPage();
            }

            // Validar que el operador del QR coincida
            if (asignacion.idOperador != idOperadorQR)
            {
                MensajeError = "El código QR no pertenece al operador de esta asignación";
                return RedirectToPage();
            }

            // Validar que el sello del QR coincida
            if (asignacion.Sello?.Sello != numeroSello)
            {
                MensajeError = "El código QR no coincide con el sello de esta asignación";
                return RedirectToPage();
            }

            // ✅ TODO VÁLIDO - Procesar devolución
            asignacion.FechaDevolucion = DateTime.Now;
            asignacion.Status = 1; // Devuelto (puedes usar otro status si prefieres)
            asignacion.editor = HttpContext.Session.GetInt32("idUsuario");
            // Liberar el sello para nueva asignación
            if (asignacion.Sello != null)
            {
                asignacion.Sello.Status = 1; // Activo/Disponible
                asignacion.Sello.SupervisorId = null;
                asignacion.Sello.FechaAsignacion = null;
            }

            await _context.SaveChangesAsync();

            Mensaje = $"Sello {numeroSello} devuelto correctamente. El sello está disponible nuevamente.";
            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: SUBIR EVIDENCIA
        // ==========================================
        [BindProperty]
        public string TipoEvidencia { get; set; }

        [BindProperty]
        public IFormFile ArchivoEvidencia { get; set; }

        [BindProperty]
        public string Comentarios { get; set; }

        public async Task<IActionResult> OnPostSubirEvidenciaAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null)
            {
                MensajeError = "Sesión expirada";
                return RedirectToPage();
            }

            if (ArchivoEvidencia == null || ArchivoEvidencia.Length == 0)
            {
                MensajeError = "Debes seleccionar un archivo";
                return RedirectToPage();
            }

            // Validar tamaño máximo (10 MB)
            if (ArchivoEvidencia.Length > 10 * 1024 * 1024)
            {
                MensajeError = "El archivo no puede superar 10 MB";
                return RedirectToPage();
            }

            var asignacion = await _context.TblAsigSellos
                .Include(a => a.Sello)
                .FirstOrDefaultAsync(a => a.id == IdAsignacion && a.Status == 3);

            if (asignacion == null)
            {
                MensajeError = "Asignación no encontrada";
                return RedirectToPage();
            }

            try
            {
                var extension = Path.GetExtension(ArchivoEvidencia.FileName).ToLower();
                string imagenBase64;
                string thumbnailBase64 = null;
                string imagenOriginalBase64 = null;
                int tamanoOriginal;
                int tamanoComprimido;
                string tipoArchivo;

                if (extension == ".pdf")
                {
                    // Procesar PDF
                    var (pdfBase64, tamanoKB) = _imagenService.ProcesarPDF(ArchivoEvidencia);
                    imagenBase64 = pdfBase64;
                    imagenOriginalBase64 = pdfBase64;
                    thumbnailBase64 = null;
                    tamanoOriginal = tamanoKB;
                    tamanoComprimido = tamanoKB;
                    tipoArchivo = "pdf";
                }
                else if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
                {
                    // Validar que sea una imagen válida
                    if (!_imagenService.EsImagenValida(ArchivoEvidencia))
                    {
                        MensajeError = "El archivo no es una imagen válida";
                        return RedirectToPage();
                    }

                    // Procesar imagen con compresión
                    var (imgBase64, thumbBase64, tamOriginal, tamComprimido) =
                        _imagenService.ProcesarImagen(ArchivoEvidencia);

                    imagenBase64 = imgBase64;          // Imagen comprimida
                    thumbnailBase64 = thumbBase64;      // Miniatura
                    imagenOriginalBase64 = imgBase64;   // Para esta implementación, guardamos la comprimida
                    tamanoOriginal = tamOriginal;
                    tamanoComprimido = tamComprimido;
                    tipoArchivo = "imagen";
                }
                else
                {
                    MensajeError = "Formato no válido. Solo se permiten: JPG, PNG, PDF";
                    return RedirectToPage();
                }

                // Guardar en base de datos
                var evidencia = new TblImagenAsigSellos
                {
                    idTabla = asignacion.id,
                    Imagen = imagenBase64,
                    ImagenThumbnail = thumbnailBase64,
                    TamanoOriginal = tamanoOriginal,
                    TamanoComprimido = tamanoComprimido,
                    TipoArchivo = tipoArchivo,
                    FSubidaEvidencia = DateTime.Now,
                    Editor = idUsuario
                };

                _context.TblImagenAsigSellos.Add(evidencia);

                // Actualizar status según tipo de evidencia
                asignacion.StatusEvidencia = TipoEvidencia;
                asignacion.Comentarios = Comentarios;

                if (asignacion.Sello != null)
                {
                    switch (TipoEvidencia)
                    {
                        case "Utilizado":
                            asignacion.Sello.Status = 12;
                            asignacion.Status = 12;
                            break;
                        case "Defectuoso":
                            asignacion.Sello.Status = 6;
                            asignacion.Status = 6;
                            break;
                        case "Planta":
                            asignacion.Sello.Status = 11;
                            asignacion.Status = 11;
                            break;
                        case "Extraviado":
                            asignacion.Sello.Status = 8;
                            asignacion.Status = 8;
                            break;
                    }
                }

                await _context.SaveChangesAsync();

                var porcentajeReduccion = tamanoOriginal > 0
                    ? 100 - (tamanoComprimido * 100 / tamanoOriginal)
                    : 0;

                Mensaje = $"✅ Evidencia subida correctamente. Tamaño: {tamanoOriginal} KB → {tamanoComprimido} KB (reducción: {porcentajeReduccion}%)";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al subir evidencia: {ex.Message}";
            }

            return RedirectToPage();
        }
        // ==========================================
        // HANDLER: VER EVIDENCIAS
        // ==========================================
        public async Task<IActionResult> OnGetVerEvidenciasAsync(int idAsignacion)
        {
            var evidencias = await _context.TblImagenAsigSellos
                .Where(e => e.idTabla == idAsignacion)
                .OrderByDescending(e => e.FSubidaEvidencia)
                .ToListAsync();

            if (!evidencias.Any())
            {
                return Content("<div class='alert alert-info'>No hay evidencias registradas</div>");
            }

            var html = "<div class='row'>";

            foreach (var evidencia in evidencias)
            {
                var esPDF = evidencia.TipoArchivo == "pdf";
                var esRutaAntigua = evidencia.TipoArchivo == "ruta_antigua";

                html += $@"
            <div class='col-md-6 mb-3'>
                <div class='card h-100'>
                    <div class='card-body'>";

                if (esRutaAntigua)
                {
                    // Compatibilidad con rutas antiguas
                    var ext = Path.GetExtension(evidencia.Imagen).ToLower();
                    var esPDFAntiguo = ext == ".pdf";

                    html += esPDFAntiguo ?
                        $@"<a href='{evidencia.Imagen}' target='_blank' class='btn btn-outline-danger w-100'>
                    <i class='fas fa-file-pdf fa-3x mb-2'></i><br/>
                    Ver PDF (Formato Antiguo)
                </a>" :
                        $@"<img src='{evidencia.Imagen}' class='img-fluid rounded' alt='Evidencia' 
                    style='max-height: 250px; width: 100%; object-fit: cover;' />";
                }
                else if (esPDF)
                {
                    // PDF en Base64
                    html += $@"
                <div class='text-center'>
                    <i class='fas fa-file-pdf fa-5x text-danger mb-3'></i>
                    <br/>
                    <a href='data:application/pdf;base64,{evidencia.Imagen}' 
                       target='_blank' 
                       download='evidencia_{evidencia.id}.pdf'
                       class='btn btn-danger'>
                        <i class='fas fa-download'></i> Descargar PDF
                    </a>
                    <p class='text-muted small mt-2'>{evidencia.TamanoComprimido} KB</p>
                </div>";
                }
                else
                {
                    // Imagen en Base64 - Usar thumbnail para preview, imagen completa para modal
                    var imagenMostrar = !string.IsNullOrEmpty(evidencia.ImagenThumbnail)
                        ? evidencia.ImagenThumbnail
                        : evidencia.Imagen;

                    html += $@"
                <img src='data:image/jpeg;base64,{imagenMostrar}' 
                     class='img-fluid rounded shadow-sm' 
                     alt='Evidencia' 
                     style='max-height: 250px; width: 100%; object-fit: cover; cursor: pointer;'
                     onclick='verImagenCompleta(""{evidencia.Imagen}"", {evidencia.id})' 
                     title='Click para ver en tamaño completo' />";
                }

                html += $@"
                        <hr class='my-2'/>
                        <div class='d-flex justify-content-between align-items-center'>
                            <small class='text-muted'>
                                <i class='fas fa-clock'></i> {evidencia.FSubidaEvidencia:dd/MM/yyyy HH:mm}
                            </small>";

                if (evidencia.TamanoOriginal.HasValue && evidencia.TamanoComprimido.HasValue && !esPDF)
                {
                    var reduccion = evidencia.TamanoOriginal.Value > 0
                        ? 100 - (evidencia.TamanoComprimido.Value * 100 / evidencia.TamanoOriginal.Value)
                        : 0;

                    html += $@"
                            <small class='text-success'>
                                <i class='fas fa-compress-arrows-alt'></i> -{reduccion}%
                            </small>";
                }

                html += @"
                        </div>
                    </div>
                </div>
            </div>";
            }

            html += "</div>";

            // Modal para ver imagen completa
            html += @"
        <div class='modal fade' id='modalImagenCompleta' tabindex='-1'>
            <div class='modal-dialog modal-xl modal-dialog-centered'>
                <div class='modal-content'>
                    <div class='modal-header'>
                        <h5 class='modal-title'>
                            <i class='fas fa-image me-2'></i>Evidencia Completa
                        </h5>
                        <button type='button' class='btn-close' data-bs-dismiss='modal'></button>
                    </div>
                    <div class='modal-body text-center bg-dark'>
                        <img id='imagenCompleta' src='' class='img-fluid' style='max-height: 80vh;' />
                    </div>
                    <div class='modal-footer'>
                        <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Cerrar</button>
                        <a id='btnDescargarImagen' href='#' download='evidencia.jpg' class='btn btn-primary'>
                            <i class='fas fa-download'></i> Descargar
                        </a>
                    </div>
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
    }
}