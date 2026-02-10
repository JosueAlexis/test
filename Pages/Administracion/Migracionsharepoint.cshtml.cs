using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.Services;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ProyectoRH2025.Pages.Administracion
{
    public class MigracionSharePointModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MigracionSharePointModel> _logger;
        private readonly ISharePointTestService _sharePointService;
        private readonly MigracionSharePointService _migracionService;

        public MigracionSharePointModel(
            IConfiguration configuration,
            ILogger<MigracionSharePointModel> logger,
            ISharePointTestService sharePointService,
            MigracionSharePointService migracionService)
        {
            _configuration = configuration;
            _logger = logger;
            _sharePointService = sharePointService;
            _migracionService = migracionService;
        }

        public int TotalImagenes { get; set; }
        public int ImagenesPendientes { get; set; }
        public int ImagenesMigradas { get; set; }
        public decimal TamanoTotalMB { get; set; }
        public decimal TamanoMigradoMB { get; set; }
        public decimal TamanoPendienteMB { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var rolId = HttpContext.Session.GetInt32("idRol");
            if (rolId != 5 && rolId != 7) return RedirectToPage("/Login");
            await CargarEstadisticasAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostIniciarMigracionAsync(
            DateTime? FechaInicio,
            DateTime? FechaFin,
            int TamanoLote = 50,
            bool SoloPendientes = true,
            bool SobreescribirExistentes = false)
        {
            try
            {
                var usuario = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema";
                var migrationId = _migracionService.IniciarMigracion(
                    FechaInicio, FechaFin, TamanoLote, SoloPendientes, SobreescribirExistentes, usuario,
                    () => EjecutarMigracionAsync(FechaInicio, FechaFin, TamanoLote, SoloPendientes, SobreescribirExistentes, usuario));
                return new JsonResult(new { success = true, migrationId = migrationId.ToString(), mensaje = "Migración iniciada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error iniciando migración");
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        public IActionResult OnGetObtenerProgreso(string? migrationId)
        {
            if (string.IsNullOrEmpty(migrationId) || !Guid.TryParse(migrationId, out var id))
            {
                return new JsonResult(new { success = false, error = "ID de migración inválido" });
            }
            var progreso = _migracionService.ObtenerProgreso(id);
            if (progreso == null)
            {
                return new JsonResult(new { success = false, completado = true, mensaje = "Migración no encontrada" });
            }
            var tiempo = DateTime.Now - progreso.Inicio;
            var velocidad = tiempo.TotalMinutes > 0 ? (int)(progreso.Procesadas / tiempo.TotalMinutes) : 0;
            return new JsonResult(new
            {
                success = true,
                porcentaje = progreso.Porcentaje,
                procesadas = progreso.Procesadas,
                exitosas = progreso.Exitosas,
                fallidas = progreso.Fallidas,
                velocidad,
                completado = progreso.Completado,
                logs = progreso.Logs.TakeLast(50).ToList()
            });
        }

        public async Task<IActionResult> OnGetActualizarEstadisticasAsync()
        {
            await CargarEstadisticasAsync();
            return new JsonResult(new
            {
                total = TotalImagenes,
                pendientes = ImagenesPendientes,
                migradas = ImagenesMigradas,
                tamanoMB = TamanoTotalMB,
                tamanoMigradoMB = TamanoMigradoMB,
                tamanoPendienteMB = TamanoPendienteMB
            });
        }

        public async Task<IActionResult> OnGetVerificarConexionAsync()
        {
            try
            {
                var resultado = await _sharePointService.TestConnectionAsync();
                return new JsonResult(new { success = resultado.IsSuccess, mensaje = resultado.Message, error = resultado.Error });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando conexión SharePoint");
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        private async Task CargarEstadisticasAsync()
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SP_ObtenerEstadisticasMigracion", conn) { CommandType = CommandType.StoredProcedure };
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    TotalImagenes = reader.GetInt32("TotalImagenes");
                    ImagenesMigradas = reader.GetInt32("ImagenesMigradas");
                    ImagenesPendientes = reader.GetInt32("ImagenesPendientes");
                    TamanoTotalMB = Math.Round(reader.GetDecimal("TamanoTotalMB"), 2);
                    TamanoMigradoMB = Math.Round(reader.GetDecimal("TamanoMigradoMB"), 2);
                    TamanoPendienteMB = Math.Round(reader.GetDecimal("TamanoPendienteMB"), 2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando estadísticas");
            }
        }

        private async Task EjecutarMigracionAsync(
            DateTime? fechaInicio,
            DateTime? fechaFin,
            int tamanoLote,
            bool soloPendientes,
            bool sobreescribir,
            string usuario)
        {
            var sw = Stopwatch.StartNew();
            int procesadas = 0;
            int exitosas = 0;
            int fallidas = 0;

            try
            {
                // Obtener el progreso actual
                var progreso = _migracionService.ObtenerProgreso(_migracionService.MigrationIdActual ?? Guid.Empty);

                // Calcular total a procesar
                var totalImagenes = await ObtenerTotalImagenesAsync(fechaInicio, fechaFin, soloPendientes);

                if (progreso != null)
                {
                    progreso.TotalProcesar = totalImagenes;
                    progreso.AgregarLog("info", $"📊 Total de imágenes a procesar: {totalImagenes:N0}");
                }

                while (true)
                {
                    var lote = await ObtenerLoteImagenesAsync(fechaInicio, fechaFin, soloPendientes, procesadas, tamanoLote);
                    if (lote.Count == 0) break;

                    if (progreso != null)
                    {
                        progreso.AgregarLog("info", $"📦 Procesando lote de {lote.Count} imágenes (offset: {procesadas})");
                    }

                    foreach (var imagen in lote)
                    {
                        try
                        {
                            var carpetaDestino = DeterminarCarpetaSharePoint(imagen);
                            var nombreArchivoSeguro = SanitizarNombreArchivo(imagen.FileName);

                            bool subidoExitoso = sobreescribir || !imagen.YaMigrada
                                ? await _sharePointService.UploadFileAsync(carpetaDestino, nombreArchivoSeguro, imagen.ImageData)
                                : true;

                            if (subidoExitoso)
                            {
                                await MarcarComoMigradaAsync(imagen.EvidenciaID, usuario);
                                exitosas++;

                                // Log cada 10 exitosas
                                if (exitosas % 10 == 0 && progreso != null)
                                {
                                    progreso.AgregarLog("success", $"✅ {exitosas} imágenes migradas exitosamente");
                                }
                            }
                            else
                            {
                                fallidas++;
                                if (progreso != null)
                                {
                                    progreso.AgregarLog("warning", $"⚠️ Fallo al subir imagen {imagen.EvidenciaID}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error procesando imagen {EvidenciaID}", imagen.EvidenciaID);
                            fallidas++;

                            if (progreso != null)
                            {
                                progreso.AgregarLog("error", $"❌ Error en imagen {imagen.EvidenciaID}: {ex.Message}");
                            }
                        }

                        procesadas++;

                        // Actualizar contadores en progreso
                        if (progreso != null)
                        {
                            progreso.Procesadas = procesadas;
                            progreso.Exitosas = exitosas;
                            progreso.Fallidas = fallidas;
                        }
                    }

                    await Task.Delay(400);
                }

                sw.Stop();

                if (progreso != null)
                {
                    progreso.AgregarLog("success", $"🎉 Migración completada: {exitosas} exitosas, {fallidas} fallidas en {sw.Elapsed.TotalMinutes:F2} minutos");
                }

                await RegistrarLogEnBDAsync(
                    usuario,
                    "MIGRACION",
                    procesadas,
                    exitosas,
                    fallidas,
                    sw.Elapsed.TotalMinutes,
                    fechaInicio,
                    fechaFin,
                    tamanoLote,
                    $"Migración completada - {exitosas} exitosas, {fallidas} fallidas"
                );
            }
            catch (Exception ex)
            {
                sw.Stop();

                var progreso = _migracionService.ObtenerProgreso(_migracionService.MigrationIdActual ?? Guid.Empty);

                if (progreso != null)
                {
                    progreso.AgregarLog("error", $"💥 Error crítico: {ex.Message}");
                }

                await RegistrarLogEnBDAsync(
                    usuario,
                    "MIGRACION_ERROR",
                    procesadas,
                    exitosas,
                    fallidas,
                    sw.Elapsed.TotalMinutes,
                    fechaInicio,
                    fechaFin,
                    tamanoLote,
                    ex.Message
                );
            }
        }

        private async Task<int> ObtenerTotalImagenesAsync(
            DateTime? fechaInicio,
            DateTime? fechaFin,
            bool soloPendientes)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var query = @"
                SELECT COUNT(*)
                FROM POD_Evidencias_Imagenes e
                INNER JOIN POD_Records p ON e.POD_ID_FK = p.POD_ID
                WHERE e.ImageData IS NOT NULL
                  AND DATALENGTH(e.ImageData) > 0";

            if (soloPendientes)
                query += " AND (e.MigradaSharePoint = 0 OR e.MigradaSharePoint IS NULL)";

            if (fechaInicio.HasValue)
                query += " AND e.CaptureDate >= @FechaInicio";

            if (fechaFin.HasValue)
                query += " AND e.CaptureDate < @FechaFin";

            using var cmd = new SqlCommand(query, conn);

            if (fechaInicio.HasValue)
                cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio.Value);

            if (fechaFin.HasValue)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin.Value.AddDays(1));

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task<List<ImagenParaMigrar>> ObtenerLoteImagenesAsync(
            DateTime? fechaInicio,
            DateTime? fechaFin,
            bool soloPendientes,
            int offset,
            int take)
        {
            var imagenes = new List<ImagenParaMigrar>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            var query = @"
                SELECT
                    e.EvidenciaID,
                    e.POD_ID_FK,
                    e.FileName,
                    e.ImageData,
                    e.MimeType,
                    e.CaptureDate,
                    ISNULL(e.MigradaSharePoint, 0) AS YaMigrada,
                    p.Folio,
                    p.FechaSalida
                FROM POD_Evidencias_Imagenes e
                INNER JOIN POD_Records p ON e.POD_ID_FK = p.POD_ID
                WHERE e.ImageData IS NOT NULL
                  AND DATALENGTH(e.ImageData) > 0";
            if (soloPendientes)
                query += " AND (e.MigradaSharePoint = 0 OR e.MigradaSharePoint IS NULL)";
            if (fechaInicio.HasValue)
                query += " AND e.CaptureDate >= @FechaInicio";
            if (fechaFin.HasValue)
                query += " AND e.CaptureDate < @FechaFin";
            query += @"
                ORDER BY e.CaptureDate
                OFFSET @Offset ROWS
                FETCH NEXT @Take ROWS ONLY";
            using var cmd = new SqlCommand(query, conn);
            if (fechaInicio.HasValue)
                cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio.Value);
            if (fechaFin.HasValue)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin.Value.AddDays(1));
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@Take", take);
            using var reader = await cmd.ExecuteReaderAsync();
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
                    YaMigrada = reader.GetBoolean("YaMigrada")
                });
            }
            return imagenes;
        }

        private string DeterminarCarpetaSharePoint(ImagenParaMigrar imagen)
        {
            DateTime fechaBase = imagen.FechaSalida ?? imagen.CaptureDate ?? DateTime.Today;
            var carpetaFecha = fechaBase.AddDays(1).ToString("yyyy-MM-dd");
            var carpetaPod = $"POD_{imagen.PodIdFK}";
            return $"{carpetaFecha}/{carpetaPod}";
        }

        private string SanitizarNombreArchivo(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "sin_nombre.jpg";
            var invalido = new[] { '#', '%', '&', '*', ':', '<', '>', '?', '/', '\\', '{', '}', '|', '"', '\'' };
            var limpio = string.Concat(nombre.Where(c => !invalido.Contains(c)));
            if (limpio.Length > 200) limpio = limpio.Substring(0, 200);
            var extension = Path.GetExtension(limpio).ToLower();
            if (string.IsNullOrEmpty(extension) || !Regex.IsMatch(extension, @"\.(jpg|jpeg|png|gif|bmp|pdf)$"))
                limpio += ".jpg";
            return limpio;
        }

        private async Task MarcarComoMigradaAsync(int evidenciaId, string usuario)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            var query = @"
                UPDATE POD_Evidencias_Imagenes
                SET MigradaSharePoint = 1,
                    FechaMigracionSharePoint = GETDATE(),
                    UsuarioMigracion = @Usuario
                WHERE EvidenciaID = @EvidenciaID";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@EvidenciaID", evidenciaId);
            cmd.Parameters.AddWithValue("@Usuario", usuario ?? "Sistema");
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RegistrarLogEnBDAsync(
            string usuario,
            string tipoOperacion,
            int imagenesAfectadas,
            int exitosas,
            int fallidas,
            double tiempoMinutos,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            int tamanoLote,
            string observaciones)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SP_RegistrarLogMigracion", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@Usuario", usuario ?? "Sistema");
                cmd.Parameters.AddWithValue("@TipoOperacion", tipoOperacion);
                cmd.Parameters.AddWithValue("@ImagenesAfectadas", imagenesAfectadas);
                cmd.Parameters.AddWithValue("@ImagenesExitosas", exitosas);
                cmd.Parameters.AddWithValue("@ImagenesFallidas", fallidas);
                cmd.Parameters.AddWithValue("@TiempoMinutos", Math.Round(tiempoMinutos, 2));
                cmd.Parameters.AddWithValue("@FechaInicio", (object?)fechaInicio ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FechaFin", (object?)fechaFin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TamanoLote", tamanoLote);
                cmd.Parameters.AddWithValue("@Observaciones", observaciones ?? "");
                cmd.Parameters.AddWithValue("@DetallesError", DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando log de migración en BD");
            }
        }

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
    }
}