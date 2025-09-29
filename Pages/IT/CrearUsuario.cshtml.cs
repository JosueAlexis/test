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
    public class CrearUsuarioModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CrearUsuarioModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Usuario Usuario { get; set; } = new Usuario { Status = 1 };

        [BindProperty]
        [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string PasswordInicial { get; set; } = string.Empty;

        [BindProperty]
        public bool ForzarCambio { get; set; } = true;

        public List<SelectListItem> Roles { get; set; } = new();
        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar permisos
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 7) // Solo Admin o IT
            {
                return RedirectToPage("/Login");
            }

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

            if (!ModelState.IsValid)
            {
                await CargarRoles();
                return Page();
            }

            try
            {
                // Verificar que el usuario no exista
                var usuarioExistente = await _context.TblUsuarios
                    .AnyAsync(u => u.UsuarioNombre == Usuario.UsuarioNombre);

                if (usuarioExistente)
                {
                    MensajeError = "Ya existe un usuario con ese nombre.";
                    await CargarRoles();
                    return Page();
                }

                // Verificar que el rol existe
                var rolExiste = await _context.TblRolusuario
                    .AnyAsync(r => r.idRol == Usuario.idRol);

                if (!rolExiste)
                {
                    MensajeError = "El rol seleccionado no existe.";
                    await CargarRoles();
                    return Page();
                }

                // Hashear la contraseña
                Usuario.pass = HashPassword(PasswordInicial);
                Usuario.DefaultPassw = ForzarCambio ? 1 : 0;
                Usuario.CambioPass = DateTime.Now;

                _context.TblUsuarios.Add(Usuario);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Usuario {Usuario.UsuarioNombre} creado exitosamente.";
                return RedirectToPage("/IT/Usuarios");
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al crear usuario: {ex.Message}";
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
                Text = r.RolNombre
            }).ToList();

            // Agregar opción por defecto
            Roles.Insert(0, new SelectListItem
            {
                Value = "",
                Text = "Seleccione un rol"
            });
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