using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.MODELS;
using ProyectoRH2025.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Cartelera
{
    public class AdminModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISharePointTestService _sharePointService;
        private readonly ILogger<AdminModel> _logger;

        public AdminModel(
            ApplicationDbContext context,
            ISharePointTestService sharePointService,
            ILogger<AdminModel> logger)
        {
            _context = context;
            _sharePointService = sharePointService;
            _logger = logger;
        }

        public List<CarteleraItem> Items { get; set; } = new List<CarteleraItem>();
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }
        public int DefaultDuration { get; set; } = 10;
        public string TransitionType { get; set; } = "fade";
        public string TickerText { get; set; } = "🟢 Bienvenidos a ProyectoRH2025 | ⚠️ Registra tu asistencia";

        public async Task OnGetAsync()
        {
            try
            {
                Items = await _context.CarteleraItems
                    .OrderBy(i => i.DisplayOrder)
                    .ThenByDescending(i => i.UploadDate)
                    .ToListAsync();

                TotalItems = Items.Count;
                ActiveItems = Items.Count(i => i.IsActive);

                var durationConfig = await _context.CarteleraConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "DefaultDuration");

                if (durationConfig != null)
                {
                    int.TryParse(durationConfig.ConfigValue, out int duration);
                    DefaultDuration = duration > 0 ? duration : 10;
                }

                var transitionConfig = await _context.CarteleraConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "TransitionType");

                if (transitionConfig != null)
                {
                    TransitionType = transitionConfig.ConfigValue;
                }

                var tickerConfig = await _context.CarteleraConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "TickerText");

                if (tickerConfig != null)
                {
                    TickerText = tickerConfig.ConfigValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error cargando datos de administración");
                Items = new List<CarteleraItem>();
            }
        }

        public async Task<IActionResult> OnPostUploadAsync(
            IFormFile file,
            string description,
            int duration,
            string category)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    TempData["Error"] = "No se seleccionó ningún archivo";
                    return RedirectToPage();
                }

                // Cambia esto:
                if (file.Length > 100 * 1024 * 1024) // <--- Cambiado de 50 a 100
                {
                    TempData["Error"] = "El archivo excede el tamaño máximo de 100 MB"; // <--- Mensaje actualizado
                    return RedirectToPage();
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var contentType = DetermineContentType(extension);

                if (contentType == "unknown")
                {
                    TempData["Error"] = "Tipo de archivo no soportado";
                    return RedirectToPage();
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                string sharePointUrl;

                using (var stream = file.OpenReadStream())
                {
                    sharePointUrl = await _sharePointService.UploadCarteleraFileAsync(
                        stream,
                        uniqueFileName,
                        file.ContentType
                    );
                }

                if (string.IsNullOrEmpty(sharePointUrl))
                {
                    TempData["Error"] = "Error al subir archivo a SharePoint";
                    return RedirectToPage();
                }

                var maxOrder = await _context.CarteleraItems.MaxAsync(i => (int?)i.DisplayOrder) ?? 0;

                var item = new CarteleraItem
                {
                    FileName = uniqueFileName,
                    ContentType = contentType,
                    SharePointPath = $"Cartelera Digital/Activos/{uniqueFileName}",
                    SharePointUrl = sharePointUrl,
                    IsActive = true,
                    DisplayOrder = maxOrder + 1,
                    DurationSeconds = duration > 0 ? duration : DefaultDuration,
                    UploadDate = DateTime.Now,
                    UploaderUserId = User?.Identity?.Name ?? "Sistema",
                    Description = description,
                    FileSize = file.Length,
                    Category = category,
                    MimeType = file.ContentType
                };

                _context.CarteleraItems.Add(item);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Archivo '{file.FileName}' subido exitosamente";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo archivo");
                TempData["Error"] = "Error al subir archivo: " + ex.Message;
                return RedirectToPage();
            }
        }

        // --- NUEVO MÉTODO PARA GUARDAR CÁMARAS Y ENLACES WEB ---
        public async Task<IActionResult> OnPostAddLinkAsync(
            string url,
            string description,
            int duration,
            string category)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    TempData["Error"] = "La URL no puede estar vacía";
                    return RedirectToPage();
                }

                var maxOrder = await _context.CarteleraItems.MaxAsync(i => (int?)i.DisplayOrder) ?? 0;

                var item = new CarteleraItem
                {
                    FileName = "Cámara / Enlace Web",
                    ContentType = "iframe",
                    SharePointPath = "Web Link",
                    SharePointUrl = url, // Guardamos la URL directamente aquí
                    IsActive = true,
                    DisplayOrder = maxOrder + 1,
                    DurationSeconds = duration > 0 ? duration : DefaultDuration,
                    UploadDate = DateTime.Now,
                    UploaderUserId = User?.Identity?.Name ?? "Sistema",
                    Description = description,
                    FileSize = 0,
                    Category = category,
                    MimeType = "text/html"
                };

                _context.CarteleraItems.Add(item);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Enlace agregado exitosamente";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando enlace");
                TempData["Error"] = "Error al agregar enlace: " + ex.Message;
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostUpdateConfigAsync(
            int defaultDuration,
            string transitionType,
            string tickerText)
        {
            try
            {
                var durationConfig = await _context.CarteleraConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "DefaultDuration");
                if (durationConfig != null) { durationConfig.ConfigValue = defaultDuration.ToString(); durationConfig.LastUpdated = DateTime.Now; }
                else { _context.CarteleraConfigs.Add(new CarteleraConfig { ConfigKey = "DefaultDuration", ConfigValue = defaultDuration.ToString(), LastUpdated = DateTime.Now }); }

                var transitionConfig = await _context.CarteleraConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "TransitionType");
                if (transitionConfig != null) { transitionConfig.ConfigValue = transitionType; transitionConfig.LastUpdated = DateTime.Now; }
                else { _context.CarteleraConfigs.Add(new CarteleraConfig { ConfigKey = "TransitionType", ConfigValue = transitionType, LastUpdated = DateTime.Now }); }

                var tickerConfig = await _context.CarteleraConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "TickerText");
                if (tickerConfig != null) { tickerConfig.ConfigValue = tickerText ?? ""; tickerConfig.LastUpdated = DateTime.Now; }
                else { _context.CarteleraConfigs.Add(new CarteleraConfig { ConfigKey = "TickerText", ConfigValue = tickerText ?? "", LastUpdated = DateTime.Now }); }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Configuración actualizada exitosamente";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando configuración");
                TempData["Error"] = "Error al actualizar configuración";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int id)
        {
            try
            {
                var item = await _context.CarteleraItems.FindAsync(id);
                if (item == null) return NotFound();

                item.IsActive = !item.IsActive;
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, isActive = item.IsActive });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando estado del item {Id}", id);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostUpdateItemAsync(
            int id, string description, int duration, int displayOrder,
            string category, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var item = await _context.CarteleraItems.FindAsync(id);
                if (item == null)
                {
                    TempData["Error"] = "Item no encontrado";
                    return RedirectToPage();
                }

                item.Description = description;
                item.DurationSeconds = duration;
                item.DisplayOrder = displayOrder;
                item.Category = category;
                item.StartDate = startDate;
                item.EndDate = endDate;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Item actualizado exitosamente";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando item {Id}", id);
                TempData["Error"] = "Error al actualizar item";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var item = await _context.CarteleraItems.FindAsync(id);
                if (item == null) return new JsonResult(new { success = false, error = "Item no encontrado" });

                // No intentar borrar de SharePoint si es un simple enlace web
                if (item.ContentType != "iframe")
                {
                    var deleted = await _sharePointService.DeleteCarteleraFileAsync(item.FileName, false);
                    if (!deleted)
                    {
                        _logger.LogWarning("No se pudo eliminar archivo de SharePoint: {FileName}", item.FileName);
                    }
                }

                _context.CarteleraItems.Remove(item);
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando item {Id}", id);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        private string DetermineContentType(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "image",
                ".mp4" or ".webm" => "video",
                ".pdf" => "pdf",
                _ => "unknown"
            };
        }
    }
}