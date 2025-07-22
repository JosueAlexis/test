using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoRH2025.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            HttpContext.Session.Clear(); // ?? Limpia la sesi�n
            return RedirectToPage("/Login"); // ?? Redirige al login
        }
    }
}
