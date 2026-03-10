using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ProyectoRH2025.Data;
using ProyectoRH2025.MODELS;
using ProyectoRH2025.Services;
using ProyectoRH2025.Pages.Liquidaciones; // Importamos tu clase de la página

namespace ProyectoRH2025.BackgroundJobs
{
    public interface IReporteMasivoJob
    {
        // Renombramos el método para reflejar que ahora es PDF
        Task GenerarPdfMasivoAsync(int[] selectedIds, string emailUsuario, string requestScheme, string requestHost);
    }

    public class ReporteMasivoJob : IReporteMasivoJob
    {
        private readonly ApplicationDbContext _context;
        private readonly ISharePointTestService _sharePointService;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ReporteMasivoJob> _logger;

        public ReporteMasivoJob(
            ApplicationDbContext context,
            ISharePointTestService sharePointService,
            IEmailService emailService,
            IWebHostEnvironment env,
            ILogger<ReporteMasivoJob> logger)
        {
            _context = context;
            _sharePointService = sharePointService;
            _emailService = emailService;
            _env = env;
            _logger = logger;
        }

        public async Task GenerarPdfMasivoAsync(int[] selectedIds, string emailUsuario, string requestScheme, string requestHost)
        {
            try
            {
                _logger.LogInformation($"Iniciando generación de PDF ÚNICO para {selectedIds.Length} registros. Solicitado por: {emailUsuario}");

                var podRecords = await _context.PodRecords
                    .Include(p => p.PodEvidenciasImagenes)
                    .Where(p => selectedIds.Contains(p.POD_ID))
                    .ToListAsync();

                string fechaPdf = DateTime.Now.ToString("yyyyMMdd_HHmm");
                string nombrePdf = $"Reporte_Masivo_Liquidaciones_{fechaPdf}.pdf";

                string finalDirectory = Path.Combine(_env.WebRootPath, "descargas_masivas");
                if (!Directory.Exists(finalDirectory))
                {
                    Directory.CreateDirectory(finalDirectory);
                }

                string finalPdfPath = Path.Combine(finalDirectory, nombrePdf);

                // CREAMOS UN SOLO ARCHIVO PDF FÍSICO
                using (var fs = new FileStream(finalPdfPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var document = new Document(PageSize.A4, 50, 50, 50, 50);
                    var writer = PdfWriter.GetInstance(document, fs);
                    document.Open();

                    for (int i = 0; i < podRecords.Count; i++)
                    {
                        var pod = podRecords[i];
                        await CargarEvidenciasSharePointSiNecesario(pod);

                        // Utilizamos la función global que centralizamos en GenerarPDF.cshtml.cs
                        GenerarPDFModel.AgregarContenidoPodAlDocumento(document, writer, pod);

                        // Salto de página para el siguiente registro
                        if (i < podRecords.Count - 1)
                        {
                            document.NewPage();
                        }
                    }

                    document.Close();
                }

                // Enviar el correo electrónico informando sobre el PDF
                string linkDescarga = $"{requestScheme}://{requestHost}/descargas_masivas/{nombrePdf}";

                string mensajeHtml = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                        <h2 style='color: #dc3545;'><i class='fas fa-file-pdf'></i> Tu Reporte PDF está listo</h2>
                        <p>El reporte consolidado de liquidaciones que solicitaste ya fue generado exitosamente en un solo documento.</p>
                        <p>Total de folios incluidos: <strong>{podRecords.Count}</strong></p>
                        <div style='margin-top: 30px;'>
                            <a href='{linkDescarga}' style='background-color: #dc3545; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                Descargar Archivo PDF
                            </a>
                        </div>
                        <p style='margin-top: 30px; font-size: 12px; color: #777;'>
                            * El archivo estará disponible para su descarga durante 48 horas.
                        </p>
                    </div>";

                await _emailService.EnviarCorreoAsync(emailUsuario, "Reporte PDF de Liquidaciones", mensajeHtml);

                _logger.LogInformation($"Proceso finalizado. Correo PDF enviado a {emailUsuario}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grave generando el PDF masivo.");
                throw;
            }
        }

        private async Task CargarEvidenciasSharePointSiNecesario(PodRecord podRecord)
        {
            try
            {
                var evidenciasEnBD = podRecord.PodEvidenciasImagenes?.Where(e => e.ImageData != null && e.ImageData.Length > 0).ToList();
                if (evidenciasEnBD?.Any() == true) return;

                if (!podRecord.FechaSalida.HasValue) return;

                var fechaProcesamiento = podRecord.FechaSalida.Value.AddDays(1);
                var fechaCarpeta = fechaProcesamiento.ToString("yyyy-MM-dd");
                var contenidoDelDia = await _sharePointService.GetAllFolderContentsAsync(fechaCarpeta);

                var carpetaPod = contenidoDelDia.FirstOrDefault(item => item.IsFolder && item.Type != "status" && item.Type != "error" && item.Name.Equals($"POD_{podRecord.POD_ID}", StringComparison.OrdinalIgnoreCase));

                if (carpetaPod != null)
                {
                    var rutaCarpetaPod = $"{fechaCarpeta}/{carpetaPod.Name}";
                    var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(rutaCarpetaPod);
                    var imagenes = archivosEnCarpeta.Where(a => !a.IsFolder && a.Type != "status" && a.Type != "error").ToList();

                    if (imagenes.Any())
                    {
                        if (podRecord.PodEvidenciasImagenes == null) podRecord.PodEvidenciasImagenes = new List<PodEvidenciaImagen>();

                        foreach (var imagen in imagenes)
                        {
                            try
                            {
                                var imageBytes = await _sharePointService.GetFileBytesAsync(rutaCarpetaPod, imagen.Name);
                                if (imageBytes != null && imageBytes.Length > 0)
                                {
                                    podRecord.PodEvidenciasImagenes.Add(new PodEvidenciaImagen
                                    {
                                        EvidenciaID = 0,
                                        POD_ID_FK = podRecord.POD_ID,
                                        FileName = imagen.Name,
                                        ImageData = imageBytes,
                                        CaptureDate = imagen.Modified,
                                        MimeType = "image/jpeg"
                                    });
                                }
                            }
                            catch (Exception ex) { _logger.LogError(ex, "Error descargando imagen SP"); }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error cargando evidencias de SharePoint"); }
        }
    }
}