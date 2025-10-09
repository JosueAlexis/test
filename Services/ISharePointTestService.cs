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

        // ========== AGREGAR ESTOS 3 MÉTODOS NUEVOS ==========

        /// <summary>
        /// Sube un nuevo archivo a SharePoint
        /// </summary>
        Task<bool> UploadFileAsync(string folderPath, string fileName, byte[] fileContent);

        /// <summary>
        /// Reemplaza un archivo existente en SharePoint
        /// </summary>
        Task<bool> ReplaceFileAsync(string folderPath, string fileName, byte[] newContent);

        /// <summary>
        /// Elimina un archivo de SharePoint
        /// </summary>
        Task<bool> DeleteFileAsync(string folderPath, string fileName);
    }
}