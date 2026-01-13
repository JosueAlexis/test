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

        // ========== NUEVOS MÉTODOS PARA CARTELERA DIGITAL ==========

        /// <summary>
        /// Sube un archivo a la carpeta Activos de Cartelera Digital en SharePoint
        /// </summary>
        /// <param name="fileStream">Stream del archivo a subir</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="contentType">Tipo MIME del archivo</param>
        /// <returns>URL del archivo en SharePoint</returns>
        Task<string> UploadCarteleraFileAsync(Stream fileStream, string fileName, string contentType);

        /// <summary>
        /// Obtiene la lista de archivos activos en la Cartelera Digital
        /// </summary>
        /// <returns>Lista de archivos con su información</returns>
        Task<List<CarteleraFileInfo>> GetCarteleraActivosAsync();

        /// <summary>
        /// Mueve un archivo de la carpeta Activos a la carpeta Archivo
        /// </summary>
        /// <param name="fileName">Nombre del archivo a archivar</param>
        /// <returns>True si se archivó correctamente</returns>
        Task<bool> ArchivarCarteleraItemAsync(string fileName);

        /// <summary>
        /// Elimina un archivo de SharePoint (de Activos o Archivo)
        /// </summary>
        /// <param name="fileName">Nombre del archivo a eliminar</param>
        /// <param name="isArchived">True si el archivo está en la carpeta Archivo, False si está en Activos</param>
        /// <returns>True si se eliminó correctamente</returns>
        Task<bool> DeleteCarteleraFileAsync(string fileName, bool isArchived = false);

        /// <summary>
        /// Obtiene la URL de descarga directa de un archivo en SharePoint
        /// </summary>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="isArchived">True si el archivo está en la carpeta Archivo</param>
        /// <returns>URL de descarga directa</returns>
        Task<string> GetCarteleraFileDownloadUrlAsync(string fileName, bool isArchived = false);
    }

    /// <summary>
    /// Clase auxiliar para retornar información de archivos de Cartelera Digital
    /// </summary>
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