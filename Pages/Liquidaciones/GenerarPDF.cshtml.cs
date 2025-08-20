using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.MODELS;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace ProyectoRH2025.Pages.Liquidaciones
{
    public class GenerarPDFModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public GenerarPDFModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // OPCIÓN 1 y 2: Generar PDF individual
        public async Task<IActionResult> OnGetAsync(int? id, string? ids)
        {
            // Si viene un solo ID
            if (id.HasValue)
            {
                var podRecord = await _context.PodRecords
                    .Include(p => p.PodEvidenciasImagenes)
                    .FirstOrDefaultAsync(p => p.POD_ID == id.Value);

                if (podRecord == null)
                    return NotFound();

                // Generar nombre del archivo: Remolque + Fecha del status
                var fechaStatus = podRecord.CaptureDate?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
                var remolque = !string.IsNullOrEmpty(podRecord.Remolque) ? podRecord.Remolque : "SinRemolque";
                var nombreArchivo = $"{remolque}_{fechaStatus}.pdf";

                var pdfBytes = GenerarPDFBytes(podRecord);

                return File(pdfBytes, "application/pdf", nombreArchivo);
            }

            // Si vienen múltiples IDs (desde selección masiva)
            if (!string.IsNullOrEmpty(ids))
            {
                var idsList = ids.Split(',').Select(int.Parse).ToArray();
                return await OnPostGenerarPDFsMasivosAsync(idsList);
            }

            return NotFound();
        }

        // OPCIÓN 3: Generar PDFs masivos
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

            // Si es solo uno, generar PDF individual
            if (podRecords.Count == 1)
            {
                var pod = podRecords.First();
                var fechaStatus = pod.CaptureDate?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
                var remolque = !string.IsNullOrEmpty(pod.Remolque) ? pod.Remolque : "SinRemolque";
                var nombreArchivo = $"{remolque}_{fechaStatus}.pdf";
                var pdfBytes = GenerarPDFBytes(pod);
                return File(pdfBytes, "application/pdf", nombreArchivo);
            }

            // Si son múltiples, crear un ZIP con todos los PDFs
            return await GenerarZipConPDFs(podRecords);
        }

        private byte[] GenerarPDFBytes(PodRecord podRecord)
        {
            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 50, 50);
                var writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                try
                {
                    // Título principal
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
                    document.Add(new Paragraph(" ")); // Espacio

                    // Información del viaje en tabla
                    var table = new PdfPTable(2);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 1, 2 });

                    // Función auxiliar para agregar filas a la tabla
                    void AgregarFila(string etiqueta, string valor)
                    {
                        var cellEtiqueta = new PdfPCell(new Phrase(etiqueta, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                        cellEtiqueta.BackgroundColor = BaseColor.LightGray;
                        cellEtiqueta.Padding = 5;
                        table.AddCell(cellEtiqueta);

                        var cellValor = new PdfPCell(new Phrase(valor ?? "N/A", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
                        cellValor.Padding = 5;
                        table.AddCell(cellValor);
                    }

                    AgregarFila("Cliente:", podRecord.Cliente);
                    AgregarFila("Tractor:", podRecord.Tractor);
                    AgregarFila("Remolque:", podRecord.Remolque);
                    AgregarFila("Conductor:", podRecord.DriverName);
                    AgregarFila("Origen:", podRecord.Origen);
                    AgregarFila("Destino:", podRecord.Destino);
                    AgregarFila("Planta:", podRecord.Plant); // <-- CAMPO PLANT AGREGADO
                    AgregarFila("Fecha de Salida:", podRecord.FechaSalida?.ToString("dd/MM/yyyy HH:mm"));
                    AgregarFila("Fecha de Entrega:", podRecord.CaptureDate?.ToString("dd/MM/yyyy HH:mm"));
                    AgregarFila("Status:", GetStatusText(podRecord.Status));

                    document.Add(table);
                    document.Add(new Paragraph(" ")); // Espacio

                    // Evidencias fotográficas
                    var evidenciasConImagen = podRecord.PodEvidenciasImagenes?.Where(e => e.ImageData != null && e.ImageData.Length > 0).ToList();

                    if (evidenciasConImagen?.Any() == true)
                    {
                        var tituloEvidencias = new Paragraph($"EVIDENCIAS FOTOGRÁFICAS ({evidenciasConImagen.Count} imagen(es))")
                        {
                            Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14),
                            SpacingBefore = 10,
                            SpacingAfter = 10
                        };
                        document.Add(tituloEvidencias);

                        foreach (var evidencia in evidenciasConImagen)
                        {
                            try
                            {
                                var image = Image.GetInstance(evidencia.ImageData);

                                // Ajustar tamaño de la imagen para que quepa en la página
                                float maxWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
                                float maxHeight = 300f;

                                image.ScaleToFit(maxWidth, maxHeight);
                                image.Alignment = Element.ALIGN_CENTER;

                                document.Add(image);

                                // Información de la imagen
                                var infoImagen = new Paragraph($"Archivo: {evidencia.FileName} | Fecha: {evidencia.CaptureDate?.ToString("dd/MM/yyyy HH:mm")}")
                                {
                                    Font = FontFactory.GetFont(FontFactory.HELVETICA, 8),
                                    Alignment = Element.ALIGN_CENTER,
                                    SpacingAfter = 15
                                };
                                document.Add(infoImagen);

                                // Verificar si necesitamos nueva página
                                if (writer.GetVerticalPosition(false) < 100)
                                {
                                    document.NewPage();
                                }
                            }
                            catch (Exception ex)
                            {
                                // Si la imagen no se puede procesar, agregar texto de error
                                var errorParagraph = new Paragraph($"Error al procesar imagen: {evidencia.FileName} - {ex.Message}")
                                {
                                    Font = FontFactory.GetFont(FontFactory.HELVETICA, 8),
                                    SpacingAfter = 10
                                };
                                document.Add(errorParagraph);
                            }
                        }
                    }
                    else
                    {
                        var noEvidencias = new Paragraph("No hay evidencias fotográficas disponibles.")
                        {
                            Font = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 12),
                            Alignment = Element.ALIGN_CENTER,
                            SpacingBefore = 20
                        };
                        document.Add(noEvidencias);
                    }

                    // Pie de página con fecha de generación
                    document.NewPage();
                    var piePagina = new Paragraph($"Documento generado el: {DateTime.Now:dd/MM/yyyy HH:mm}")
                    {
                        Font = FontFactory.GetFont(FontFactory.HELVETICA, 8),
                        Alignment = Element.ALIGN_RIGHT
                    };
                    document.Add(piePagina);
                }
                catch (Exception ex)
                {
                    // En caso de error, agregar mensaje de error al documento
                    document.Add(new Paragraph($"Error al generar PDF: {ex.Message}"));
                }
                finally
                {
                    document.Close();
                }

                return ms.ToArray();
            }
        }
        private async Task<IActionResult> GenerarZipConPDFs(List<PodRecord> podRecords)
        {
            using (var zipStream = new MemoryStream())
            {
                using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var pod in podRecords)
                    {
                        var fechaStatus = pod.CaptureDate?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
                        var remolque = !string.IsNullOrEmpty(pod.Remolque) ? pod.Remolque : "SinRemolque";
                        var nombreArchivo = $"{remolque}_{fechaStatus}.pdf";

                        var pdfBytes = GenerarPDFBytes(pod);

                        var zipEntry = archive.CreateEntry(nombreArchivo);
                        using (var entryStream = zipEntry.Open())
                        {
                            await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                        }
                    }
                }

                var fechaZip = DateTime.Now.ToString("yyyyMMdd_HHmm");
                var nombreZip = $"PDFs_Liquidaciones_{fechaZip}.zip";

                return File(zipStream.ToArray(), "application/zip", nombreZip);
            }
        }

        private string GetStatusText(byte? status)
        {
            return status switch
            {
                0 => "En Tránsito",      // Corregido: 0 en lugar de 1
                1 => "Entregado",       // Corregido: 1 en lugar de 2
                2 => "Pendiente",       // Corregido: 2 en lugar de 3
                _ => "Desconocido"
            };
        }
    }
}