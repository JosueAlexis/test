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

        // Propiedades para la vista
        public List<CarteleraItem> Items { get; set; } = new List<CarteleraItem>();
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }
        public int DefaultDuration { get; set; } = 10;
        public string TransitionType { get; set; } = "fade";

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Cargando datos de Cartelera Digital...");

                // Cargar items de la base de datos
                Items = await _context.CarteleraItems
                    .OrderBy(i => i.DisplayOrder)
                    .ThenByDescending(i => i.UploadDate)
                    .ToListAsync();

                _logger.LogInformation($"✅ Se cargaron {Items.Count} items de la base de datos");

                TotalItems = Items.Count;
                ActiveItems = Items.Count(i => i.IsActive);

                _logger.LogInformation($"📊 Total: {TotalItems}, Activos: {ActiveItems}");

                // Cargar configuración
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

                // Validar tamaño (50 MB máximo)
                if (file.Length > 50 * 1024 * 1024)
                {
                    TempData["Error"] = "El archivo excede el tamaño máximo de 50 MB";
                    return RedirectToPage();
                }

                // Validar tipo de archivo
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var contentType = DetermineContentType(extension);

                if (contentType == "unknown")
                {
                    TempData["Error"] = "Tipo de archivo no soportado";
                    return RedirectToPage();
                }

                // Generar nombre único para evitar conflictos
                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";

                // Subir a SharePoint
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

                // Obtener el último DisplayOrder
                var maxOrder = await _context.CarteleraItems.MaxAsync(i => (int?)i.DisplayOrder) ?? 0;

                // Crear registro en base de datos
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

                _logger.LogInformation("Archivo subido exitosamente: {FileName}", uniqueFileName);
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

        public async Task<IActionResult> OnPostUpdateConfigAsync(
            int defaultDuration,
            string transitionType)
        {
            try
            {
                // Actualizar DefaultDuration
                var durationConfig = await _context.CarteleraConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "DefaultDuration");

                if (durationConfig != null)
                {
                    durationConfig.ConfigValue = defaultDuration.ToString();
                    durationConfig.LastUpdated = DateTime.Now;
                }
                else
                {
                    _context.CarteleraConfigs.Add(new CarteleraConfig
                    {
                        ConfigKey = "DefaultDuration",
                        ConfigValue = defaultDuration.ToString(),
                        LastUpdated = DateTime.Now
                    });
                }

                // Actualizar TransitionType
                var transitionConfig = await _context.CarteleraConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "TransitionType");

                if (transitionConfig != null)
                {
                    transitionConfig.ConfigValue = transitionType;
                    transitionConfig.LastUpdated = DateTime.Now;
                }
                else
                {
                    _context.CarteleraConfigs.Add(new CarteleraConfig
                    {
                        ConfigKey = "TransitionType",
                        ConfigValue = transitionType,
                        LastUpdated = DateTime.Now
                    });
                }

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
                if (item == null)
                {
                    return NotFound();
                }

                item.IsActive = !item.IsActive;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Item {Id} cambiado a {State}", id, item.IsActive ? "Activo" : "Inactivo");

                return new JsonResult(new { success = true, isActive = item.IsActive });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando estado del item {Id}", id);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostUpdateItemAsync(
            int id,
            string description,
            int duration,
            int displayOrder,
            string category,
            DateTime? startDate,
            DateTime? endDate)
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

                _logger.LogInformation("Item {Id} actualizado", id);
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
                if (item == null)
                {
                    return new JsonResult(new { success = false, error = "Item no encontrado" });
                }

                // Eliminar de SharePoint
                var deleted = await _sharePointService.DeleteCarteleraFileAsync(item.FileName, false);

                if (!deleted)
                {
                    _logger.LogWarning("No se pudo eliminar archivo de SharePoint: {FileName}", item.FileName);
                }

                // Eliminar de base de datos
                _context.CarteleraItems.Remove(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Item {Id} eliminado", id);

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