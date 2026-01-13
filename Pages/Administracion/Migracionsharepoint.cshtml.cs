using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.Services;
using System.Data;
using System.Diagnostics;

namespace ProyectoRH2025.Pages.Administracion
{
    public class MigracionSharePointModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MigracionSharePointModel> _logger;
        private readonly ISharePointTestService _sharePointService;

        // Diccionario estático para mantener el progreso entre peticiones
        private static ProgresoMigracion? _progresoActual;
        private static readonly object _lockProgreso = new object();

        public MigracionSharePointModel(
            IConfiguration configuration,
            ILogger<MigracionSharePointModel> logger,
            ISharePointTestService sharePointService)
        {
            _configuration = configuration;
            _logger = logger;
            _sharePointService = sharePointService;
        }

        // PROPIEDADES PÚBLICAS
        public int TotalImagenes { get; set; }
        public int ImagenesPendientes { get; set; }
        public int ImagenesMigradas { get; set; }
        public decimal TamanoTotalMB { get; set; }
        public string? MensajeExito { get; set; }
        public string? MensajeError { get; set; }

        // PROPIEDADES DE FILTROS
        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; }

        // ========================================================================
        // ON GET - CARGAR PÁGINA
        // ========================================================================
        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar permisos (solo Administradores o IT)
            var rolId = HttpContext.Session.GetInt32("idRol");
            if (rolId != 5 && rolId != 7) // Solo Admin(5) o IT(7)
            {
                return RedirectToPage("/Login");
            }

            await CargarEstadisticas();

            return Page();
        }

        // ========================================================================
        // HANDLER: INICIAR MIGRACIÓN
        // ========================================================================
        public async Task<IActionResult> OnPostIniciarMigracionAsync(
            DateTime? FechaInicio,
            DateTime? FechaFin,
            int TamanoLote = 50,
            bool SoloPendientes = true,
            bool SobreescribirExistentes = false)
        {
            try
            {
                _logger.LogInformation("=== INICIANDO MIGRACIÓN ===");
                _logger.LogInformation("Filtros: FechaInicio={FechaInicio}, FechaFin={FechaFin}, Lote={Lote}, SoloPendientes={Pendientes}",
                    FechaInicio, FechaFin, TamanoLote, SoloPendientes);

                // Inicializar progreso
                lock (_lockProgreso)
                {
                    _progresoActual = new ProgresoMigracion
                    {
                        TotalProcesar = 0,
                        Procesadas = 0,
                        Exitosas = 0,
                        Fallidas = 0,
                        Completado = false,
                        InicioMigracion = DateTime.Now
                    };
                }

                // Ejecutar migración en tarea en segundo plano
                _ = Task.Run(async () =>
                {
                    await EjecutarMigracionAsync(FechaInicio, FechaFin, TamanoLote, SoloPendientes, SobreescribirExistentes);
                });

                return new JsonResult(new
                {
                    success = true,
                    mensaje = $"Migración iniciada con lotes de {TamanoLote} imágenes"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error iniciando migración");
                return new JsonResult(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // ========================================================================
        // HANDLER: OBTENER PROGRESO
        // ========================================================================
        public IActionResult OnGetObtenerProgreso()
        {
            lock (_lockProgreso)
            {
                if (_progresoActual == null)
                {
                    return new JsonResult(new
                    {
                        porcentaje = 0,
                        procesadas = 0,
                        exitosas = 0,
                        fallidas = 0,
                        velocidad = 0,
                        completado = false
                    });
                }

                // Calcular velocidad (imágenes por minuto)
                var tiempoTranscurrido = DateTime.Now - _progresoActual.InicioMigracion;
                var velocidad = tiempoTranscurrido.TotalMinutes > 0
                    ? (int)(_progresoActual.Procesadas / tiempoTranscurrido.TotalMinutes)
                    : 0;

                return new JsonResult(new
                {
                    porcentaje = _progresoActual.Porcentaje,
                    procesadas = _progresoActual.Procesadas,
                    exitosas = _progresoActual.Exitosas,
                    fallidas = _progresoActual.Fallidas,
                    velocidad,
                    completado = _progresoActual.Completado,
                    ultimoLog = _progresoActual.UltimoLog
                });
            }
        }

        // ========================================================================
        // HANDLER: ACTUALIZAR ESTADÍSTICAS
        // ========================================================================
        public async Task<IActionResult> OnGetActualizarEstadisticasAsync()
        {
            try
            {
                await CargarEstadisticas();

                return new JsonResult(new
                {
                    total = TotalImagenes,
                    pendientes = ImagenesPendientes,
                    migradas = ImagenesMigradas,
                    tamanoMB = TamanoTotalMB
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando estadísticas");
                return new JsonResult(new { error = ex.Message });
            }
        }

        // ========================================================================
        // HANDLER: VERIFICAR CONEXIÓN SHAREPOINT
        // ========================================================================
        public async Task<IActionResult> OnGetVerificarConexionAsync()
        {
            try
            {
                var resultado = await _sharePointService.TestConnectionAsync();

                return new JsonResult(new
                {
                    success = resultado.IsSuccess,
                    mensaje = resultado.Message,
                    error = resultado.Error
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando conexión");
                return new JsonResult(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // ========================================================================
        // MÉTODO PRIVADO: CARGAR ESTADÍSTICAS
        // ========================================================================
        private async Task CargarEstadisticas()
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        COUNT(*) as Total,
                        SUM(CASE WHEN MigradaSharePoint = 0 OR MigradaSharePoint IS NULL THEN 1 ELSE 0 END) as Pendientes,
                        SUM(CASE WHEN MigradaSharePoint = 1 THEN 1 ELSE 0 END) as Migradas,
                        ISNULL(SUM(DATALENGTH(ImageData)) / 1024.0 / 1024.0, 0) as TamanoMB
                    FROM POD_Evidencias_Imagenes
                    WHERE ImageData IS NOT NULL";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    TotalImagenes = reader.GetInt32("Total");
                    ImagenesPendientes = reader.GetInt32("Pendientes");
                    ImagenesMigradas = reader.GetInt32("Migradas");
                    TamanoTotalMB = Math.Round(reader.GetDecimal("TamanoMB"), 2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando estadísticas");
                throw;
            }
        }

        // ========================================================================
        // MÉTODO PRIVADO: EJECUTAR MIGRACIÓN
        // ========================================================================
        private async Task EjecutarMigracionAsync(
            DateTime? fechaInicio,
            DateTime? fechaFin,
            int tamanoLote,
            bool soloPendientes,
            bool sobreescribirExistentes)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("🚀 EJECUCIÓN DE MIGRACIÓN INICIADA");

                // 1. OBTENER IMÁGENES A MIGRAR
                var imagenes = await ObtenerImagenesParaMigrar(fechaInicio, fechaFin, soloPendientes);

                lock (_lockProgreso)
                {
                    _progresoActual!.TotalProcesar = imagenes.Count;
                    _progresoActual.AgregarLog("info", $"Total de imágenes a procesar: {imagenes.Count}");
                }

                _logger.LogInformation("📊 Total imágenes a migrar: {Count}", imagenes.Count);

                if (imagenes.Count == 0)
                {
                    lock (_lockProgreso)
                    {
                        _progresoActual!.Completado = true;
                        _progresoActual.AgregarLog("warning", "No hay imágenes para migrar");
                    }
                    return;
                }

                // 2. PROCESAR POR LOTES
                var totalLotes = (int)Math.Ceiling(imagenes.Count / (double)tamanoLote);
                _logger.LogInformation("📦 Total de lotes: {Lotes} (tamaño: {Tamaño})", totalLotes, tamanoLote);

                for (int i = 0; i < totalLotes; i++)
                {
                    var lote = imagenes.Skip(i * tamanoLote).Take(tamanoLote).ToList();

                    _logger.LogInformation("📦 Procesando lote {Actual}/{Total} ({Count} imágenes)",
                        i + 1, totalLotes, lote.Count);

                    lock (_lockProgreso)
                    {
                        _progresoActual!.AgregarLog("info",
                            $"Procesando lote {i + 1}/{totalLotes} ({lote.Count} imágenes)");
                    }

                    await ProcesarLoteAsync(lote, sobreescribirExistentes);

                    // Pequeña pausa entre lotes para no saturar SharePoint
                    await Task.Delay(500);
                }

                // 3. FINALIZAR
                stopwatch.Stop();

                lock (_lockProgreso)
                {
                    _progresoActual!.Completado = true;
                    _progresoActual.AgregarLog("success",
                        $"Migración completada en {stopwatch.Elapsed.TotalMinutes:F1} minutos. " +
                        $"Exitosas: {_progresoActual.Exitosas}, Fallidas: {_progresoActual.Fallidas}");
                }

                _logger.LogInformation("✅ MIGRACIÓN COMPLETADA - Tiempo: {Tiempo}min, Exitosas: {Exitosas}, Fallidas: {Fallidas}",
                    stopwatch.Elapsed.TotalMinutes, _progresoActual.Exitosas, _progresoActual.Fallidas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR EN MIGRACIÓN");

                lock (_lockProgreso)
                {
                    _progresoActual!.Completado = true;
                    _progresoActual.AgregarLog("error", $"Error crítico: {ex.Message}");
                }
            }
        }

        // ========================================================================
        // MÉTODO PRIVADO: OBTENER IMÁGENES PARA MIGRAR
        // ========================================================================
        private async Task<List<ImagenParaMigrar>> ObtenerImagenesParaMigrar(
            DateTime? fechaInicio,
            DateTime? fechaFin,
            bool soloPendientes)
        {
            var imagenes = new List<ImagenParaMigrar>();

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        e.EvidenciaID,
                        e.POD_ID_FK,
                        e.FileName,
                        e.ImageData,
                        e.MimeType,
                        e.CaptureDate,
                        e.MigradaSharePoint,
                        p.Folio,
                        p.FechaSalida
                    FROM POD_Evidencias_Imagenes e
                    INNER JOIN POD_Records p ON e.POD_ID_FK = p.POD_ID
                    WHERE e.ImageData IS NOT NULL
                        AND DATALENGTH(e.ImageData) > 0";

                // Agregar filtro de pendientes
                if (soloPendientes)
                {
                    query += " AND (e.MigradaSharePoint = 0 OR e.MigradaSharePoint IS NULL)";
                }

                // Agregar filtros de fecha
                if (fechaInicio.HasValue)
                {
                    query += " AND e.CaptureDate >= @FechaInicio";
                }

                if (fechaFin.HasValue)
                {
                    query += " AND e.CaptureDate <= @FechaFin";
                }

                query += " ORDER BY e.CaptureDate DESC";

                using var command = new SqlCommand(query, connection);

                if (fechaInicio.HasValue)
                    command.Parameters.AddWithValue("@FechaInicio", fechaInicio.Value);

                if (fechaFin.HasValue)
                    command.Parameters.AddWithValue("@FechaFin", fechaFin.Value.AddDays(1).AddSeconds(-1));

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    imagenes.Add(new ImagenParaMigrar
                    {
                        EvidenciaID = reader.GetInt32("EvidenciaID"),
                        PodIdFK = reader.GetInt32("POD_ID_FK"),
                        FileName = reader["FileName"]?.ToString() ?? $"Evidencia_{reader.GetInt32("EvidenciaID")}.jpg",
                        ImageData = (byte[])reader["ImageData"],
                        MimeType = reader["MimeType"]?.ToString(),
                        CaptureDate = reader["CaptureDate"] as DateTime?,
                        Folio = reader["Folio"]?.ToString(),
                        FechaSalida = reader["FechaSalida"] as DateTime?,
                        YaMigrada = reader["MigradaSharePoint"] as bool? ?? false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo imágenes para migrar");
                throw;
            }

            return imagenes;
        }

        // ========================================================================
        // MÉTODO PRIVADO: PROCESAR LOTE
        // ========================================================================
        private async Task ProcesarLoteAsync(List<ImagenParaMigrar> lote, bool sobreescribir)
        {
            foreach (var imagen in lote)
            {
                try
                {
                    // Determinar carpeta de destino en SharePoint
                    var carpetaDestino = DeterminarCarpetaSharePoint(imagen);

                    _logger.LogInformation("📤 Subiendo: {FileName} a {Carpeta}",
                        imagen.FileName, carpetaDestino);

                    // Subir a SharePoint
                    bool exitoso;

                    if (sobreescribir || !imagen.YaMigrada)
                    {
                        exitoso = await _sharePointService.UploadFileAsync(
                            carpetaDestino,
                            imagen.FileName,
                            imagen.ImageData);
                    }
                    else
                    {
                        _logger.LogInformation("⏭️ Saltando {FileName} (ya migrada)", imagen.FileName);
                        exitoso = true;
                    }

                    if (exitoso)
                    {
                        // Marcar como migrada en BD
                        await MarcarComoMigradaAsync(imagen.EvidenciaID);

                        lock (_lockProgreso)
                        {
                            _progresoActual!.Exitosas++;
                            _progresoActual.Procesadas++;
                        }

                        _logger.LogInformation("✅ Migrada: {FileName}", imagen.FileName);
                    }
                    else
                    {
                        lock (_lockProgreso)
                        {
                            _progresoActual!.Fallidas++;
                            _progresoActual!.Procesadas++;
                            _progresoActual.AgregarLog("error", $"Error subiendo {imagen.FileName}");
                        }

                        _logger.LogWarning("❌ Fallo: {FileName}", imagen.FileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando imagen {Id}", imagen.EvidenciaID);

                    lock (_lockProgreso)
                    {
                        _progresoActual!.Fallidas++;
                        _progresoActual!.Procesadas++;
                        _progresoActual.AgregarLog("error", $"Excepción en {imagen.FileName}: {ex.Message}");
                    }
                }
            }
        }

        // ========================================================================
        // MÉTODO PRIVADO: DETERMINAR CARPETA SHAREPOINT
        // ========================================================================
        private string DeterminarCarpetaSharePoint(ImagenParaMigrar imagen)
        {
            // Lógica:
            // - Si tiene FechaSalida: usar (FechaSalida + 1 día) / POD_{PodId}
            // - Si no: usar (CaptureDate) / POD_{PodId}

            DateTime fechaBase;

            if (imagen.FechaSalida.HasValue)
            {
                fechaBase = imagen.FechaSalida.Value.AddDays(1);
            }
            else if (imagen.CaptureDate.HasValue)
            {
                fechaBase = imagen.CaptureDate.Value;
            }
            else
            {
                fechaBase = DateTime.Today;
            }

            var carpetaFecha = fechaBase.ToString("yyyy-MM-dd");
            var carpetaPod = $"POD_{imagen.PodIdFK}";

            return $"{carpetaFecha}/{carpetaPod}";
        }

        // ========================================================================
        // MÉTODO PRIVADO: MARCAR COMO MIGRADA
        // ========================================================================
        private async Task MarcarComoMigradaAsync(int evidenciaId)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    UPDATE POD_Evidencias_Imagenes
                    SET MigradaSharePoint = 1,
                        FechaMigracionSharePoint = GETDATE(),
                        UsuarioMigracion = @Usuario
                    WHERE EvidenciaID = @EvidenciaID";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@EvidenciaID", evidenciaId);
                command.Parameters.AddWithValue("@Usuario",
                    HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema");

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marcando evidencia {Id} como migrada", evidenciaId);
            }
        }

        // ========================================================================
        // CLASES AUXILIARES
        // ========================================================================
        private class ImagenParaMigrar
        {
            public int EvidenciaID { get; set; }
            public int PodIdFK { get; set; }
            public string FileName { get; set; } = "";
            public byte[] ImageData { get; set; } = Array.Empty<byte>();
            public string? MimeType { get; set; }
            public DateTime? CaptureDate { get; set; }
            public string? Folio { get; set; }
            public DateTime? FechaSalida { get; set; }
            public bool YaMigrada { get; set; }
        }

        private class ProgresoMigracion
        {
            public int TotalProcesar { get; set; }
            public int Procesadas { get; set; }
            public int Exitosas { get; set; }
            public int Fallidas { get; set; }
            public bool Completado { get; set; }
            public DateTime InicioMigracion { get; set; }
            public LogEntry? UltimoLog { get; set; }

            public int Porcentaje =>
                TotalProcesar > 0 ? (int)((Procesadas / (double)TotalProcesar) * 100) : 0;

            public void AgregarLog(string tipo, string mensaje)
            {
                UltimoLog = new LogEntry
                {
                    Tipo = tipo,
                    Mensaje = mensaje,
                    Timestamp = DateTime.Now
                };
            }
        }

        private class LogEntry
        {
            public string Tipo { get; set; } = "info";
            public string Mensaje { get; set; } = "";
            public DateTime Timestamp { get; set; }
        }
    }
}