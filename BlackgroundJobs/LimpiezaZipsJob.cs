namespace ProyectoRH2025.BackgroundJobs
{
    public interface ILimpiezaZipsJob
    {
        void BorrarZipsAntiguos();
    }

    public class LimpiezaZipsJob : ILimpiezaZipsJob
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LimpiezaZipsJob> _logger;

        public LimpiezaZipsJob(IWebHostEnvironment env, ILogger<LimpiezaZipsJob> logger)
        {
            _env = env;
            _logger = logger;
        }

        public void BorrarZipsAntiguos()
        {
            try
            {
                string directoryPath = Path.Combine(_env.WebRootPath, "descargas_masivas");

                if (!Directory.Exists(directoryPath)) return;

                // Obtener TODOS los archivos de la carpeta (ya no solo *.zip)
                var archivos = Directory.GetFiles(directoryPath);
                int borrados = 0;

                foreach (var archivo in archivos)
                {
                    var fileInfo = new FileInfo(archivo);

                    // Si el archivo fue creado hace más de 48 horas (2 días)
                    if (fileInfo.CreationTime < DateTime.Now.AddDays(-2))
                    {
                        fileInfo.Delete();
                        borrados++;
                    }
                }

                if (borrados > 0)
                {
                    _logger.LogInformation($"Limpieza automática: Se borraron {borrados} archivos antiguos de la carpeta descargas_masivas.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar limpiar los archivos de descargas_masivas.");
            }
        }
    }
}