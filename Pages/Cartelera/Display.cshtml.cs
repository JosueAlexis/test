using Microsoft.AspNetCore.Authorization; // <-- Agregado para permitir acceso
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Cartelera
{
    [AllowAnonymous] // <-- ESTA LÍNEA ES LA MAGIA: Exenta esta página del Login
    public class DisplayModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DisplayModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
            // La página principal carga sin datos estáticos, todo es por JS
        }

        // Endpoint para que la TV consulte el texto de la barra dinámica
        public async Task<IActionResult> OnGetConfigAsync()
        {
            var tickerConfig = await _context.CarteleraConfigs
                .FirstOrDefaultAsync(c => c.ConfigKey == "TickerText");

            return new JsonResult(new
            {
                tickerText = tickerConfig?.ConfigValue ?? "🟢 Bienvenidos a ProyectoRH2025 | 💡 Usa el panel de admin para cambiar este texto"
            });
        }
    }
}