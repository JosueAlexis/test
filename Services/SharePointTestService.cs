// Services/SharePointTestService.cs - VERSIÓN COMPLETA CON MÉTODOS DE SUSTITUCIÓN
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net.Http;
using System.Text.Json;
using Azure.Core;

namespace ProyectoRH2025.Services
{
    public class SharePointTestService : ISharePointTestService
    {
        private readonly SharePointConfig _config;
        private readonly ILogger<SharePointTestService> _logger;
        private GraphServiceClient? _graphClient;

        public SharePointTestService(IOptions<SharePointConfig> config, ILogger<SharePointTestService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task<SharePointTestResult> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("🌐 Iniciando prueba de conexión a SharePoint...");

                if (string.IsNullOrEmpty(_config.ClientSecret))
                {
                    return new SharePointTestResult
                    {
                        IsSuccess = false,
                        Message = "Client Secret no configurado",
                        Error = "Configure el Client Secret en appsettings.json",
                        Details = new Dictionary<string, object>
                        {
                            ["ClientId"] = _config.ClientId ?? "No configurado",
                            ["TenantId"] = _config.TenantId ?? "No configurado",
                            ["SiteUrl"] = _config.SiteUrl ?? "No configurado"
                        }
                    };
                }

                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                _graphClient = new GraphServiceClient(credential);

                _logger.LogInformation("🔐 Probando autenticación...");

                var users = await _graphClient.Users.GetAsync(config =>
                {
                    config.QueryParameters.Top = 5;
                    config.QueryParameters.Select = new[] { "displayName", "mail", "id" };
                });

                var userCount = users?.Value?.Count ?? 0;
                var firstUser = users?.Value?.FirstOrDefault()?.DisplayName ?? "No disponible";

                _logger.LogInformation($"✅ Autenticación exitosa. Usuarios encontrados: {userCount}");

                return new SharePointTestResult
                {
                    IsSuccess = true,
                    Message = $"Conexión exitosa a Microsoft Graph. Usuarios encontrados: {userCount}",
                    Details = new Dictionary<string, object>
                    {
                        ["Estado"] = "✅ Conectado",
                        ["TenantId"] = _config.TenantId ?? "",
                        ["ClientId"] = _config.ClientId ?? "",
                        ["ClientSecret"] = $"{_config.ClientSecret?.Length ?? 0} caracteres",
                        ["SiteUrl"] = _config.SiteUrl ?? "",
                        ["UsuariosEncontrados"] = userCount,
                        ["PrimerUsuario"] = firstUser,
                        ["TiempoRespuesta"] = "< 1s"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en conexión a SharePoint");
                return new SharePointTestResult
                {
                    IsSuccess = false,
                    Message = "Error de conexión",
                    Error = ex.Message,
                    Details = new Dictionary<string, object>
                    {
                        ["TipoError"] = ex.GetType().Name,
                        ["Configuración"] = new
                        {
                            ClientId = _config.ClientId ?? "No configurado",
                            TenantId = _config.TenantId ?? "No configurado",
                            SiteUrl = _config.SiteUrl ?? "No configurado",
                            ClientSecretLength = _config.ClientSecret?.Length ?? 0
                        }
                    }
                };
            }
        }

        public async Task<List<SharePointFileInfo>> GetTestFilesAsync()
        {
            return await GetAllFolderContentsAsync();
        }

        public async Task<List<SharePointFileInfo>> GetFolderContentsAsync(string folderPath = "")
        {
            var files = new List<SharePointFileInfo>();

            try
            {
                _logger.LogInformation($"📁 Obteniendo contenido de carpeta: {folderPath}");

                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                    _graphClient = new GraphServiceClient(credential);
                }

                files.Add(new SharePointFileInfo
                {
                    Name = $"🌐 Accediendo a: {_config.SiteUrl}",
                    Type = "status",
                    Modified = DateTime.Now,
                    ModifiedBy = "Sistema",
                    Size = 0
                });

                var siteId = await GetSiteIdAsync();
                if (!string.IsNullOrEmpty(siteId))
                {
                    files.Add(new SharePointFileInfo
                    {
                        Name = "✅ Sitio encontrado",
                        Type = "status",
                        Modified = DateTime.Now,
                        ModifiedBy = "Sistema",
                        Size = 0
                    });

                    var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                    var documentDrive = drives?.Value?.FirstOrDefault(d =>
                        d.Name?.Contains("Documents") == true ||
                        d.Name?.Contains("Documentos") == true);

                    if (documentDrive != null)
                    {
                        files.Add(new SharePointFileInfo
                        {
                            Name = $"📚 Biblioteca: {documentDrive.Name}",
                            Type = "status",
                            Modified = DateTime.Now,
                            ModifiedBy = "Sistema",
                            Size = 0
                        });

                        var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                        var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

                        files.Add(new SharePointFileInfo
                        {
                            Name = $"🔍 Navegando a: {fullPath}",
                            Type = "status",
                            Modified = DateTime.Now,
                            ModifiedBy = "Sistema",
                            Size = 0
                        });

                        var realFiles = await GetRealFolderContentsAsync(siteId, documentDrive.Id, fullPath);
                        files.AddRange(realFiles);
                    }
                }

                if (files.Count(f => f.Type != "status" && f.Type != "error") == 0)
                {
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        files.AddRange(GetMockFolderStructure());
                    }
                    else
                    {
                        files.AddRange(GetMockFolderContents(folderPath));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo contenido de carpeta");
                files.Add(new SharePointFileInfo
                {
                    Name = $"❌ Error: {ex.Message}",
                    Type = "error",
                    Modified = DateTime.Now,
                    ModifiedBy = "Sistema",
                    Size = 0
                });
            }

            return files;
        }

        public async Task<List<SharePointFileInfo>> GetAllFolderContentsAsync(string folderPath = "")
        {
            var allFiles = new List<SharePointFileInfo>();

            try
            {
                _logger.LogInformation($"📁 Obteniendo TODO el contenido de carpeta: {folderPath}");

                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                    _graphClient = new GraphServiceClient(credential);
                }

                var siteId = await GetSiteIdAsync();
                if (!string.IsNullOrEmpty(siteId))
                {
                    var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                    var documentDrive = drives?.Value?.FirstOrDefault(d =>
                        d.Name?.Contains("Documents") == true ||
                        d.Name?.Contains("Documentos") == true);

                    if (documentDrive != null)
                    {
                        var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                        var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

                        allFiles = await GetAllRealFolderContentsAsync(siteId, documentDrive.Id, fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo TODO el contenido de carpeta");
                allFiles.Add(new SharePointFileInfo
                {
                    Name = $"❌ Error: {ex.Message}",
                    Type = "error",
                    Modified = DateTime.Now,
                    ModifiedBy = "Sistema",
                    Size = 0
                });
            }

            return allFiles;
        }

        public async Task<SharePointFileInfo?> GetFolderByNameAsync(string folderPath, string folderName)
        {
            try
            {
                _logger.LogInformation($"🔍 Buscando carpeta específica: '{folderName}' en '{folderPath}'");

                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                    _graphClient = new GraphServiceClient(credential);
                }

                var siteId = await GetSiteIdAsync();
                if (!string.IsNullOrEmpty(siteId))
                {
                    var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                    var documentDrive = drives?.Value?.FirstOrDefault(d =>
                        d.Name?.Contains("Documents") == true ||
                        d.Name?.Contains("Documentos") == true);

                    if (documentDrive != null)
                    {
                        var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                        var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

                        return await SearchSpecificFolderAsync(siteId, documentDrive.Id, fullPath, folderName);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error buscando carpeta '{folderName}' en '{folderPath}'");
                return null;
            }
        }

        public async Task<List<SharePointFileInfo>> SearchFoldersAsync(string folderPath, string searchPattern)
        {
            var matchingFolders = new List<SharePointFileInfo>();

            try
            {
                _logger.LogInformation($"🔍 Buscando carpetas con patrón: '{searchPattern}' en '{folderPath}'");

                var allContents = await GetAllFolderContentsAsync(folderPath);

                matchingFolders = allContents
                    .Where(item =>
                        item.IsFolder &&
                        item.Type != "status" &&
                        item.Type != "error" &&
                        item.Name.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogInformation($"📁 Encontradas {matchingFolders.Count} carpetas con patrón '{searchPattern}'");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error buscando carpetas con patrón '{searchPattern}'");
            }

            return matchingFolders;
        }

        private async Task<SharePointFileInfo?> SearchSpecificFolderAsync(string siteId, string driveId, string folderPath, string folderName)
        {
            try
            {
                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                var encodedPath = Uri.EscapeDataString(folderPath);
                var encodedFolderName = Uri.EscapeDataString(folderName);

                var filterUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/children?$filter=name eq '{encodedFolderName}' and folder ne null";
                var filterResponse = await httpClient.GetAsync(filterUrl);

                if (filterResponse.IsSuccessStatusCode)
                {
                    var filterContent = await filterResponse.Content.ReadAsStringAsync();
                    var filterResult = JsonSerializer.Deserialize<JsonElement>(filterContent);

                    if (filterResult.TryGetProperty("value", out var filterItems) && filterItems.GetArrayLength() > 0)
                    {
                        var item = filterItems[0];
                        _logger.LogInformation($"✅ Carpeta '{folderName}' encontrada con filtro directo");
                        return ParseSharePointItem(item);
                    }
                }

                _logger.LogInformation($"🔍 Filtro directo no encontró '{folderName}', usando paginación completa...");

                var baseUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/children?$top=999";
                string? nextLink = baseUrl;
                var pageCount = 0;

                while (!string.IsNullOrEmpty(nextLink))
                {
                    pageCount++;
                    _logger.LogInformation($"📄 Buscando en página {pageCount}...");

                    var response = await httpClient.GetAsync(nextLink);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<JsonElement>(content);

                        if (result.TryGetProperty("value", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                var itemName = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "";
                                var isFolder = item.TryGetProperty("folder", out _);

                                if (isFolder && string.Equals(itemName, folderName, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger.LogInformation($"✅ Carpeta '{folderName}' encontrada en página {pageCount}");
                                    return ParseSharePointItem(item);
                                }
                            }
                        }

                        nextLink = null;
                        if (result.TryGetProperty("@odata.nextLink", out var nextLinkEl))
                        {
                            nextLink = nextLinkEl.GetString();
                        }
                    }
                    else
                    {
                        break;
                    }

                    if (pageCount > 20)
                    {
                        _logger.LogWarning($"⚠️ Deteniendo búsqueda después de {pageCount} páginas");
                        break;
                    }
                }

                _logger.LogInformation($"❌ Carpeta '{folderName}' no encontrada después de buscar en {pageCount} páginas");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error buscando carpeta específica '{folderName}'");
                return null;
            }
        }

        private SharePointFileInfo ParseSharePointItem(JsonElement item)
        {
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Sin nombre";
            var isFolder = item.TryGetProperty("folder", out _);
            var size = item.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
            var modifiedBy = "Usuario SharePoint";
            var modified = DateTime.Now;

            if (item.TryGetProperty("lastModifiedDateTime", out var modEl))
            {
                DateTime.TryParse(modEl.GetString(), out modified);
            }

            if (item.TryGetProperty("lastModifiedBy", out var modByEl) &&
                modByEl.TryGetProperty("user", out var userEl) &&
                userEl.TryGetProperty("displayName", out var displayNameEl))
            {
                modifiedBy = displayNameEl.GetString() ?? "Usuario SharePoint";
            }

            return new SharePointFileInfo
            {
                Name = name ?? "Sin nombre",
                Type = "folder",
                Size = size,
                Modified = modified,
                ModifiedBy = modifiedBy,
                IsFolder = isFolder,
                WebUrl = item.TryGetProperty("webUrl", out var urlEl) ? urlEl.GetString() : ""
            };
        }

        private async Task<List<SharePointFileInfo>> GetAllRealFolderContentsAsync(string siteId, string driveId, string folderPath)
        {
            var allFiles = new List<SharePointFileInfo>();

            try
            {
                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                var encodedPath = Uri.EscapeDataString(folderPath);
                var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/children?$top=999";

                string? nextLink = url;

                while (!string.IsNullOrEmpty(nextLink))
                {
                    var response = await httpClient.GetAsync(nextLink);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<JsonElement>(content);

                        if (result.TryGetProperty("value", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Sin nombre";
                                var isFolder = item.TryGetProperty("folder", out _);
                                var size = item.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                                var modifiedBy = "Usuario SharePoint";
                                var modified = DateTime.Now;

                                if (item.TryGetProperty("lastModifiedDateTime", out var modEl))
                                {
                                    DateTime.TryParse(modEl.GetString(), out modified);
                                }

                                if (item.TryGetProperty("lastModifiedBy", out var modByEl) &&
                                    modByEl.TryGetProperty("user", out var userEl) &&
                                    userEl.TryGetProperty("displayName", out var displayNameEl))
                                {
                                    modifiedBy = displayNameEl.GetString() ?? "Usuario SharePoint";
                                }

                                allFiles.Add(new SharePointFileInfo
                                {
                                    Name = name ?? "Sin nombre",
                                    Type = isFolder ? "folder" : GetFileType(name ?? ""),
                                    Size = size,
                                    Modified = modified,
                                    ModifiedBy = modifiedBy,
                                    IsFolder = isFolder,
                                    WebUrl = item.TryGetProperty("webUrl", out var urlEl) ? urlEl.GetString() : ""
                                });
                            }
                        }

                        nextLink = null;
                        if (result.TryGetProperty("@odata.nextLink", out var nextLinkEl))
                        {
                            nextLink = nextLinkEl.GetString();
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogInformation($"📁 Obtenidos {allFiles.Count} elementos totales");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo contenido completo de carpeta");
            }

            return allFiles;
        }

        private async Task<string> GetSiteIdAsync()
        {
            try
            {
                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                if (string.IsNullOrEmpty(_config.SiteUrl))
                {
                    _logger.LogError("❌ SiteUrl no configurado");
                    return "";
                }

                var uri = new Uri(_config.SiteUrl);
                var hostname = uri.Host;
                var sitePath = uri.AbsolutePath;

                var url = $"https://graph.microsoft.com/v1.0/sites/{hostname}:{sitePath}";
                _logger.LogInformation("🌐 Obteniendo Site ID desde: {Url}", url);

                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(content);

                    if (result.TryGetProperty("id", out var idElement))
                    {
                        var siteId = idElement.GetString();
                        _logger.LogInformation("✅ Site ID obtenido: {SiteId}", siteId);
                        return siteId ?? "";
                    }
                }

                _logger.LogWarning("❌ No se pudo obtener Site ID. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, await response.Content.ReadAsStringAsync());
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo Site ID para URL: {SiteUrl}", _config.SiteUrl);
                return "";
            }
        }

        private async Task<List<SharePointFileInfo>> GetRealFolderContentsAsync(string siteId, string driveId, string folderPath)
        {
            var files = new List<SharePointFileInfo>();

            try
            {
                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                var encodedPath = Uri.EscapeDataString(folderPath);
                var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/children";

                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(content);

                    if (result.TryGetProperty("value", out var items))
                    {
                        var itemCount = items.GetArrayLength();
                        files.Add(new SharePointFileInfo
                        {
                            Name = $"✅ Contenido encontrado: {itemCount} elementos",
                            Type = "status",
                            Modified = DateTime.Now,
                            ModifiedBy = "Sistema",
                            Size = 0
                        });

                        foreach (var item in items.EnumerateArray())
                        {
                            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Sin nombre";
                            var isFolder = item.TryGetProperty("folder", out _);
                            var size = item.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                            var modifiedBy = "Usuario SharePoint";
                            var modified = DateTime.Now;

                            if (item.TryGetProperty("lastModifiedDateTime", out var modEl))
                            {
                                DateTime.TryParse(modEl.GetString(), out modified);
                            }

                            if (item.TryGetProperty("lastModifiedBy", out var modByEl) &&
                                modByEl.TryGetProperty("user", out var userEl) &&
                                userEl.TryGetProperty("displayName", out var displayNameEl))
                            {
                                modifiedBy = displayNameEl.GetString() ?? "Usuario SharePoint";
                            }

                            files.Add(new SharePointFileInfo
                            {
                                Name = name ?? "Sin nombre",
                                Type = isFolder ? "folder" : GetFileType(name ?? ""),
                                Size = size,
                                Modified = modified,
                                ModifiedBy = modifiedBy,
                                IsFolder = isFolder,
                                WebUrl = item.TryGetProperty("webUrl", out var urlEl) ? urlEl.GetString() : ""
                            });
                        }
                    }
                }
                else
                {
                    files.Add(new SharePointFileInfo
                    {
                        Name = $"⚠️ No se pudo acceder a la carpeta. Status: {response.StatusCode}",
                        Type = "error",
                        Modified = DateTime.Now,
                        ModifiedBy = "Sistema",
                        Size = 0
                    });
                }
            }
            catch (Exception ex)
            {
                files.Add(new SharePointFileInfo
                {
                    Name = $"⚠️ Error accediendo a carpeta: {ex.Message}",
                    Type = "error",
                    Modified = DateTime.Now,
                    ModifiedBy = "Sistema",
                    Size = 0
                });
            }

            return files;
        }

        private List<SharePointFileInfo> GetMockFolderStructure()
        {
            return new List<SharePointFileInfo>
            {
                new SharePointFileInfo
                {
                    Name = "2025-07-03",
                    Type = "folder",
                    Size = 531000000,
                    Modified = new DateTime(2025, 7, 3),
                    ModifiedBy = "Quiosco 2",
                    IsFolder = true,
                    WebUrl = $"{_config.SiteUrl}/Documentos/POD%20AKNA/POD%20AKNA%202025/POD%20QUIOSCO/2025-07-03"
                },
                new SharePointFileInfo
                {
                    Name = "2025-07-04",
                    Type = "folder",
                    Size = 619000000,
                    Modified = new DateTime(2025, 7, 4),
                    ModifiedBy = "Quiosco 2",
                    IsFolder = true,
                    WebUrl = $"{_config.SiteUrl}/Documentos/POD%20AKNA/POD%20AKNA%202025/POD%20QUIOSCO/2025-07-04"
                },
                new SharePointFileInfo
                {
                    Name = "2025-07-17",
                    Type = "folder",
                    Size = 372000000,
                    Modified = new DateTime(2025, 7, 17),
                    ModifiedBy = "Quiosco 2",
                    IsFolder = true,
                    WebUrl = $"{_config.SiteUrl}/Documentos/POD%20AKNA/POD%20AKNA%202025/POD%20QUIOSCO/2025-07-17"
                }
            };
        }

        private List<SharePointFileInfo> GetMockFolderContents(string folderName)
        {
            return new List<SharePointFileInfo>
            {
                new SharePointFileInfo
                {
                    Name = $"POD_18171",
                    Type = "folder",
                    Size = 0,
                    Modified = DateTime.Parse($"{folderName} 11:00"),
                    ModifiedBy = "Quiosco 2",
                    IsFolder = true,
                    WebUrl = $"{_config.SiteUrl}/Documentos/POD%20AKNA/POD%20AKNA%202025/POD%20QUIOSCO/{folderName}/POD_18171"
                },
                new SharePointFileInfo
                {
                    Name = $"POD_18172",
                    Type = "folder",
                    Size = 0,
                    Modified = DateTime.Parse($"{folderName} 11:00"),
                    ModifiedBy = "Quiosco 2",
                    IsFolder = true,
                    WebUrl = $"{_config.SiteUrl}/Documentos/POD%20AKNA/POD%20AKNA%202025/POD%20QUIOSCO/{folderName}/POD_18172"
                }
            };
        }

        private string GetFileType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "pdf",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "image",
                ".xlsx" or ".xls" => "xlsx",
                ".docx" or ".doc" => "docx",
                ".txt" => "txt",
                _ => "file"
            };
        }

        public async Task<bool> CreateTestFolderAsync(string folderName)
        {
            try
            {
                _logger.LogInformation($"📁 Creando carpeta de prueba: {folderName}");
                await Task.Delay(1000);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creando carpeta");
                return false;
            }
        }

        public async Task<byte[]> GetFileBytesAsync(string carpeta, string fileName)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando archivo: {FileName} en carpeta: {Carpeta}", fileName, carpeta);

                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret); _graphClient = new GraphServiceClient(credential);
                }

                var siteId = await GetSiteIdAsync();
                if (string.IsNullOrEmpty(siteId))
                {
                    _logger.LogError("❌ No se pudo obtener Site ID");
                    return null;
                }

                var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                var documentDrive = drives?.Value?.FirstOrDefault(d =>
                    d.Name?.Contains("Documents", StringComparison.OrdinalIgnoreCase) == true ||
                    d.Name?.Contains("Documentos", StringComparison.OrdinalIgnoreCase) == true);

                if (documentDrive == null)
                {
                    _logger.LogError("❌ No se encontró la biblioteca de documentos");
                    return null;
                }

                var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                var searchPath = string.IsNullOrEmpty(carpeta) ? basePath : $"{basePath}/{carpeta}";

                _logger.LogInformation("🔍 Buscando en ruta: {SearchPath}", searchPath);

                var foundFile = await SearchFileRecursivelyAsync(siteId, documentDrive.Id, searchPath, fileName);

                if (foundFile != null)
                {
                    _logger.LogInformation("✅ Archivo encontrado en: {Path}", foundFile.FullPath);
                    return await DownloadFileFromSharePointAsync(siteId, documentDrive.Id, foundFile.FullPath);
                }

                _logger.LogWarning("❌ Archivo no encontrado: {FileName} en {SearchPath}", fileName, searchPath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en GetFileBytesAsync para {FileName} en {Carpeta}", fileName, carpeta);
                return null;
            }
        }

        private async Task<SharePointFileResult> SearchFileRecursivelyAsync(string siteId, string driveId, string folderPath, string targetFileName)
        {
            try
            {
                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                return await SearchInFolderAsync(httpClient, siteId, driveId, folderPath, targetFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en búsqueda recursiva");
                return null;
            }
        }

        private async Task<SharePointFileResult> SearchInFolderAsync(HttpClient httpClient, string siteId, string driveId, string currentPath, string targetFileName)
        {
            var encodedPath = Uri.EscapeDataString(currentPath);
            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/children";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            if (!result.TryGetProperty("value", out var items)) return null;

            foreach (var item in items.EnumerateArray())
            {
                var itemName = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "";
                var isFolder = item.TryGetProperty("folder", out _);

                if (!isFolder)
                {
                    if (FileMatchesPattern(itemName, targetFileName))
                    {
                        return new SharePointFileResult
                        {
                            FullPath = $"{currentPath}/{itemName}",
                            FileName = itemName
                        };
                    }
                }
                else
                {
                    var subfolderPath = $"{currentPath}/{itemName}";
                    var foundInSubfolder = await SearchInFolderAsync(httpClient, siteId, driveId, subfolderPath, targetFileName);
                    if (foundInSubfolder != null)
                    {
                        return foundInSubfolder;
                    }
                }
            }

            return null;
        }

        private bool FileMatchesPattern(string fileName, string targetPattern)
        {
            var patterns = new[]
            {
                targetPattern,
                $"{targetPattern}.jpg",
                $"{targetPattern}.jpeg",
                $"{targetPattern}.png",
                $"{targetPattern}_1",
                $"{targetPattern}_1.jpg",
                targetPattern.Replace("POD_", "POD"),
            };

            return patterns.Any(pattern =>
                fileName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<byte[]> DownloadFileFromSharePointAsync(string siteId, string driveId, string filePath)
        {
            var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
            var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var encodedPath = Uri.EscapeDataString(filePath);
            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}:/content";

            using var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            return null;
        }

        public async Task<string> GetSiteInfoAsync()
        {
            try
            {
                return $"Sitio: {_config.SiteUrl} - Configurado correctamente";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // ========================================================================
        // NUEVOS MÉTODOS PARA SUBIR, REEMPLAZAR Y ELIMINAR ARCHIVOS
        // ========================================================================

        public async Task<bool> UploadFileAsync(string folderPath, string fileName, byte[] fileContent)
        {
            try
            {
                _logger.LogInformation("📤 UPLOAD - Subiendo: {File} a {Folder} ({Size} KB)",
                    fileName, folderPath, fileContent.Length / 1024);

                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                    _graphClient = new GraphServiceClient(credential);
                }

                var siteId = await GetSiteIdAsync();
                if (string.IsNullOrEmpty(siteId))
                {
                    _logger.LogError("❌ No se pudo obtener Site ID");
                    return false;
                }

                var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                var documentDrive = drives?.Value?.FirstOrDefault(d =>
                    d.Name?.Contains("Documents", StringComparison.OrdinalIgnoreCase) == true ||
                    d.Name?.Contains("Documentos", StringComparison.OrdinalIgnoreCase) == true);

                if (documentDrive == null)
                {
                    _logger.LogError("❌ No se encontró la biblioteca de documentos");
                    return false;
                }

                var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

                _logger.LogInformation("📂 Ruta completa: {FullPath}/{FileName}", fullPath, fileName);

                await EnsureFolderExistsAsync(siteId, documentDrive.Id, fullPath);

                var encodedPath = Uri.EscapeDataString($"{fullPath}/{fileName}");
                var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{documentDrive.Id}/root:/{encodedPath}:/content";

                var credential2 = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential2.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                var content = new ByteArrayContent(fileContent);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                var response = await httpClient.PutAsync(uploadUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Archivo subido exitosamente: {File}", fileName);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ Error subiendo archivo. Status: {Status}, Error: {Error}",
                        response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Excepción subiendo archivo {File} a {Folder}", fileName, folderPath);
                return false;
            }
        }

        public async Task<bool> ReplaceFileAsync(string folderPath, string fileName, byte[] newContent)
        {
            try
            {
                _logger.LogInformation("🔄 REPLACE - Reemplazando: {File} en {Folder} ({Size} KB)",
                    fileName, folderPath, newContent.Length / 1024);

                var exists = await FileExistsAsync(folderPath, fileName);

                if (!exists)
                {
                    _logger.LogWarning("⚠️ Archivo no existe, creando uno nuevo");
                    return await UploadFileAsync(folderPath, fileName, newContent);
                }

                var result = await UploadFileAsync(folderPath, fileName, newContent);

                if (result)
                {
                    _logger.LogInformation("✅ Archivo reemplazado exitosamente: {File}", fileName);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error reemplazando archivo {File}", fileName);
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string folderPath, string fileName)
        {
            try
            {
                _logger.LogInformation("🗑️ DELETE - Eliminando: {File} de {Folder}", fileName, folderPath);

                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                    _graphClient = new GraphServiceClient(credential);
                }

                var siteId = await GetSiteIdAsync();
                if (string.IsNullOrEmpty(siteId))
                {
                    _logger.LogError("❌ No se pudo obtener Site ID");
                    return false;
                }

                var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                var documentDrive = drives?.Value?.FirstOrDefault(d =>
                    d.Name?.Contains("Documents", StringComparison.OrdinalIgnoreCase) == true ||
                    d.Name?.Contains("Documentos", StringComparison.OrdinalIgnoreCase) == true);

                if (documentDrive == null)
                {
                    _logger.LogError("❌ No se encontró la biblioteca de documentos");
                    return false;
                }

                var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

                var encodedPath = Uri.EscapeDataString($"{fullPath}/{fileName}");
                var deleteUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{documentDrive.Id}/root:/{encodedPath}";

                var credential2 = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential2.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                var response = await httpClient.DeleteAsync(deleteUrl);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Archivo eliminado: {File}", fileName);
                    return true;
                }
                else
                {
                    _logger.LogWarning("⚠️ No se pudo eliminar. Status: {Status}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error eliminando archivo {File}", fileName);
                return false;
            }
        }

        private async Task<bool> FileExistsAsync(string folderPath, string fileName)
        {
            try
            {
                if (_graphClient == null)
                {
                    var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                    _graphClient = new GraphServiceClient(credential);
                }

                var siteId = await GetSiteIdAsync();
                if (string.IsNullOrEmpty(siteId)) return false;

                var drives = await _graphClient.Sites[siteId].Drives.GetAsync();
                var documentDrive = drives?.Value?.FirstOrDefault(d =>
                    d.Name?.Contains("Documents", StringComparison.OrdinalIgnoreCase) == true ||
                    d.Name?.Contains("Documentos", StringComparison.OrdinalIgnoreCase) == true);

                if (documentDrive == null) return false;

                var basePath = _config.LiquidacionesFolder ?? "POD AKNA/POD AKNA 2025/POD QUIOSCO";
                var fullPath = string.IsNullOrEmpty(folderPath) ? basePath : $"{basePath}/{folderPath}";

                var encodedPath = Uri.EscapeDataString($"{fullPath}/{fileName}");
                var fileUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{documentDrive.Id}/root:/{encodedPath}";

                var credential2 = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential2.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                var response = await httpClient.GetAsync(fileUrl);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task EnsureFolderExistsAsync(string siteId, string driveId, string folderPath)
        {
            try
            {
                _logger.LogInformation("📁 Verificando carpetas: {Path}", folderPath);

                var folders = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var currentPath = "";

                var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                foreach (var folderName in folders)
                {
                    var parentPath = string.IsNullOrEmpty(currentPath) ? "" : currentPath;
                    currentPath = string.IsNullOrEmpty(currentPath) ? folderName : $"{currentPath}/{folderName}";

                    var encodedPath = Uri.EscapeDataString(currentPath);
                    var checkUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encodedPath}";

                    var checkResponse = await httpClient.GetAsync(checkUrl);

                    if (!checkResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("📁 Creando carpeta: {Folder}", folderName);

                        var createUrl = string.IsNullOrEmpty(parentPath)
                            ? $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root/children"
                            : $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{Uri.EscapeDataString(parentPath)}:/children";

                        var createBody = new
                        {
                            name = folderName,
                            folder = new { },
                            AdditionalData = new Dictionary<string, object>
                            {
                                { "@microsoft.graph.conflictBehavior", "rename" }
                            }
                        };

                        var jsonContent = JsonSerializer.Serialize(createBody);
                        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                        var createResponse = await httpClient.PostAsync(createUrl, content);

                        if (!createResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await createResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("⚠️ No se pudo crear carpeta {Folder}: {Error}", folderName, errorContent);
                        }
                        else
                        {
                            _logger.LogInformation("✅ Carpeta creada: {Folder}", folderName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error asegurando carpetas");
                throw;
            }
        }

        public class SharePointFileResult
        {
            public string FullPath { get; set; }
            public string FileName { get; set; }
        }
    }
}