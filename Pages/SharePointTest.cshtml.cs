// Pages/SharePointTest.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProyectoRH2025.Services;

namespace ProyectoRH2025.Pages
{
    public class SharePointTestModel : PageModel
    {
        private readonly ISharePointTestService _sharePointService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SharePointTestModel> _logger;

        public SharePointTestModel(
            ISharePointTestService sharePointService,
            IConfiguration configuration,
            ILogger<SharePointTestModel> logger)
        {
            _sharePointService = sharePointService;
            _configuration = configuration;
            _logger = logger;
        }

        public SharePointTestResult? TestResult { get; set; }
        public List<SharePointFileInfo> TestFiles { get; set; } = new();
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            // Pasar configuración a la vista
            ViewData["ClientSecret"] = _configuration["SharePoint:ClientSecret"];
            ViewData["SiteUrl"] = _configuration["SharePoint:SiteUrl"];
            ViewData["TenantId"] = _configuration["SharePoint:TenantId"];
            ViewData["ClientId"] = _configuration["SharePoint:ClientId"];

            StatusMessage = "?? Listo para ejecutar pruebas de SharePoint";
        }

        public async Task<IActionResult> OnPostTestConnectionAsync()
        {
            try
            {
                // Pasar configuración a la vista
                ViewData["ClientSecret"] = _configuration["SharePoint:ClientSecret"];
                ViewData["SiteUrl"] = _configuration["SharePoint:SiteUrl"];
                ViewData["TenantId"] = _configuration["SharePoint:TenantId"];
                ViewData["ClientId"] = _configuration["SharePoint:ClientId"];

                TestResult = await _sharePointService.TestConnectionAsync();

                if (TestResult.IsSuccess)
                {
                    StatusMessage = "? Conexión exitosa a SharePoint";
                    TestFiles = await _sharePointService.GetTestFilesAsync();
                }
                else
                {
                    StatusMessage = "? Error en la conexión a SharePoint";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"?? Excepción: {ex.Message}";
                _logger.LogError(ex, "Error in SharePoint test");
            }

            return Page();
        }

        public async Task<IActionResult> OnGetGetFilesAsync(string folderPath = "")
        {
            try
            {
                // Obtener archivos de la carpeta específica
                var files = await _sharePointService.GetFolderContentsAsync(folderPath);
                return new JsonResult(files);
            }
            catch (Exception ex)
            {
                return new JsonResult(new List<SharePointFileInfo>
                {
                    new SharePointFileInfo
                    {
                        Name = $"? Error: {ex.Message}",
                        Type = "error",
                        Modified = DateTime.Now,
                        ModifiedBy = "Sistema",
                        Size = 0
                    }
                });
            }
        }

        public async Task<IActionResult> OnPostCreateTestFolderAsync(string folderName)
        {
            // Pasar configuración a la vista
            ViewData["ClientSecret"] = _configuration["SharePoint:ClientSecret"];
            ViewData["SiteUrl"] = _configuration["SharePoint:SiteUrl"];
            ViewData["TenantId"] = _configuration["SharePoint:TenantId"];
            ViewData["ClientId"] = _configuration["SharePoint:ClientId"];

            if (string.IsNullOrEmpty(folderName))
            {
                StatusMessage = "?? Nombre de carpeta requerido";
                return Page();
            }

            try
            {
                var success = await _sharePointService.CreateTestFolderAsync(folderName);
                StatusMessage = success
                    ? $"? Carpeta '{folderName}' creada exitosamente"
                    : $"? Error creando carpeta '{folderName}'";
            }
            catch (Exception ex)
            {
                StatusMessage = $"?? Error: {ex.Message}";
                _logger.LogError(ex, "Error creating test folder");
            }

            return Page();
        }

        // Método auxiliar para obtener clase CSS del icono
        public string GetFileIconClass(string fileType)
        {
            return fileType?.ToLower() switch
            {
                "pdf" => "fas fa-file-pdf text-danger",
                "image" => "fas fa-file-image text-success",
                "docx" => "fas fa-file-word text-primary",
                "xlsx" => "fas fa-file-excel text-success",
                "folder" => "fas fa-folder text-warning",
                "status" => "fas fa-info-circle text-info",
                "error" => "fas fa-exclamation-triangle text-danger",
                _ => "fas fa-file text-muted"
            };
        }

        // Método auxiliar para formatear tamaño de archivo
        public string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }
    }
}