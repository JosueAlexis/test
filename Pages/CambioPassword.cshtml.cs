using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace ProyectoRH2025.Pages
{
    public class CambioPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CambioPasswordModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        [Required(ErrorMessage = "La contrase�a actual es obligatoria.")]
        public string PasswordActual { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "La nueva contrase�a es obligatoria.")]
        public string PasswordNueva { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Confirma la nueva contrase�a.")]
        [Compare(nameof(PasswordNueva), ErrorMessage = "Las contrase�as no coinciden.")]
        public string ConfirmarPassword { get; set; }

        public string? MensajeError { get; set; }
        public string? MensajeExito { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                MensajeError = "Por favor completa todos los campos correctamente.";
                return Page();
            }

            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null)
            {
                MensajeError = "Sesi�n expirada. Por favor inicia sesi�n nuevamente.";
                return Page();
            }

            var usuario = await _context.TblUsuarios.FindAsync(idUsuario);
            if (usuario == null)
            {
                MensajeError = "Usuario no encontrado.";
                return Page();
            }

            using var sha1 = SHA1.Create();
            var hashActual = sha1.ComputeHash(Encoding.UTF8.GetBytes(PasswordActual));

            if (!usuario.pass.SequenceEqual(hashActual))
            {
                MensajeError = "La contrase�a actual es incorrecta.";
                return Page();
            }

            if (!EsContrase�aSegura(PasswordNueva))
            {
                MensajeError = "La nueva contrase�a debe tener al menos 8 caracteres e incluir letras, n�meros y s�mbolos.";
                return Page();
            }

            var hashNuevo = sha1.ComputeHash(Encoding.UTF8.GetBytes(PasswordNueva));

            var historico = await _context.TblHistoricoPass
                .Where(h => h.idUsuario == idUsuario)
                .OrderByDescending(h => h.ID)
                .Take(5)
                .Select(h => h.PassAnterior)
                .ToListAsync();

            if (historico.Any(h => h.SequenceEqual(hashNuevo)))
            {
                MensajeError = "No puedes reutilizar una de tus �ltimas 5 contrase�as.";
                return Page();
            }

            _context.TblHistoricoPass.Add(new TblHistoricoPass
            {
                idUsuario = usuario.idUsuario,
                PassAnterior = usuario.pass
            });

            usuario.pass = hashNuevo;
            usuario.DefaultPassw = 0;
            usuario.CambioPass = DateTime.Now;

            await _context.SaveChangesAsync();

            // Limpiar contrase�as viejas (dejar solo 5)
            var contrase�asViejas = await _context.TblHistoricoPass
                .Where(h => h.idUsuario == usuario.idUsuario)
                .OrderByDescending(h => h.ID)
                .Skip(5)
                .ToListAsync();

            if (contrase�asViejas.Any())
            {
                _context.TblHistoricoPass.RemoveRange(contrase�asViejas);
                await _context.SaveChangesAsync();
            }

            MensajeExito = "La contrase�a fue actualizada exitosamente.";
            return Page();
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
