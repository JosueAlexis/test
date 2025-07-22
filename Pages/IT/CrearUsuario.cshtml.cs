using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
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
        public Usuario Usuario { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "La contrase�a es obligatoria.")]
        public string PasswordInicial { get; set; }

        [BindProperty]
        public bool ForzarCambio { get; set; }

        public List<SelectListItem> Roles { get; set; } = new();

        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 1007)
                return RedirectToPage("/Login");

            await LoadRolesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Debug.WriteLine("?? El m�todo OnPostAsync se ejecut�");
            await LoadRolesAsync();

            if (!ModelState.IsValid)
            {
                // ?? Mostrar en consola los errores de validaci�n
                foreach (var entry in ModelState)
                {
                    if (entry.Value.Errors.Any())
                    {
                        Debug.WriteLine($"?? Error en el campo: {entry.Key}");
                        foreach (var error in entry.Value.Errors)
                        {
                            Debug.WriteLine($"    ? {error.ErrorMessage}");
                        }
                    }
                }

                MensajeError = "Formulario inv�lido.";
                return Page();
            }

            try
            {
                using var sha1 = SHA1.Create();
                Usuario.pass = sha1.ComputeHash(Encoding.UTF8.GetBytes(PasswordInicial));
                Usuario.DefaultPassw = ForzarCambio ? 1 : 0;
                Usuario.CambioPass = null;
                Usuario.TokenRecuperacion = null;

                _context.TblUsuarios.Add(Usuario);
                var filas = await _context.SaveChangesAsync();

                if (filas == 0)
                {
                    MensajeError = "No se guard� ning�n registro.";
                    return Page();
                }

                return RedirectToPage("/IT/Usuarios");
            }
            catch (Exception ex)
            {
                MensajeError = "Error al guardar: " + ex.Message;
                return Page();
            }
        }


        private async Task LoadRolesAsync()
        {
            Roles = await _context.TblRolusuario
                .Select(r => new SelectListItem
                {
                    Value = r.idRol.ToString(),
                    Text = r.RolNombre
                })
                .ToListAsync();
        }
        private bool EsContrase�aSegura(string contrase�a)
        {
            if (string.IsNullOrEmpty(contrase�a)) return false;

            bool tieneLetra = contrase�a.Any(char.IsLetter);
            bool tieneNumero = contrase�a.Any(char.IsDigit);
            bool tieneSimbolo = contrase�a.Any(c => !char.IsLetterOrDigit(c));
            bool longitudValida = contrase�a.Length >= 8;

            return tieneLetra && tieneNumero && tieneSimbolo && longitudValida;
        }

    }
}
