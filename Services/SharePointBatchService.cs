// Services/SharePointBatchService.cs - Servicio optimizado para subida masiva
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace ProyectoRH2025.Services
{
    public class SharePointBatchService : ISharePointBatchService
    {
        private readonly SharePointConfig _config;
        private readonly ILogger<SharePointBatchService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ClientSecretCredential _credential;

        // Configuración de reintentos
        private const int DEFAULT_MAX_RETRIES = 3;
        private readonly int[] RETRY_DELAYS_MS = { 2000, 4000, 8000 }; // Exponential backoff: 2s, 4s, 8s
        private const int THROTTLE_DELAY_MS = 30000; // 30 segundos para error 429

        public SharePointBatchService(
            IOptions<SharePointConfig> config,
            ILogger<SharePointBatchService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
        }

        public async Task<BatchUploadResult> UploadFilesInBatchAsync(
            List<FileUploadTask> uploadTasks,
            int maxConcurrency = 5,
            CancellationToken cancellationToken = default)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var results = new List<FileUploadResult>();

            _logger.LogInformation("📦 BATCH UPLOAD - Iniciando subida de {Count} archivos con concurrencia máxima: {Max}",
                uploadTasks.Count, maxConcurrency);

            // Semáforo para limitar concurrencia
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            // Crear tareas paralelas
            var uploadOperations = uploadTasks.Select(async task =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await UploadFileWithRetryAsync(
                        task.FolderPath,
                        task.FileName,
                        task.FileContent,
                        DEFAULT_MAX_RETRIES,
                        cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Ejecutar todas las subidas en paralelo
            results = (await Task.WhenAll(uploadOperations)).ToList();

            totalStopwatch.Stop();

            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess);

            _logger.LogInformation("📊 BATCH UPLOAD - Completado en {Elapsed}s. Éxitos: {Success}/{Total}, Fallas: {Failures}",
                totalStopwatch.Elapsed.TotalSeconds.ToString("F2"),
                successCount,
                results.Count,
                failureCount);

            return new BatchUploadResult
            {
                TotalFiles = uploadTasks.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                Results = results,
                TotalElapsedTime = totalStopwatch.Elapsed
            };
        }

        public async Task<FileUploadResult> UploadFileWithRetryAsync(
            string folderPath,
            string fileName,
            byte[] fileContent,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var attemptCount = 0;
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                attemptCount++;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogDebug("📤 Intento {Attempt}/{Max} - Subiendo: {File} ({Size} KB)",
                        attempt + 1, maxRetries + 1, fileName, fileContent.Length / 1024);

                    var success = await UploadFileInternalAsync(folderPath, fileName, fileContent, cancellationToken);

                    if (success)
                    {
                        stopwatch.Stop();
                        _logger.LogInformation("✅ ÉXITO - {File} subido en intento {Attempt} ({Time}s)",
                            fileName, attempt + 1, stopwatch.Elapsed.TotalSeconds.ToString("F2"));

                        return new FileUploadResult
                        {
                            IsSuccess = true,
                            FileName = fileName,
                            AttemptsCount = attemptCount,
                            ElapsedTime = stopwatch.Elapsed
                        };
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Error 429: Throttling de SharePoint
                    lastException = ex;
                    _logger.LogWarning("⚠️ THROTTLING (429) - {File}. Esperando {Delay}s antes de reintentar...",
                        fileName, THROTTLE_DELAY_MS / 1000);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(THROTTLE_DELAY_MS, cancellationToken);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    // Error 503: Servicio no disponible temporalmente
                    lastException = ex;
                    var delay = attempt < RETRY_DELAYS_MS.Length ? RETRY_DELAYS_MS[attempt] : RETRY_DELAYS_MS.Last();

                    _logger.LogWarning("⚠️ SERVICIO NO DISPONIBLE (503) - {File}. Reintentando en {Delay}s...",
                        fileName, delay / 1000);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    // Timeout de la petición
                    lastException = ex;
                    _logger.LogWarning("⏱️ TIMEOUT - {File} en intento {Attempt}. Reintentando...",
                        fileName, attempt + 1);

                    if (attempt < maxRetries)
                    {
                        var delay = attempt < RETRY_DELAYS_MS.Length ? RETRY_DELAYS_MS[attempt] : RETRY_DELAYS_MS.Last();
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    // Otros errores
                    lastException = ex;
                    _logger.LogError(ex, "❌ ERROR - {File} en intento {Attempt}: {Message}",
                        fileName, attempt + 1, ex.Message);

                    if (attempt < maxRetries)
                    {
                        var delay = attempt < RETRY_DELAYS_MS.Length ? RETRY_DELAYS_MS[attempt] : RETRY_DELAYS_MS.Last();
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            // Todos los intentos fallaron
            stopwatch.Stop();
            _logger.LogError("❌ FALLO FINAL - {File} después de {Attempts} intentos. Error: {Error}",
                fileName, attemptCount, lastException?.Message ?? "Desconocido");

            return new FileUploadResult
            {
                IsSuccess = false,
                FileName = fileName,
                ErrorMessage = lastException?.Message ?? "Error desconocido después de múltiples intentos",
                AttemptsCount = attemptCount,
                ElapsedTime = stopwatch.Elapsed
            };
        }

        private async Task<bool> UploadFileInternalAsync(
            string folderPath,
            string fileName,
            byte[] fileContent,
            CancellationToken cancellationToken)
        {
            // Obtener token de acceso
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }),
                cancellationToken);

            // Obtener Site ID
            var siteId = await GetSiteIdAsync(token.Token, cancellationToken);
            if (string.IsNullOrEmpty(siteId))
            {
                throw new InvalidOperationException("No se pudo obtener Site ID");
            }

            // Obtener Drive ID
            var driveId = await GetDocumentDriveIdAsync(siteId, token.Token, cancellationToken);
            if (string.IsNullOrEmpty(driveId))
            {
                throw new InvalidOperationException("No se pudo obtener Drive ID");
            }

            // Asegurar que las carpetas existan
            var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
            var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

            await EnsureFolderExistsAsync(siteId, driveId, fullPath, token.Token, cancellationToken);

            // Subir archivo
            var encodedPath = Uri.EscapeDataString($"{fullPath}/{fileName}");
            var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/content";

            var httpClient = _httpClientFactory.CreateClient("SharePointClient");
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var content = new ByteArrayContent(fileContent);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await httpClient.PutAsync(uploadUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // Manejar errores específicos
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException("Throttling detectado", null, HttpStatusCode.TooManyRequests);
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new HttpRequestException("Servicio no disponible", null, HttpStatusCode.ServiceUnavailable);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Error subiendo archivo. Status: {response.StatusCode}, Detalle: {errorContent}");
        }

        private async Task<string> GetSiteIdAsync(string accessToken, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("SharePointClient");
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var uri = new Uri(_config.SiteUrl);
            var url = $"https://graph.microsoft.com/v1.0/sites/{uri.Host}:{uri.AbsolutePath}";

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return result.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
        }

        private async Task<string> GetDocumentDriveIdAsync(string siteId, string accessToken, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("SharePointClient");
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives";
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            if (result.TryGetProperty("value", out var drives))
            {
                foreach (var drive in drives.EnumerateArray())
                {
                    if (drive.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString() ?? "";
                        if (name.Contains("Documents", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Documentos", StringComparison.OrdinalIgnoreCase))
                        {
                            return drive.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                        }
                    }
                }
            }

            return "";
        }

        private async Task EnsureFolderExistsAsync(
            string siteId,
            string driveId,
            string folderPath,
            string accessToken,
            CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("SharePointClient");
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var folders = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";

            foreach (var folderName in folders)
            {
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? folderName : $"{currentPath}/{folderName}";

                var encodedPath = Uri.EscapeDataString(currentPath);
                var checkUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}";

                var checkResponse = await httpClient.GetAsync(checkUrl, cancellationToken);

                if (!checkResponse.IsSuccessStatusCode)
                {
                    // Crear carpeta
                    var createUrl = string.IsNullOrEmpty(parentPath)
                        ? $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root/children"
                        : $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{Uri.EscapeDataString(parentPath)}:/children";

                    var createBody = JsonSerializer.Serialize(new
                    {
                        name = folderName,
                        folder = new { },
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "rename" }
                        }
                    });

                    var content = new StringContent(createBody, System.Text.Encoding.UTF8, "application/json");
                    var createResponse = await httpClient.PostAsync(createUrl, content, cancellationToken);

                    if (!createResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("⚠️ No se pudo crear carpeta {Folder}: {Error}", folderName, errorContent);
                    }
                }
            }
        }
    }
}
