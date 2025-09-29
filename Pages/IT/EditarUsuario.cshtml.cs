using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace ProyectoRH2025.Pages.IT
{
    public class EditarUsuarioModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditarUsuarioModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Usuario Usuario { get; set; } = new();

        [BindProperty]
        [DataType(DataType.Password)]
        public string? NuevaPassword { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Compare("NuevaPassword", ErrorMessage = "Las contraseñas no coinciden.")]
        public string? ConfirmarPassword { get; set; }

        [BindProperty]
        public bool ForzarCambioPassword { get; set; }

        public List<SelectListItem> Roles { get; set; } = new();
        public string? MensajeError { get; set; }
        public string? MensajeExito { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            // Verificar permisos
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 7) // Solo Admin o IT
            {
                return RedirectToPage("/Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            Usuario = await _context.TblUsuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.idUsuario == id);

            if (Usuario == null)
            {
                return NotFound();
            }

            ForzarCambioPassword = Usuario.DefaultPassw == 1;
            await CargarRoles();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Verificar permisos
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 7)
            {
                return RedirectToPage("/Login");
            }

            // Validar contraseñas si se proporcionaron
            if (!string.IsNullOrEmpty(NuevaPassword))
            {
                if (NuevaPassword.Length < 6)
                {
                    MensajeError = "La contraseña debe tener al menos 6 caracteres.";
                    await CargarRoles();
                    return Page();
                }

                if (NuevaPassword != ConfirmarPassword)
                {
                    MensajeError = "Las contraseñas no coinciden.";
                    await CargarRoles();
                    return Page();
                }
            }

            try
            {
                var usuarioExistente = await _context.TblUsuarios.FindAsync(Usuario.idUsuario);
                if (usuarioExistente == null)
                {
                    MensajeError = "Usuario no encontrado.";
                    await CargarRoles();
                    return Page();
                }

                // Verificar que el nombre de usuario no esté siendo usado por otro usuario
                var usuarioDuplicado = await _context.TblUsuarios
                    .AnyAsync(u => u.UsuarioNombre == Usuario.UsuarioNombre && u.idUsuario != Usuario.idUsuario);

                if (usuarioDuplicado)
                {
                    MensajeError = "Ya existe otro usuario con ese nombre.";
                    await CargarRoles();
                    return Page();
                }

                // Verificar que no se esté eliminando el último administrador
                if (usuarioExistente.Status == 1 && Usuario.Status == 0)
                {
                    var esAdmin = usuarioExistente.idRol == 5 || usuarioExistente.idRol == 7;
                    if (esAdmin)
                    {
                        var cantidadAdminsActivos = await _context.TblUsuarios
                            .Where(u => (u.idRol == 5 || u.idRol == 7) && u.Status == 1 && u.idUsuario != Usuario.idUsuario)
                            .CountAsync();

                        if (cantidadAdminsActivos == 0)
                        {
                            MensajeError = "No se puede desactivar el último administrador del sistema.";
                            await CargarRoles();
                            return Page();
                        }
                    }
                }

                // Actualizar campos
                usuarioExistente.UsuarioNombre = Usuario.UsuarioNombre;
                usuarioExistente.NombreCompleto = Usuario.NombreCompleto;
                usuarioExistente.CorreoElectronico = Usuario.CorreoElectronico;
                usuarioExistente.idRol = Usuario.idRol;
                usuarioExistente.Status = Usuario.Status;
                usuarioExistente.idSucursal = Usuario.idSucursal;

                // Actualizar contraseña si se proporcionó
                if (!string.IsNullOrEmpty(NuevaPassword))
                {
                    usuarioExistente.pass = HashPassword(NuevaPassword);
                    usuarioExistente.CambioPass = DateTime.Now;
                }

                // Manejar forzar cambio de contraseña
                if (ForzarCambioPassword)
                {
                    usuarioExistente.DefaultPassw = 1;
                }
                else
                {
                    usuarioExistente.DefaultPassw = 0;
                }

                await _context.SaveChangesAsync();

                MensajeExito = "Usuario actualizado exitosamente.";

                // Limpiar campos de contraseña
                NuevaPassword = string.Empty;
                ConfirmarPassword = string.Empty;

                await CargarRoles();
                return Page();
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al actualizar usuario: {ex.Message}";
                await CargarRoles();
                return Page();
            }
        }

        private async Task CargarRoles()
        {
            var roles = await _context.TblRolusuario
                .OrderBy(r => r.RolNombre)
                .ToListAsync();

            Roles = roles.Select(r => new SelectListItem
            {
                Value = r.idRol.ToString(),
                Text = r.RolNombre,
                Selected = r.idRol == Usuario.idRol
            }).ToList();
        }

        private byte[] HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                return bytes;
            }
        }
    }
}