// Services/ISharePointBatchService.cs
namespace ProyectoRH2025.Services
{
    public interface ISharePointBatchService
    {
        /// <summary>
        /// Sube múltiples archivos a SharePoint en paralelo con reintentos automáticos
        /// </summary>
        /// <param name="uploadTasks">Lista de tareas de subida con folderPath, fileName y fileContent</param>
        /// <param name="maxConcurrency">Número máximo de subidas simultáneas (default: 5)</param>
        /// <param name="cancellationToken">Token para cancelar la operación</param>
        /// <returns>Resultado del batch con estadísticas de éxito/fallo</returns>
        Task<BatchUploadResult> UploadFilesInBatchAsync(
            List<FileUploadTask> uploadTasks,
            int maxConcurrency = 5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sube un archivo individual con reintentos automáticos
        /// </summary>
        Task<FileUploadResult> UploadFileWithRetryAsync(
            string folderPath,
            string fileName,
            byte[] fileContent,
            int maxRetries = 3,
            CancellationToken cancellationToken = default);
    }

    public class FileUploadTask
    {
        public string FolderPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public string? Metadata { get; set; } // Para tracking adicional (ej: POD_ID)
    }

    public class FileUploadResult
    {
        public bool IsSuccess { get; set; }
        public string FileName { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public int AttemptsCount { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }

    public class BatchUploadResult
    {
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<FileUploadResult> Results { get; set; } = new();
        public TimeSpan TotalElapsedTime { get; set; }

        public double SuccessRate => TotalFiles > 0 ? (double)SuccessCount / TotalFiles * 100 : 0;
    }
}
