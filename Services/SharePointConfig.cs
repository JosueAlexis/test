// Services/SharePointConfig.cs
namespace ProyectoRH2025.Services
{
    public class SharePointConfig
    {
        public string SiteUrl { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string DocumentLibrary { get; set; } = "Shared Documents";
        public string LiquidacionesFolder { get; set; } = "Liquidaciones";
    }

    public class SharePointTestResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "";
        public string? Error { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    public class SharePointFileInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public string ModifiedBy { get; set; } = "";
        public string WebUrl { get; set; } = "";
        public bool IsFolder { get; set; }
    }
}