// Services/ISharePointTestService.cs
namespace ProyectoRH2025.Services
{
    public interface ISharePointTestService
    {
        Task<SharePointTestResult> TestConnectionAsync();
        Task<List<SharePointFileInfo>> GetAllFolderContentsAsync(string folderPath = "");

        Task<List<SharePointFileInfo>> GetTestFilesAsync();
        Task<List<SharePointFileInfo>> GetFolderContentsAsync(string folderPath = "");
        Task<byte[]> GetFileBytesAsync(string carpeta, string fileName);
        Task<bool> CreateTestFolderAsync(string folderName);
        Task<string> GetSiteInfoAsync();
    }
}