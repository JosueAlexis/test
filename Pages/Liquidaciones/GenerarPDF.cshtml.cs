using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.MODELS;
using ProyectoRH2025.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace ProyectoRH2025.Pages.Liquidaciones
{
    public class GenerarPDFModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISharePointTestService _sharePointService;
        private readonly ILogger<GenerarPDFModel> _logger;

        public GenerarPDFModel(
            ApplicationDbContext context,
            ISharePointTestService sharePointService,
            ILogger<GenerarPDFModel> logger)
        {
            _context = context;
            _sharePointService = sharePointService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(int? id, string? ids)
        {
            if (id.HasValue)
            {
                var podRecord = await _context.PodRecords
                    .Include(p => p.PodEvidenciasImagenes)
                    .FirstOrDefaultAsync(p => p.POD_ID == id.Value);

                if (podRecord == null) return NotFound();

                await CargarEvidenciasSharePointSiNecesario(podRecord);
                return await GenerarPdfUnicoConMultiplesRegistros(new List<PodRecord> { podRecord });
            }

            if (!string.IsNullOrEmpty(ids))
            {
                var idsList = ids.Split(',').Select(int.Parse).ToArray();
                return await OnPostGenerarPDFsMasivosAsync(idsList);
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostGenerarPDFsMasivosAsync(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] = "Debe seleccionar al menos un registro.";
                return RedirectToPage("./Index");
            }

            var podRecords = await _context.PodRecords
                .Include(p => p.PodEvidenciasImagenes)
                .Where(p => selectedIds.Contains(p.POD_ID))
                .ToListAsync();

            if (podRecords.Count == 0)
            {
                TempData["Error"] = "No se encontraron registros.";
                return RedirectToPage("./Index");
            }

            foreach (var pod in podRecords)
            {
                await CargarEvidenciasSharePointSiNecesario(pod);
            }

            // Manda todos los registros a un SOLO archivo PDF
            return await GenerarPdfUnicoConMultiplesRegistros(podRecords);
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

                var carpetaPod = contenidoDelDia.FirstOrDefault(item =>
                        item.IsFolder && item.Type != "status" && item.Type != "error" &&
                        item.Name.Equals($"POD_{podRecord.POD_ID}", StringComparison.OrdinalIgnoreCase));

                if (carpetaPod != null)
                {
                    var rutaCarpetaPod = $"{fechaCarpeta}/{carpetaPod.Name}";
                    var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(rutaCarpetaPod);
                    var imagenes = archivosEnCarpeta.Where(archivo => !archivo.IsFolder && archivo.Type != "status" && archivo.Type != "error" && EsArchivoImagen(archivo.Name)).ToList();

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
                                        MimeType = GetMimeType(imagen.Name)
                                    });
                                }
                            }
                            catch (Exception ex) { _logger.LogError(ex, "Error descargando imagen"); }
                        }
                    }
                }
                else
                {
                    await BuscarEnFechaExacta(podRecord, podRecord.FechaSalida.Value.ToString("yyyy-MM-dd"));
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error SharePoint"); }
        }

        private async Task BuscarEnFechaExacta(PodRecord podRecord, string fechaExacta)
        {
            try
            {
                var contenidoExacto = await _sharePointService.GetAllFolderContentsAsync(fechaExacta);
                var carpetaPodExacta = contenidoExacto.FirstOrDefault(item => item.IsFolder && item.Type != "status" && item.Type != "error" && item.Name.Equals($"POD_{podRecord.POD_ID}", StringComparison.OrdinalIgnoreCase));

                if (carpetaPodExacta != null)
                {
                    var rutaCarpetaPod = $"{fechaExacta}/{carpetaPodExacta.Name}";
                    var archivosEnCarpeta = await _sharePointService.GetAllFolderContentsAsync(rutaCarpetaPod);
                    var imagenes = archivosEnCarpeta.Where(a => !a.IsFolder && a.Type != "status" && a.Type != "error" && EsArchivoImagen(a.Name)).ToList();

                    if (imagenes.Any())
                    {
                        if (podRecord.PodEvidenciasImagenes == null) podRecord.PodEvidenciasImagenes = new List<PodEvidenciaImagen>();
                        foreach (var imagen in imagenes)
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
                                    MimeType = GetMimeType(imagen.Name)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error exacta"); }
        }

        private bool EsArchivoImagen(string nombreArchivo)
        {
            if (string.IsNullOrEmpty(nombreArchivo)) return false;
            var extensionesImagen = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif" };
            return extensionesImagen.Contains(Path.GetExtension(nombreArchivo)?.ToLowerInvariant());
        }

        private string GetMimeType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/jpeg"
            };
        }

        // ========================================================================
        // MAGIA DEL PDF ÚNICO
        // ========================================================================
        private async Task<IActionResult> GenerarPdfUnicoConMultiplesRegistros(List<PodRecord> podRecords)
        {
            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 50, 50);
                var writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                for (int i = 0; i < podRecords.Count; i++)
                {
                    AgregarContenidoPodAlDocumento(document, writer, podRecords[i]);

                    // Agregar un salto de página para el siguiente registro, excepto si es el último
                    if (i < podRecords.Count - 1)
                    {
                        document.NewPage();
                    }
                }

                document.Close();

                string prefijo = podRecords.Count == 1 ? podRecords.First().Folio : "Masivo";
                var nombrePdf = $"Reporte_Liquidaciones_{prefijo}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

                return File(ms.ToArray(), "application/pdf", nombrePdf);
            }
        }

        // FUNCIÓN GLOBAL REUTILIZABLE PARA ARMAR EL LIENZO DEL PDF
        public static void AgregarContenidoPodAlDocumento(Document document, PdfWriter writer, PodRecord podRecord)
        {
            var titulo = new Paragraph($"PRUEBA DE ENTREGA (POD)")
            {
                Alignment = Element.ALIGN_CENTER,
                Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18)
            };
            document.Add(titulo);

            var subtitulo = new Paragraph($"Folio: {podRecord.Folio}")
            {
                Alignment = Element.ALIGN_CENTER,
                Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14)
            };
            document.Add(subtitulo);
            document.Add(new Paragraph(" "));

            var table = new PdfPTable(2);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1, 2 });

            void AgregarFila(string etiqueta, string valor)
            {
                var cellEtiqueta = new PdfPCell(new Phrase(etiqueta, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10))) { BackgroundColor = BaseColor.LightGray, Padding = 5 };
                table.AddCell(cellEtiqueta);
                var cellValor = new PdfPCell(new Phrase(valor ?? "N/A", FontFactory.GetFont(FontFactory.HELVETICA, 10))) { Padding = 5 };
                table.AddCell(cellValor);
            }

            AgregarFila("Cliente:", podRecord.Cliente);
            AgregarFila("Tractor:", podRecord.Tractor);
            AgregarFila("Remolque:", podRecord.Remolque);
            AgregarFila("Conductor:", podRecord.DriverName);
            AgregarFila("Origen:", podRecord.Origen);
            AgregarFila("Destino:", podRecord.Destino);
            AgregarFila("Planta:", podRecord.Plant);
            AgregarFila("Fecha de Salida:", podRecord.FechaSalida?.ToString("dd/MM/yyyy HH:mm"));
            AgregarFila("Fecha de Entrega:", podRecord.CaptureDate?.ToString("dd/MM/yyyy HH:mm"));

            string txtStatus = podRecord.Status switch { 0 => "En Tránsito", 1 => "Entregado", 2 => "Pendiente", _ => "Desconocido" };
            AgregarFila("Status:", txtStatus);

            document.Add(table);
            document.Add(new Paragraph(" "));

            var evidencias = podRecord.PodEvidenciasImagenes?.Where(e => e.ImageData != null && e.ImageData.Length > 0).ToList();

            if (evidencias?.Any() == true)
            {
                document.Add(new Paragraph($"EVIDENCIAS FOTOGRÁFICAS ({evidencias.Count} imagen(es))") { Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14), SpacingBefore = 10, SpacingAfter = 10 });

                foreach (var evidencia in evidencias)
                {
                    try
                    {
                        var image = Image.GetInstance(evidencia.ImageData);
                        image.ScaleToFit(document.PageSize.Width - document.LeftMargin - document.RightMargin, 300f);
                        image.Alignment = Element.ALIGN_CENTER;
                        document.Add(image);

                        document.Add(new Paragraph($"Archivo: {evidencia.FileName} | Fecha: {evidencia.CaptureDate?.ToString("dd/MM/yyyy HH:mm")}") { Font = FontFactory.GetFont(FontFactory.HELVETICA, 8), Alignment = Element.ALIGN_CENTER, SpacingAfter = 15 });

                        if (writer.GetVerticalPosition(false) < 100 && evidencia != evidencias.Last())
                        {
                            document.NewPage();
                        }
                    }
                    catch (Exception ex)
                    {
                        document.Add(new Paragraph($"Error al procesar imagen: {ex.Message}"));
                    }
                }
            }
            else
            {
                document.Add(new Paragraph("No hay evidencias fotográficas disponibles.") { Font = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 12), Alignment = Element.ALIGN_CENTER, SpacingBefore = 20 });
            }

            document.Add(new Paragraph(" "));
            document.Add(new Paragraph($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm} | Pertenece al Folio: {podRecord.Folio}") { Font = FontFactory.GetFont(FontFactory.HELVETICA, 8), Alignment = Element.ALIGN_RIGHT });
        }
    }
}