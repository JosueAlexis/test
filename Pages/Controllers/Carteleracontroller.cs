using Microsoft.AspNetCore.Authorization; // <-- Agregado
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.MODELS;
using ProyectoRH2025.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // NOTA: No ponemos AllowAnonymous aquí arriba para que el resto del controlador siga protegido
    public class CarteleraController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CarteleraController> _logger;
        private readonly ISharePointTestService _sharePointService;

        public CarteleraController(
            ApplicationDbContext context,
            ILogger<CarteleraController> logger,
            ISharePointTestService sharePointService)
        {
            _context = context;
            _logger = logger;
            _sharePointService = sharePointService;
        }

        /// <summary>
        /// Obtiene todos los items activos de la cartelera
        /// </summary>
        [AllowAnonymous] // <-- PERMISO PARA LA TV
        [HttpGet("GetActive")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                _logger.LogInformation("📺 API: Obteniendo items activos de cartelera...");

                var now = DateTime.Now;

                var items = await _context.CarteleraItems
                    .Where(i => i.IsActive)
                    .Where(i => i.StartDate == null || i.StartDate <= now)
                    .Where(i => i.EndDate == null || i.EndDate >= now)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new
                    {
                        id = i.Id,
                        fileName = i.FileName,
                        contentType = i.ContentType,
                        sharePointUrl = i.SharePointUrl,
                        description = i.Description ?? "",
                        durationSeconds = i.DurationSeconds,
                        category = i.Category ?? "",
                        mimeType = i.MimeType ?? ""
                    })
                    .ToListAsync();

                _logger.LogInformation($"✅ API: Retornando {items.Count} items activos");

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Error obteniendo items activos de cartelera");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene un item específico por ID
        /// </summary>
        [HttpGet("GetItem/{id}")]
        public async Task<IActionResult> GetItem(int id)
        {
            // ESTE SE QUEDA SIN ALLOW ANONYMOUS (Solo el Admin puede consultar esto)
            try
            {
                _logger.LogInformation($"📺 API: Obteniendo item {id}...");

                var item = await _context.CarteleraItems
                    .Where(i => i.Id == id)
                    .Select(i => new
                    {
                        id = i.Id,
                        fileName = i.FileName,
                        contentType = i.ContentType,
                        sharePointUrl = i.SharePointUrl,
                        description = i.Description ?? "",
                        durationSeconds = i.DurationSeconds,
                        displayOrder = i.DisplayOrder,
                        category = i.Category ?? "",
                        isActive = i.IsActive,
                        startDate = i.StartDate,
                        endDate = i.EndDate,
                        uploadDate = i.UploadDate,
                        fileSize = i.FileSize,
                        mimeType = i.MimeType ?? ""
                    })
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    _logger.LogWarning($"⚠️ API: Item {id} no encontrado");
                    return NotFound(new { error = "Item no encontrado" });
                }

                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ API: Error obteniendo item {id}");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de la cartelera
        /// </summary>
        [HttpGet("GetStats")]
        public async Task<IActionResult> GetStats()
        {
            // ESTE SE QUEDA SIN ALLOW ANONYMOUS
            try
            {
                _logger.LogInformation("📊 API: Obteniendo estadísticas...");

                var totalItems = await _context.CarteleraItems.CountAsync();
                var activeItems = await _context.CarteleraItems.CountAsync(i => i.IsActive);
                var inactiveItems = totalItems - activeItems;

                var totalSize = await _context.CarteleraItems.SumAsync(i => (long?)i.FileSize) ?? 0;

                var stats = new
                {
                    totalItems,
                    activeItems,
                    inactiveItems,
                    totalSizeMB = Math.Round(totalSize / 1024.0 / 1024.0, 2)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Error obteniendo estadísticas");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint proxy para servir imágenes desde SharePoint sin autenticación
        /// </summary>
        [AllowAnonymous] // <-- PERMISO PARA LA TV
        [HttpGet("GetImage/{id}")]
        public async Task<IActionResult> GetImage(int id)
        {
            try
            {
                var item = await _context.CarteleraItems.FindAsync(id);
                if (item == null)
                {
                    return NotFound();
                }

                var downloadUrl = await _sharePointService.GetCarteleraFileDownloadUrlAsync(item.FileName, false);

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    return NotFound();
                }

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                var bytes = await httpClient.GetByteArrayAsync(downloadUrl);

                return File(bytes, item.MimeType ?? "application/octet-stream", enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ API: Error obteniendo imagen {id}");
                return StatusCode(500);
            }
        }
    }
}