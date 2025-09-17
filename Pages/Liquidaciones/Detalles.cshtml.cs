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
        private readonly ISharePointTestService _sharePointService; // NUEVO

        public DetallesModel(IConfiguration configuration, ILogger<DetallesModel> logger, ISharePointTestService sharePointService) // MODIFICADO
        {
            _configuration = configuration;
            _logger = logger;
            _sharePointService = sharePointService; // NUEVO
        }

        // Propiedades del modelo
        public LiquidacionDetalle? LiquidacionDetalle { get; set; }
        public List<EvidenciaInfo> EvidenciasInfo { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? StatusText { get; set; }
        public string? DiagnosticoTiempos { get; set; }

        // NUEVAS PROPIEDADES PARA SHAREPOINT
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
                // PASO 1: Buscar en la base de datos
                await BuscarEnBaseDatos(id.Value);

                // PASO 2: Si encontramos el registro pero NO tiene evidencias, buscar en SharePoint
                if (LiquidacionDetalle != null && !EvidenciasInfo.Any())
                {
                    await BuscarEvidenciasEnSharePoint(LiquidacionDetalle.Folio);
                }
                // PASO 3: Si no encontramos nada en BD, buscar todo en SharePoint
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
            _logger.LogInformation("🖼️ HANDLER SharePoint Image - Carpeta: {Carpeta}, Archivo: {FileName}", carpeta, fileName);

            try
            {
                if (string.IsNullOrEmpty(carpeta) || string.IsNullOrEmpty(fileName))
                {
                    _logger.LogError("❌ Parámetros requeridos faltantes");
                    return BadRequest("Carpeta y nombre de archivo son requeridos");
                }

                // Usar el servicio SharePoint para obtener los bytes de la imagen
                var imageBytes = await _sharePointService.GetFileBytesAsync(carpeta, fileName);

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    _logger.LogInformation("✅ Imagen encontrada - Tamaño: {Size} bytes", imageBytes.Length);

                    // Determinar el tipo MIME basado en la extensión
                    var contentType = GetContentType(fileName);
                    return File(imageBytes, contentType);
                }

                _logger.LogWarning("❌ Imagen no encontrada o vacía");
                return NotFound($"Imagen no encontrada: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR en handler SharePoint Image");
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
                _ => "image/jpeg" // Default
            };
        }

        // MÉTODO ACTUALIZADO: Considerar el patrón temporal real de tu operación
        private async Task BuscarEvidenciasEnSharePoint(string folio)
        {
            try
            {
                BusquedaEnSharePoint = true;
                _logger.LogInformation("🔍 Buscando evidencias SharePoint para folio '{Folio}'", folio);

                var informacionViaje = await ObtenerInformacionAdicionalDeFolio(folio);

                if (informacionViaje?.FechaSalida.HasValue == true)
                {
                    var fechaProcesamiento = informacionViaje.FechaSalida.Value.AddDays(1);
                    var fechaCarpeta = fechaProcesamiento.ToString("yyyy-MM-dd");

                    _logger.LogInformation("📅 Buscando en carpeta del día: {Fecha}", fechaCarpeta);

                    var contenidoDelDia = await _sharePointService.GetAllFolderContentsAsync(fechaCarpeta);

                    // Buscar carpeta POD específica
                    var carpetaPod = contenidoDelDia
                        .FirstOrDefault(item =>
                            item.IsFolder &&
                            item.Type != "status" &&
                            item.Type != "error" &&
                            item.Name.Equals($"POD_{informacionViaje.PodId}", StringComparison.OrdinalIgnoreCase));

                    if (carpetaPod != null)
                    {
                        _logger.LogInformation("📁 Carpeta POD encontrada: {Nombre}", carpetaPod.Name);

                        // Obtener archivos dentro de la carpeta POD
                        var rutaCarpetaPod = $"{fechaCarpeta}/{carpetaPod.Name}";
                        var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(rutaCarpetaPod);

                        var imagenes = archivosEnCarpeta
                            .Where(archivo =>
                                !archivo.IsFolder &&
                                archivo.Type != "status" &&
                                archivo.Type != "error" &&
                                EsArchivoImagen(archivo.Name))
                            .ToList();

                        _logger.LogInformation("🖼️ Imágenes encontradas: {Count}", imagenes.Count);

                        if (imagenes.Any())
                        {
                            EvidenciasInfo = imagenes.Select(imagen => new EvidenciaInfo
                            {
                                EvidenciaID = 0,
                                FileName = imagen.Name,
                                CaptureDate = imagen.Modified,
                                HasImage = true, // ✅ CRUCIAL: Esto debe ser true
                                SharePointUrl = imagen.WebUrl,
                                IsFromSharePoint = true,
                                CarpetaSharePoint = rutaCarpetaPod // ✅ Ruta completa para el handler
                            }).ToList();

                            OrigenDatos = "BD + SharePoint";
                            StatusText += $" (Encontradas {imagenes.Count} imágenes en {carpetaPod.Name})";

                            // Log de debug
                            foreach (var evidencia in EvidenciasInfo)
                            {
                                _logger.LogInformation("📄 Evidencia configurada: {FileName}, HasImage: {HasImage}, Carpeta: {Carpeta}",
                                    evidencia.FileName, evidencia.HasImage, evidencia.CarpetaSharePoint);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ No se encontraron imágenes en la carpeta POD");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("❌ No se encontró carpeta POD_{PodId} en {Fecha}", informacionViaje.PodId, fechaCarpeta);

                        // Fallback: buscar en fecha exacta
                        await BuscarEnFechaExacta(informacionViaje, informacionViaje.FechaSalida.Value.ToString("yyyy-MM-dd"));
                    }
                }
                else
                {
                    _logger.LogWarning("❌ No hay fecha de salida para buscar en SharePoint");
                    OrigenDatos = "BD (sin fecha para SharePoint)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error buscando evidencias en SharePoint");
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
        // NUEVO: Generar fechas de búsqueda considerando el patrón operativo
        private List<DateTime> GenerarFechasDeRuteo(DateTime fechaSalida)
        {
            var fechas = new List<DateTime>();

            // Patrón observado: documentos se crean el día siguiente
            fechas.Add(fechaSalida.AddDays(1));  // +1 día (patrón principal)
            fechas.Add(fechaSalida);             // Mismo día (por si acaso)
            fechas.Add(fechaSalida.AddDays(2));  // +2 días (retrasos)
            fechas.Add(fechaSalida.AddDays(-1)); // -1 día (adelantos)

            return fechas;
        }
        // NUEVO: Buscar archivos específicos en una carpeta
        private async Task<List<SharePointFileInfo>> BuscarArchivosEnCarpetaEspecifica(string carpeta, int podId)
        {
            try
            {
                var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(carpeta);

                // Buscar archivos que coincidan con el POD_ID
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
        // NUEVO: Verificar si un nombre de archivo coincide con el POD_ID
        private bool CoincideConPodId(string nombreArchivo, int podId)
        {
            // Patrones de búsqueda flexibles
            var patronesABuscar = new[]
            {
        $"POD_{podId}",           // POD_9979
        $"POD{podId}",            // POD9979  
        podId.ToString(),         // 9979
        $"_{podId}_",             // _9979_
        $"_{podId}.",             // _9979.ext
        $"POD_{podId:D4}",        // POD_0979 (con padding)
        $"POD_{podId:D5}"         // POD_09979 (con más padding)
    };

            return patronesABuscar.Any(patron =>
                nombreArchivo.Contains(patron, StringComparison.OrdinalIgnoreCase));
        }
        // NUEVO: Buscar por POD_ID en carpetas recientes como fallback
        private async Task<List<SharePointFileInfo>> BuscarPorPodIdEnCarpetasRecientes(int podId)
        {
            var archivos = new List<SharePointFileInfo>();

            try
            {
                // Obtener carpetas de fechas recientes
                var carpetasRaiz = await _sharePointService.GetAllFolderContentsAsync();       
                var carpetasFecha = carpetasRaiz
                    .Where(item => item.IsFolder && item.Name.StartsWith("2025-"))
                    .OrderByDescending(item => item.Name)
                    .Take(7); // Buscar en la última semana

                foreach (var carpeta in carpetasFecha)
                {
                    var archivosEncontrados = await BuscarArchivosEnCarpetaEspecifica(carpeta.Name, podId);

                    if (archivosEncontrados.Any())
                    {
                        archivos.AddRange(archivosEncontrados);
                        _logger.LogInformation("📁 Encontrados {Count} archivos para POD_ID {PodId} en {Carpeta}",
                            archivosEncontrados.Count, podId, carpeta.Name);
                        break; // Detener al encontrar la primera coincidencia
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en búsqueda por POD_ID en carpetas recientes");
            }

            return archivos;
        }
        // NUEVO: Consultar BD para obtener información adicional del folio
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

        // NUEVO: Buscar usando patrones relacionados con POD_ID
        private async Task<List<SharePointFileInfo>> BuscarPorPatronesPodId(int podId)
        {
            var archivos = new List<SharePointFileInfo>();

            try
            {
                // Obtener todas las carpetas de fecha disponibles
                var carpetasRaiz = await _sharePointService.GetAllFolderContentsAsync();
                var carpetasFecha = carpetasRaiz
                    .Where(item => item.IsFolder && item.Name.StartsWith("2025-"))
                    .OrderByDescending(item => item.Name)
                    .Take(5); // Buscar en las 5 carpetas más recientes

                foreach (var carpeta in carpetasFecha)
                {
                    try
                    {
                        var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(carpeta.Name);

                        // Buscar archivos que puedan estar relacionados con el POD_ID
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
                            break; // Salir al encontrar la primera coincidencia
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

        // NUEVO: Buscar por folio directo (método original como fallback)
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

        // NUEVA CLASE: Para almacenar información adicional obtenida de BD
        public class InformacionAdicionalFolio
        {
            public int PodId { get; set; }
            public string? Folio { get; set; }
            public DateTime? FechaSalida { get; set; }
            public string? Cliente { get; set; }
            public string? Tractor { get; set; }
            public string? Remolque { get; set; }
        }
        private async Task BuscarEnBaseDatos(int id)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                // Consulta principal
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
                    OrigenDatos = "Base de Datos"; // NUEVO
                }

                reader.Close();

                // Si encontramos en BD, buscar evidencias
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
                        IsFromSharePoint = false // NUEVO
                    };

                    EvidenciasInfo.Add(evidencia);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar evidencias en BD");
            }
        }

        // NUEVO MÉTODO: Buscar en SharePoint
        private async Task BuscarEnSharePoint(string folio)
        {
            try
            {   
                BusquedaEnSharePoint = true;
                _logger.LogInformation("Buscando folio '{Folio}' en SharePoint", folio);

                // Usar tu servicio existente para buscar archivos
                var archivosSharePoint = await _sharePointService.GetAllFolderContentsAsync();

                // Filtrar archivos que contengan el folio en el nombre
                var archivosFiltrados = archivosSharePoint
                    .Where(archivo => archivo.Name.Contains(folio, StringComparison.OrdinalIgnoreCase) &&
                                     archivo.Type != "status" &&
                                     archivo.Type != "error")
                    .ToList();

                if (archivosFiltrados.Any())
                {
                    // Crear liquidación "virtual" con datos de SharePoint
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

                    // Convertir archivos de SharePoint a evidencias
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

        // NUEVO MÉTODO: Determinar si es imagen
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
    }

    // Clases de modelo (actualizadas)
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

        // NUEVAS PROPIEDADES PARA SHAREPOINT
        public bool IsFromSharePoint { get; set; } = false;
        public string? SharePointUrl { get; set; }
        public string? CarpetaSharePoint { get; set; }
    }
}