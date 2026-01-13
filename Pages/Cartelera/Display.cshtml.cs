using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoRH2025.Pages.Cartelera
{
    public class DisplayModel : PageModel
    {
        public void OnGet()
        {
            // Esta página no necesita cargar datos en el servidor
            // Todo se carga desde el API en JavaScript
        }
    }
}