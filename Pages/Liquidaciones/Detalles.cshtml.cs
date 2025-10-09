using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.MODELS;
using ProyectoRH2025.Services;
using System.Data;
using System.Diagnostics;

namespace ProyectoRH2025.Pages.Liquidaciones
{
    public class DetallesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DetallesModel> _logger;
        private readonly ISharePointTestService _sharePointService;

        public DetallesModel(IConfiguration configuration, ILogger<DetallesModel> logger, ISharePointTestService sharePointService)
        {
            _configuration = configuration;
            _logger = logger;
            _sharePointService = sharePointService;
        }

        public LiquidacionDetalle? LiquidacionDetalle { get; set; }
        public List<EvidenciaInfo> EvidenciasInfo { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? StatusText { get; set; }
        public string? DiagnosticoTiempos { get; set; }
        public bool BusquedaEnSharePoint { get; set; } = false;
        public string OrigenDatos { get; set; } = "Base de Datos";

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var stopwatch = Stopwatch.StartNew();

            if (!id.HasValue)
            {
                ErrorMessage = "ID de liquidación no especificado.";
                return Page();
            }

            try
            {
                await BuscarEnBaseDatos(id.Value);

                if (LiquidacionDetalle != null && !EvidenciasInfo.Any())
                {
                    await BuscarEvidenciasEnSharePoint(LiquidacionDetalle.Folio);
                }
                else if (LiquidacionDetalle == null)
                {
                    await BuscarEnSharePoint(id.ToString());
                }

                stopwatch.Stop();
                DiagnosticoTiempos = $"Consulta completada en {stopwatch.ElapsedMilliseconds}ms - Origen: {OrigenDatos}";

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles de liquidación");
                ErrorMessage = "Error interno del servidor. Por favor, intente nuevamente.";
                stopwatch.Stop();
                DiagnosticoTiempos = $"Error en {stopwatch.ElapsedMilliseconds}ms";
                return Page();
            }
        }

