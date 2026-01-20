using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;

namespace ProyectoRH2025.Pages.IT
{
    public class UsuariosEliminadosModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public UsuariosEliminadosModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<UsuarioEliminadoVista> UsuariosEliminados { get; set; } = new();

        [TempData]
        public string MensajeExito { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Solo IT puede acceder
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 7) // Solo Admin o IT
            {
                return RedirectToPage("/Login");
            }

            await CargarUsuariosEliminadosAsync();
            return Page();
        }

        private async Task CargarUsuariosEliminadosAsync()
        {
            UsuariosEliminados = await _context.TblUsuarios
                .Include(u => u.Rol)
                .Where(u => u.EsEliminado)
                .OrderByDescending(u => u.FechaEliminacion)
                .Select(u => new UsuarioEliminadoVista
                {
                    idUsuario = u.idUsuario,
                    UsuarioNombre = u.UsuarioNombre,
                    NombreCompleto = u.NombreCompleto,
                    NombreRol = u.Rol.RolNombre,
                    FechaEliminacion = u.FechaEliminacion,
                    MotivoEliminacion = u.MotivoEliminacion,
                    // Obtener nombre del usuario que eliminó
                    NombreEliminador = _context.TblUsuarios
                        .Where(x => x.idUsuario == u.EliminadoPor)
                        .Select(x => x.UsuarioNombre)
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        // ==========================================
        // HANDLER: RESTAURAR USUARIO
        // ==========================================
        [BindProperty]
        public int IdUsuario { get; set; }

        public async Task<IActionResult> OnPostRestaurarUsuarioAsync()
        {
            var idUsuarioIT = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuarioIT == null)
            {
                MensajeError = "Sesión expirada";
                return RedirectToPage();
            }

            var usuario = await _context.TblUsuarios
                .FirstOrDefaultAsync(u => u.idUsuario == IdUsuario && u.EsEliminado);

            if (usuario == null)
            {
                MensajeError = "Usuario no encontrado";
                return RedirectToPage();
            }

            // Restaurar usuario
            usuario.EsEliminado = false;
            usuario.FechaEliminacion = null;
            usuario.EliminadoPor = null;
            usuario.MotivoEliminacion = null;
            usuario.Status = 1; // Reactivar

            await _context.SaveChangesAsync();

            MensajeExito = $"Usuario {usuario.UsuarioNombre} restaurado correctamente";
            return RedirectToPage();
        }

        public class UsuarioEliminadoVista
        {
            public int idUsuario { get; set; }
            public string UsuarioNombre { get; set; }
            public string? NombreCompleto { get; set; }
            public string NombreRol { get; set; }
            public DateTime? FechaEliminacion { get; set; }
            public string? MotivoEliminacion { get; set; }
            public string? NombreEliminador { get; set; }
        }
    }
}
