// Services/ISharePointTestService.cs
namespace ProyectoRH2025.Services
{
    public interface ISharePointTestService
    {
        // Métodos existentes (NO TOCAR)
        Task<SharePointTestResult> TestConnectionAsync();
        Task<List<SharePointFileInfo>> GetAllFolderContentsAsync(string folderPath = "");
        Task<List<SharePointFileInfo>> GetTestFilesAsync();
        Task<List<SharePointFileInfo>> GetFolderContentsAsync(string folderPath = "");
        Task<byte[]> GetFileBytesAsync(string carpeta, string fileName);
        Task<bool> CreateTestFolderAsync(string folderName);
        Task<string> GetSiteInfoAsync();

        // Métodos de subida/reemplazo/eliminación
        Task<bool> UploadFileAsync(string folderPath, string fileName, byte[] fileContent);
        Task<bool> ReplaceFileAsync(string folderPath, string fileName, byte[] newContent);
        Task<bool> DeleteFileAsync(string folderPath, string fileName);

        // ✅ NUEVO: Verificar si un archivo existe en SharePoint
        Task<bool> FileExistsAsync(string folderPath, string fileName);

        // ========== MÉTODOS PARA CARTELERA DIGITAL ==========
        Task<string> UploadCarteleraFileAsync(Stream fileStream, string fileName, string contentType);
        Task<List<CarteleraFileInfo>> GetCarteleraActivosAsync();
        Task<bool> ArchivarCarteleraItemAsync(string fileName);
        Task<bool> DeleteCarteleraFileAsync(string fileName, bool isArchived = false);
        Task<string> GetCarteleraFileDownloadUrlAsync(string fileName, bool isArchived = false);
    }

    public class CarteleraFileInfo
    {
        public string FileName { get; set; }
        public string SharePointUrl { get; set; }
        public string WebUrl { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public string DownloadUrl { get; set; }
    }
}