        public async Task<IActionResult> OnGetSharePointImageAsync(string carpeta, string fileName)
        {
            _logger.LogInformation("HANDLER SharePoint Image - Carpeta: {Carpeta}, Archivo: {FileName}", carpeta, fileName);

            try
            {
                if (string.IsNullOrEmpty(carpeta) || string.IsNullOrEmpty(fileName))
                {
                    _logger.LogError("Parámetros requeridos faltantes");
                    return BadRequest("Carpeta y nombre de archivo son requeridos");
                }

                var imageBytes = await _sharePointService.GetFileBytesAsync(carpeta, fileName);

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    _logger.LogInformation("Imagen encontrada - Tamaño: {Size} bytes", imageBytes.Length);
                    var contentType = GetContentType(fileName);
                    return File(imageBytes, contentType);
                }

                _logger.LogWarning("Imagen no encontrada o vacía");
                return NotFound($"Imagen no encontrada: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR en handler SharePoint Image");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/jpeg"
            };
        }

        private async Task BuscarEvidenciasEnSharePoint(string folio)
        {
            try
            {
                BusquedaEnSharePoint = true;
                _logger.LogInformation("Buscando evidencias SharePoint para folio '{Folio}'", folio);

                var informacionViaje = await ObtenerInformacionAdicionalDeFolio(folio);

                if (informacionViaje?.FechaSalida.HasValue == true)
                {
                    var fechaProcesamiento = informacionViaje.FechaSalida.Value.AddDays(1);
                    var fechaCarpeta = fechaProcesamiento.ToString("yyyy-MM-dd");

                    _logger.LogInformation("Buscando en carpeta del día: {Fecha}", fechaCarpeta);

                    var contenidoDelDia = await _sharePointService.GetAllFolderContentsAsync(fechaCarpeta);

                    var carpetaPod = contenidoDelDia
                        .FirstOrDefault(item =>
                            item.IsFolder &&
                            item.Type != "status" &&
                            item.Type != "error" &&
                            item.Name.Equals($"POD_{informacionViaje.PodId}", StringComparison.OrdinalIgnoreCase));

                    if (carpetaPod != null)
                    {
                        _logger.LogInformation("Carpeta POD encontrada: {Nombre}", carpetaPod.Name);

                        var rutaCarpetaPod = $"{fechaCarpeta}/{carpetaPod.Name}";
                        var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(rutaCarpetaPod);

                        var imagenes = archivosEnCarpeta
                            .Where(archivo =>
                                !archivo.IsFolder &&
                                archivo.Type != "status" &&
                                archivo.Type != "error" &&
                                EsArchivoImagen(archivo.Name))
                            .ToList();

                        _logger.LogInformation("Imágenes encontradas: {Count}", imagenes.Count);

                        if (imagenes.Any())
                        {
                            EvidenciasInfo = imagenes.Select(imagen => new EvidenciaInfo
                            {
                                EvidenciaID = 0,
                                FileName = imagen.Name,
                                CaptureDate = imagen.Modified,
                                HasImage = true,
                                SharePointUrl = imagen.WebUrl,
                                IsFromSharePoint = true,
                                CarpetaSharePoint = rutaCarpetaPod
                            }).ToList();

                            OrigenDatos = "BD + SharePoint";
                            StatusText += $" (Encontradas {imagenes.Count} imágenes en {carpetaPod.Name})";

                            foreach (var evidencia in EvidenciasInfo)
                            {
                                _logger.LogInformation("Evidencia configurada: {FileName}, HasImage: {HasImage}, Carpeta: {Carpeta}",
                                    evidencia.FileName, evidencia.HasImage, evidencia.CarpetaSharePoint);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No se encontraron imágenes en la carpeta POD");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró carpeta POD_{PodId} en {Fecha}", informacionViaje.PodId, fechaCarpeta);
                        await BuscarEnFechaExacta(informacionViaje, informacionViaje.FechaSalida.Value.ToString("yyyy-MM-dd"));
                    }
                }
                else
                {
                    _logger.LogWarning("No hay fecha de salida para buscar en SharePoint");
                    OrigenDatos = "BD (sin fecha para SharePoint)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando evidencias en SharePoint");
                OrigenDatos = "BD (error SharePoint)";
            }
        }

        private async Task BuscarEnFechaExacta(InformacionAdicionalFolio informacionViaje, string fechaExacta)
        {
            try
            {
                var contenidoExacto = await _sharePointService.GetAllFolderContentsAsync(fechaExacta);

                var carpetaPodExacta = contenidoExacto
                    .FirstOrDefault(item =>
                        item.IsFolder &&
                        item.Type != "status" &&
                        item.Type != "error" &&
                        item.Name.Equals($"POD_{informacionViaje.PodId}", StringComparison.OrdinalIgnoreCase));

                if (carpetaPodExacta != null)
                {
                    var rutaCarpetaPod = $"{fechaExacta}/{carpetaPodExacta.Name}";
                    var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(rutaCarpetaPod);

                    var imagenes = archivosEnCarpeta
                        .Where(archivo =>
                            !archivo.IsFolder &&
                            archivo.Type != "status" &&
                            archivo.Type != "error" &&
                            EsArchivoImagen(archivo.Name))
                        .ToList();

                    if (imagenes.Any())
                    {
                        EvidenciasInfo = imagenes.Select(imagen => new EvidenciaInfo
                        {
                            EvidenciaID = 0,
                            FileName = imagen.Name,
                            CaptureDate = imagen.Modified,
                            HasImage = true,
                            SharePointUrl = imagen.WebUrl,
                            IsFromSharePoint = true,
                            CarpetaSharePoint = rutaCarpetaPod
                        }).ToList();

                        OrigenDatos = "BD + SharePoint";
                        StatusText += $" (Encontradas {imagenes.Count} imágenes en fecha exacta)";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando en fecha exacta");
            }
        }

        private List<DateTime> GenerarFechasDeRuteo(DateTime fechaSalida)
        {
            var fechas = new List<DateTime>();
            fechas.Add(fechaSalida.AddDays(1));
            fechas.Add(fechaSalida);
            fechas.Add(fechaSalida.AddDays(2));
            fechas.Add(fechaSalida.AddDays(-1));
            return fechas;
        }

        private async Task<List<SharePointFileInfo>> BuscarArchivosEnCarpetaEspecifica(string carpeta, int podId)
        {
            try
            {
                var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(carpeta);

                var archivosCoincidentes = archivosEnCarpeta
                    .Where(archivo =>
                        archivo.Type != "status" &&
                        archivo.Type != "error" &&
                        !archivo.IsFolder &&
                        CoincideConPodId(archivo.Name, podId))
                    .ToList();

                return archivosCoincidentes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error buscando en carpeta {Carpeta}", carpeta);
                return new List<SharePointFileInfo>();
            }
        }

        private bool CoincideConPodId(string nombreArchivo, int podId)
        {
            var patronesABuscar = new[]
            {
                $"POD_{podId}",
                $"POD{podId}",
                podId.ToString(),
                $"_{podId}_",
                $"_{podId}.",
                $"POD_{podId:D4}",
                $"POD_{podId:D5}"
            };

            return patronesABuscar.Any(patron =>
                nombreArchivo.Contains(patron, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<SharePointFileInfo>> BuscarPorPodIdEnCarpetasRecientes(int podId)
        {
            var archivos = new List<SharePointFileInfo>();

            try
            {
                var carpetasRaiz = await _sharePointService.GetAllFolderContentsAsync();
                var carpetasFecha = carpetasRaiz
                    .Where(item => item.IsFolder && item.Name.StartsWith("2025-"))
                    .OrderByDescending(item => item.Name)
                    .Take(7);

                foreach (var carpeta in carpetasFecha)
                {
                    var archivosEncontrados = await BuscarArchivosEnCarpetaEspecifica(carpeta.Name, podId);

                    if (archivosEncontrados.Any())
                    {
                        archivos.AddRange(archivosEncontrados);
                        _logger.LogInformation("Encontrados {Count} archivos para POD_ID {PodId} en {Carpeta}",
                            archivosEncontrados.Count, podId, carpeta.Name);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en búsqueda por POD_ID en carpetas recientes");
            }

            return archivos;
        }

        private async Task<InformacionAdicionalFolio?> ObtenerInformacionAdicionalDeFolio(string folio)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    SELECT TOP 5 
                        POD_ID, Folio, FechaSalida, Cliente, Tractor, Remolque
                    FROM POD_Records 
                    WHERE Folio = @Folio 
                       OR Folio LIKE '%' + @Folio + '%'
                       OR @Folio LIKE '%' + Folio + '%'
                    ORDER BY 
                        CASE WHEN Folio = @Folio THEN 1 ELSE 2 END,
                        POD_ID DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Folio", folio);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new InformacionAdicionalFolio
                    {
                        PodId = reader.GetInt32("POD_ID"),
                        Folio = reader["Folio"]?.ToString(),
                        FechaSalida = reader["FechaSalida"] as DateTime?,
                        Cliente = reader["Cliente"]?.ToString(),
                        Tractor = reader["Tractor"]?.ToString(),
                        Remolque = reader["Remolque"]?.ToString()
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obteniendo información adicional del folio");
                return null;
            }
        }

        private async Task<List<SharePointFileInfo>> BuscarPorPatronesPodId(int podId)
        {
            var archivos = new List<SharePointFileInfo>();

            try
            {
                var carpetasRaiz = await _sharePointService.GetAllFolderContentsAsync();
                var carpetasFecha = carpetasRaiz
                    .Where(item => item.IsFolder && item.Name.StartsWith("2025-"))
                    .OrderByDescending(item => item.Name)
                    .Take(5);

                foreach (var carpeta in carpetasFecha)
                {
                    try
                    {
                        var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(carpeta.Name);

                        var archivosRelacionados = archivosEnCarpeta
                            .Where(archivo =>
                                archivo.Type != "status" &&
                                archivo.Type != "error" &&
                                !archivo.IsFolder &&
                                (archivo.Name.Contains(podId.ToString()) ||
                                 archivo.Name.Contains($"POD_{podId}") ||
                                 archivo.Name.Contains($"POD{podId}")))
                            .ToList();

                        if (archivosRelacionados.Any())
                        {
                            archivos.AddRange(archivosRelacionados);
                            _logger.LogInformation("Encontrados {Count} archivos relacionados con POD_ID {PodId} en {Carpeta}",
                                archivosRelacionados.Count, podId, carpeta.Name);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error buscando en carpeta {Carpeta}", carpeta.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en búsqueda por patrones POD_ID");
            }

            return archivos;
        }

        private async Task<List<SharePointFileInfo>> BuscarPorFolioDirecto(string folio)
        {
            try
            {
                var archivosRaiz = await _sharePointService.GetAllFolderContentsAsync();
                return archivosRaiz
                    .Where(archivo => archivo.Name.Contains(folio, StringComparison.OrdinalIgnoreCase) &&
                                     archivo.Type != "status" &&
                                     archivo.Type != "error" &&
                                     !archivo.IsFolder)
                    .ToList();
            }
            catch
            {
                return new List<SharePointFileInfo>();
            }
        }

        private async Task BuscarEnBaseDatos(int id)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var queryPrincipal = @"
                    SELECT 
                        p.POD_ID, p.Folio, p.Tractor, p.Remolque, p.FechaSalida,
                        p.Origen, p.Destino, p.Cliente, p.Status, p.CaptureDate,
                        p.DriverName, p.Plant
                    FROM POD_Records p
                    WHERE p.POD_ID = @PodId";

                using var command = new SqlCommand(queryPrincipal, connection);
                command.Parameters.AddWithValue("@PodId", id);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    LiquidacionDetalle = new LiquidacionDetalle
                    {
                        POD_ID = reader.GetInt32("POD_ID"),
                        Folio = reader["Folio"]?.ToString(),
                        Tractor = reader["Tractor"]?.ToString(),
                        Remolque = reader["Remolque"]?.ToString(),
                        FechaSalida = reader["FechaSalida"] as DateTime?,
                        Origen = reader["Origen"]?.ToString(),
                        Destino = reader["Destino"]?.ToString(),
                        Cliente = reader["Cliente"]?.ToString(),
                        Status = reader["Status"]?.ToString(),
                        CaptureDate = reader["CaptureDate"] as DateTime?,
                        DriverName = reader["DriverName"]?.ToString(),
                        Plant = reader["Plant"]?.ToString()
                    };

                    StatusText = DeterminarStatusText(LiquidacionDetalle.Status);
                    OrigenDatos = "Base de Datos";
                }

                reader.Close();

                if (LiquidacionDetalle != null)
                {
                    await BuscarEvidenciasEnBD(connection, id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar en base de datos");
                throw;
            }
        }

        private async Task BuscarEvidenciasEnBD(SqlConnection connection, int podId)
        {
            try
            {
                var queryEvidencias = @"
                    SELECT EvidenciaID, FileName, CaptureDate, ImageData
                    FROM POD_Evidencias_Imagenes 
                    WHERE POD_ID_FK = @PodId 
                    ORDER BY CaptureDate DESC";

                using var commandEvidencias = new SqlCommand(queryEvidencias, connection);
                commandEvidencias.Parameters.AddWithValue("@PodId", podId);

                using var readerEvidencias = await commandEvidencias.ExecuteReaderAsync();

                while (await readerEvidencias.ReadAsync())
                {
                    var evidencia = new EvidenciaInfo
                    {
                        EvidenciaID = readerEvidencias.GetInt32("EvidenciaID"),
                        FileName = readerEvidencias["FileName"]?.ToString(),
                        CaptureDate = readerEvidencias["CaptureDate"] as DateTime?,
                        HasImage = !readerEvidencias.IsDBNull("ImageData") &&
                                  ((byte[])readerEvidencias["ImageData"]).Length > 0,
                        IsFromSharePoint = false
                    };

                    EvidenciasInfo.Add(evidencia);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar evidencias en BD");
            }
        }

        private async Task BuscarEnSharePoint(string folio)
        {
            try
            {
                BusquedaEnSharePoint = true;
                _logger.LogInformation("Buscando folio '{Folio}' en SharePoint", folio);

                var archivosSharePoint = await _sharePointService.GetAllFolderContentsAsync();

                var archivosFiltrados = archivosSharePoint
                    .Where(archivo => archivo.Name.Contains(folio, StringComparison.OrdinalIgnoreCase) &&
                                     archivo.Type != "status" &&
                                     archivo.Type != "error")
                    .ToList();

                if (archivosFiltrados.Any())
                {
                    LiquidacionDetalle = new LiquidacionDetalle
                    {
                        POD_ID = 0,
                        Folio = folio,
                        Cliente = "Datos desde SharePoint",
                        Tractor = "Ver SharePoint",
                        Remolque = "Ver SharePoint",
                        Status = "Encontrado en SharePoint",
                        Origen = "SharePoint",
                        Destino = "SharePoint",
                        FechaSalida = archivosFiltrados.FirstOrDefault()?.Modified,
                        DriverName = "N/A",
                        Plant = "SharePoint",
                        CaptureDate = archivosFiltrados.FirstOrDefault()?.Modified
                    };

                    EvidenciasInfo = archivosFiltrados.Select(archivo => new EvidenciaInfo
                    {
                        EvidenciaID = 0,
                        FileName = archivo.Name,
                        CaptureDate = archivo.Modified,
                        HasImage = EsArchivoImagen(archivo.Name),
                        SharePointUrl = archivo.WebUrl,
                        IsFromSharePoint = true
                    }).ToList();

                    StatusText = $"Encontrado en SharePoint ({archivosFiltrados.Count} archivos)";
                    OrigenDatos = "SharePoint";

                    _logger.LogInformation("Encontrados {Count} archivos para folio '{Folio}' en SharePoint", archivosFiltrados.Count, folio);
                }
                else
                {
                    ErrorMessage = $"Folio '{folio}' no encontrado ni en Base de Datos ni en SharePoint.";
                    _logger.LogInformation("Folio '{Folio}' no encontrado en SharePoint", folio);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar en SharePoint");
                ErrorMessage = $"No encontrado en BD. Error al buscar en SharePoint: {ex.Message}";
            }
        }

        private bool EsArchivoImagen(string nombreArchivo)
        {
            if (string.IsNullOrEmpty(nombreArchivo))
                return false;

            var extensionesImagen = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif" };
            var extension = Path.GetExtension(nombreArchivo)?.ToLowerInvariant();

            return !string.IsNullOrEmpty(extension) && extensionesImagen.Contains(extension);
        }

        public async Task<IActionResult> OnGetImageAsync(int evidenciaId)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = "SELECT ImageData FROM POD_Evidencias_Imagenes WHERE EvidenciaID = @EvidenciaID";
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@EvidenciaID", evidenciaId);

                var imageData = await command.ExecuteScalarAsync() as byte[];

                if (imageData != null && imageData.Length > 0)
                {
                    return File(imageData, "image/jpeg");
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener imagen");
                return NotFound();
            }
        }

        private string DeterminarStatusText(string? status)
        {
            return status switch
            {
                "1" => "En Tránsito",
                "2" => "Entregado",
                "3" => "Cancelado",
                _ => status ?? "Desconocido"
            };
        }

        // ========================================================================
        // HANDLER DE SUSTITUCIÓN DE IMÁGENES
        // ========================================================================

        [RequestSizeLimit(10485760)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        public async Task<IActionResult> OnPostSustituirImagenAsync(
    int evidenciaId,
    IFormFile archivoNuevo,
    bool esSharePoint,
    string? carpetaSharePoint = null,
    string? nombreArchivoActual = null,
    int? podIdFk = null)
        {
            try
            {
                _logger.LogInformation("=== INICIO SUSTITUCIÓN ===");
                _logger.LogInformation("EvidenciaID: {Id}, SharePoint: {SP}, Carpeta: {Carpeta}, POD: {Pod}",
                    evidenciaId, esSharePoint, carpetaSharePoint, podIdFk);

                // VALIDACIÓN: Archivo
                if (archivoNuevo == null || archivoNuevo.Length == 0)
                {
                    return new JsonResult(new { success = false, error = "No se ha seleccionado ningún archivo" });
                }

                // VALIDACIÓN: Tamaño
                if (archivoNuevo.Length > 10 * 1024 * 1024)
                {
                    return new JsonResult(new { success = false, error = "El archivo no debe superar 10 MB" });
                }

                // VALIDACIÓN: Extensión
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".bmp" };
                var extension = Path.GetExtension(archivoNuevo.FileName).ToLowerInvariant();

                if (!extensionesPermitidas.Contains(extension))
                {
                    return new JsonResult(new { success = false, error = "Tipo de archivo no permitido" });
                }

                // Leer contenido
                byte[] nuevoContenido;
                using (var ms = new MemoryStream())
                {
                    await archivoNuevo.CopyToAsync(ms);
                    nuevoContenido = ms.ToArray();
                }

                _logger.LogInformation("Archivo leído: {Size} bytes", nuevoContenido.Length);

                var resultado = new ResultadoSustitucionInfo();

                // ===== CASO 1: IMAGEN SOLO EN SHAREPOINT =====
                if (evidenciaId == 0 && !string.IsNullOrEmpty(carpetaSharePoint) && !string.IsNullOrEmpty(nombreArchivoActual))
                {
                    _logger.LogInformation("Procesando imagen SOLO SharePoint - Carpeta: {Carpeta}", carpetaSharePoint);

                    resultado.ActualizadaEnSharePoint = await SustituirImagenEnSharePoint(
                        carpetaSharePoint,
                        nombreArchivoActual,
                        nuevoContenido
                    );

                    if (resultado.ActualizadaEnSharePoint)
                    {
                        await RegistrarAuditoriaSharePointDirecto(
                            podIdFk ?? 0,
                            carpetaSharePoint,
                            nombreArchivoActual,
                            archivoNuevo.FileName,
                            nuevoContenido
                        );

                        resultado.Mensaje = "✅ Imagen sustituida directamente en SharePoint (sin registro en BD).";
                        _logger.LogInformation("✅ Sustitución SharePoint exitosa");
                    }
                    else
                    {
                        resultado.Mensaje = "❌ Error al sustituir imagen en SharePoint.";
                        _logger.LogWarning("❌ Fallo en sustitución SharePoint");
                    }

                    return new JsonResult(new { success = resultado.ActualizadaEnSharePoint, resultado });
                }

                // ===== CASO 2: IMAGEN CON REGISTRO EN BD =====
                var evidenciaInfo = await ObtenerInformacionEvidenciaSustitucion(evidenciaId);

                if (evidenciaInfo == null)
                {
                    _logger.LogError("No se encontró evidencia con ID: {Id}", evidenciaId);
                    return new JsonResult(new
                    {
                        success = false,
                        error = $"No se encontró la evidencia con ID {evidenciaId}"
                    });
                }

                _logger.LogInformation("Evidencia encontrada: {Nombre}, Migrada: {Migrada}",
                    evidenciaInfo.NombreArchivo, evidenciaInfo.MigradaSharePoint);

                var estrategia = DeterminarEstrategiaSustitucionInteligente(evidenciaInfo);

                switch (estrategia)
                {
                    case EstrategiaSustitucionTipo.BaseDatosPrimero:
                        resultado.ActualizadaEnBD = await SustituirImagenEnBaseDatos(
                            evidenciaId, nuevoContenido, archivoNuevo.FileName);
                        resultado.Mensaje = "Imagen sustituida en BD. Se sincronizará a SharePoint.";
                        break;

                    case EstrategiaSustitucionTipo.SharePointDirecto:
                        if (!string.IsNullOrEmpty(evidenciaInfo.CarpetaSharePoint))
                        {
                            resultado.ActualizadaEnSharePoint = await SustituirImagenEnSharePoint(
                                evidenciaInfo.CarpetaSharePoint, evidenciaInfo.NombreArchivo, nuevoContenido);
                        }
                        resultado.Mensaje = "Imagen sustituida directamente en SharePoint.";
                        break;

                    case EstrategiaSustitucionTipo.Ambos:
                        resultado.ActualizadaEnBD = await SustituirImagenEnBaseDatos(
                            evidenciaId, nuevoContenido, archivoNuevo.FileName);

                        if (resultado.ActualizadaEnBD && !string.IsNullOrEmpty(evidenciaInfo.CarpetaSharePoint))
                        {
                            resultado.ActualizadaEnSharePoint = await SustituirImagenEnSharePoint(
                                evidenciaInfo.CarpetaSharePoint, evidenciaInfo.NombreArchivo, nuevoContenido);
                        }
                        resultado.Mensaje = "Imagen sustituida en BD y SharePoint.";
                        break;
                }

                await RegistrarAuditoriaImagenSustituida(
                    evidenciaId, evidenciaInfo, estrategia, nuevoContenido, archivoNuevo.FileName);

                _logger.LogInformation("=== SUSTITUCIÓN COMPLETADA ===");

                return new JsonResult(new { success = true, resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR en sustitución");
                return new JsonResult(new { success = false, error = $"Error interno: {ex.Message}" });
            }
        }
        private async Task RegistrarAuditoriaSharePointDirecto(
            int podId,
            string carpetaSharePoint,
            string nombreAnterior,
            string nombreNuevo,
            byte[] nuevoContenido)
        {
            try
            {
                _logger.LogInformation("Registrando auditoría SharePoint directo...");

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
            EXEC sp_RegistrarSustitucionSharePoint 
                @CarpetaSharePoint = @CarpetaSharePoint,
                @POD_ID_FK = @POD_ID_FK,
                @NombreArchivoAnterior = @NombreAnterior,
                @NombreArchivoNuevo = @NombreNuevo,
                @TamanoAnterior = @TamanoAnterior,
                @TamanoNuevo = @TamanoNuevo,
                @Usuario = @Usuario,
                @IP = @IP,
                @Observaciones = @Observaciones";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CarpetaSharePoint", carpetaSharePoint);
                command.Parameters.AddWithValue("@POD_ID_FK", podId);
                command.Parameters.AddWithValue("@NombreAnterior", nombreAnterior);
                command.Parameters.AddWithValue("@NombreNuevo", nombreNuevo);
                command.Parameters.AddWithValue("@TamanoAnterior", 0); // Desconocido
                command.Parameters.AddWithValue("@TamanoNuevo", nuevoContenido.Length);
                command.Parameters.AddWithValue("@Usuario", HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema");
                command.Parameters.AddWithValue("@IP", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
                command.Parameters.AddWithValue("@Observaciones", $"Sustitución directa en SharePoint | Carpeta: {carpetaSharePoint}");

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("✅ Auditoría SharePoint registrada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando auditoría SharePoint (no crítico)");
            }
        }
        private async Task<InformacionEvidenciaSustitucion?> ObtenerInformacionEvidenciaSustitucion(int evidenciaId)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
            SELECT 
                e.EvidenciaID,
                e.POD_ID_FK,
                e.FileName,
                e.CaptureDate,
                e.MigradaSharePoint,
                ISNULL(DATALENGTH(e.ImageData), 0) as TamanoActual,
                p.Folio,
                p.FechaSalida
            FROM POD_Evidencias_Imagenes e
            INNER JOIN POD_Records p ON e.POD_ID_FK = p.POD_ID
            WHERE e.EvidenciaID = @EvidenciaID";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@EvidenciaID", evidenciaId);

                // ⬇️ AGREGA ESTE LOG TEMPORAL ⬇️
                _logger.LogInformation("🔍 Buscando EvidenciaID: {Id} en BD", evidenciaId);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // ⬇️ AGREGA ESTE LOG TEMPORAL ⬇️
                    _logger.LogInformation("✅ Registro encontrado en BD");

                    var captureDate = reader["CaptureDate"] as DateTime?;
                    var fechaSalida = reader["FechaSalida"] as DateTime?;

                    string? carpetaSharePoint = null;
                    if (captureDate.HasValue)
                    {
                        var fechaProcesamiento = (fechaSalida ?? captureDate.Value).AddDays(1);
                        carpetaSharePoint = $"{fechaProcesamiento:yyyy-MM-dd}/POD_{reader.GetInt32(1)}";
                    }

                    return new InformacionEvidenciaSustitucion
                    {
                        EvidenciaID = reader.GetInt32(0),
                        PodId = reader.GetInt32(1),
                        NombreArchivo = reader["FileName"]?.ToString() ?? "",
                        FechaCaptura = captureDate,
                        FechaSalida = fechaSalida,
                        MigradaSharePoint = reader["MigradaSharePoint"] as bool? ?? false,
                        Folio = reader["Folio"]?.ToString(),
                        CarpetaSharePoint = carpetaSharePoint,
                        TamanoActual = reader.IsDBNull(6) ? 0 : Convert.ToInt64(reader[6])  // ✅ CORREGIDO
                    };
                }

                // ⬇️ AGREGA ESTE LOG TEMPORAL ⬇️
                _logger.LogWarning("❌ No se encontró registro en BD para EvidenciaID: {Id}", evidenciaId);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo información de evidencia {Id}", evidenciaId);
                return null;
            }
        }
        private EstrategiaSustitucionTipo DeterminarEstrategiaSustitucionInteligente(InformacionEvidenciaSustitucion evidencia)
        {
            var fechaLimite = DateTime.Today.AddDays(-30);

            if (evidencia.FechaCaptura >= fechaLimite)
            {
                _logger.LogInformation("Mes actual detectado - Estrategia: BaseDatosPrimero");
                return EstrategiaSustitucionTipo.BaseDatosPrimero;
            }
            else if (evidencia.MigradaSharePoint)
            {
                _logger.LogInformation("Ya migrada a SharePoint - Estrategia: SharePointDirecto");
                return EstrategiaSustitucionTipo.SharePointDirecto;
            }
            else
            {
                _logger.LogInformation("Caso especial - Estrategia: Ambos");
                return EstrategiaSustitucionTipo.Ambos;
            }
        }

        private async Task<bool> SustituirImagenEnBaseDatos(int evidenciaId, byte[] nuevoContenido, string nuevoNombre)
        {
            try
            {
                _logger.LogInformation("Sustituyendo en BD - EvidenciaID: {Id}", evidenciaId);

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    UPDATE POD_Evidencias_Imagenes 
                    SET ImageData = @ImageData,
                        FileName = @FileName,
                        MimeType = @MimeType
                    WHERE EvidenciaID = @EvidenciaID";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@EvidenciaID", evidenciaId);
                command.Parameters.AddWithValue("@ImageData", nuevoContenido);
                command.Parameters.AddWithValue("@FileName", nuevoNombre);
                command.Parameters.AddWithValue("@MimeType", ObtenerMimeType(nuevoNombre));

                var filasActualizadas = await command.ExecuteNonQueryAsync();

                if (filasActualizadas > 0)
                {
                    _logger.LogInformation("BD actualizada - {Filas} fila(s)", filasActualizadas);
                    return true;
                }

                _logger.LogWarning("No se actualizó ninguna fila en BD");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sustituyendo en BD");
                return false;
            }
        }

        private async Task<bool> SustituirImagenEnSharePoint(string carpeta, string nombreArchivo, byte[] nuevoContenido)
        {
            try
            {
                _logger.LogInformation("Sustituyendo en SharePoint - {Carpeta}/{Archivo}", carpeta, nombreArchivo);

                var resultado = await _sharePointService.ReplaceFileAsync(carpeta, nombreArchivo, nuevoContenido);

                if (resultado)
                {
                    _logger.LogInformation("SharePoint actualizado");
                }
                else
                {
                    _logger.LogWarning("Fallo al actualizar SharePoint");
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sustituyendo en SharePoint");
                return false;
            }
        }
        private string ObtenerMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }
        private async Task RegistrarAuditoriaImagenSustituida(
            int evidenciaId,
            InformacionEvidenciaSustitucion evidencia,
            EstrategiaSustitucionTipo estrategia,
            byte[] nuevoContenido,
            string nuevoNombre)
        {
            try
            {
                _logger.LogInformation("Registrando auditoría...");

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    EXEC sp_RegistrarSustitucionImagen 
                        @EvidenciaID = @EvidenciaID,
                        @POD_ID_FK = @POD_ID_FK,
                        @NombreArchivoAnterior = @NombreAnterior,
                        @NombreArchivoNuevo = @NombreNuevo,
                        @TamanoAnterior = @TamanoAnterior,
                        @TamanoNuevo = @TamanoNuevo,
                        @Origen = @Origen,
                        @Usuario = @Usuario,
                        @IP = @IP,
                        @Observaciones = @Observaciones";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@EvidenciaID", evidenciaId);
                command.Parameters.AddWithValue("@POD_ID_FK", evidencia.PodId);
                command.Parameters.AddWithValue("@NombreAnterior", evidencia.NombreArchivo);
                command.Parameters.AddWithValue("@NombreNuevo", nuevoNombre);
                command.Parameters.AddWithValue("@TamanoAnterior", evidencia.TamanoActual);
                command.Parameters.AddWithValue("@TamanoNuevo", nuevoContenido.Length);
                command.Parameters.AddWithValue("@Origen", estrategia.ToString());
                command.Parameters.AddWithValue("@Usuario", HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema");
                command.Parameters.AddWithValue("@IP", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
                command.Parameters.AddWithValue("@Observaciones", $"Folio: {evidencia.Folio} | Estrategia: {estrategia}");

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Auditoría registrada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando auditoría (no crítico)");
            }
        }

        public enum EstrategiaSustitucionTipo
        {
            BaseDatosPrimero,
            SharePointDirecto,
            Ambos
        }

        public class InformacionEvidenciaSustitucion
        {
            public int EvidenciaID { get; set; }
            public int PodId { get; set; }
            public string NombreArchivo { get; set; } = string.Empty;
            public DateTime? FechaCaptura { get; set; }
            public DateTime? FechaSalida { get; set; }
            public bool MigradaSharePoint { get; set; }
            public string? Folio { get; set; }
            public string? CarpetaSharePoint { get; set; }
            public long TamanoActual { get; set; }
        }

        public class ResultadoSustitucionInfo
        {
            public bool ActualizadaEnBD { get; set; }
            public bool ActualizadaEnSharePoint { get; set; }
            public string Mensaje { get; set; } = string.Empty;
        }

        public class InformacionAdicionalFolio
        {
            public int PodId { get; set; }
            public string? Folio { get; set; }
            public DateTime? FechaSalida { get; set; }
            public string? Cliente { get; set; }
            public string? Tractor { get; set; }
            public string? Remolque { get; set; }
        }
    }

    public class LiquidacionDetalle
    {
        public int POD_ID { get; set; }
        public string? Folio { get; set; }
        public string? Tractor { get; set; }
        public string? Remolque { get; set; }
        public DateTime? FechaSalida { get; set; }
        public string? Origen { get; set; }
        public string? Destino { get; set; }
        public string? Cliente { get; set; }
        public string? Status { get; set; }
        public DateTime? CaptureDate { get; set; }
        public string? DriverName { get; set; }
        public string? Plant { get; set; }
    }

    public class EvidenciaInfo
    {
        public int EvidenciaID { get; set; }
        public string? FileName { get; set; }
        public DateTime? CaptureDate { get; set; }
        public bool HasImage { get; set; }
        public bool IsFromSharePoint { get; set; } = false;
        public string? SharePointUrl { get; set; }
        public string? CarpetaSharePoint { get; set; }
    }
}