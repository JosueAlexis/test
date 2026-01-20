using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;

namespace ProyectoRH2025.Pages.IT
{
    public class AdministrarCuentasModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AdministrarCuentasModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<CuentaVista> Cuentas { get; set; } = new();

        [TempData]
        public string MensajeExito { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 7) return RedirectToPage("/Login");

            await CargarCuentasAsync();
            return Page();
        }

        private async Task CargarCuentasAsync()
        {
            Cuentas = await _context.TblCuentas
                .Select(c => new CuentaVista
                {
                    Id = c.Id,
                    CodigoCuenta = c.CodigoCuenta,
                    NombreCuenta = c.NombreCuenta,
                    Descripcion = c.Descripcion,
                    ColorHex = c.ColorHex,
                    EsActiva = c.EsActiva,
                    OrdenVisualizacion = c.OrdenVisualizacion,
                    UsuariosAsignados = _context.TblUsuariosCuentas
                        .Count(uc => uc.IdCuenta == c.Id && uc.EsActivo)
                })
                .OrderBy(c => c.OrdenVisualizacion)
                .ThenBy(c => c.NombreCuenta)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCrearCuentaAsync(
            string CodigoCuenta, string NombreCuenta, string? Descripcion,
            string? ColorHex, int OrdenVisualizacion)
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) return RedirectToPage("/Login");

            if (await _context.TblCuentas.AnyAsync(c => c.CodigoCuenta == CodigoCuenta))
            {
                MensajeError = "Ya existe una cuenta con ese código";
                return RedirectToPage();
            }

            var cuenta = new TblCuentas
            {
                CodigoCuenta = CodigoCuenta.ToUpper().Trim(),
                NombreCuenta = NombreCuenta.Trim(),
                Descripcion = Descripcion,
                ColorHex = ColorHex,
                OrdenVisualizacion = OrdenVisualizacion,
                CreadoPor = idUsuario.Value
            };

            _context.TblCuentas.Add(cuenta);
            await _context.SaveChangesAsync();

            MensajeExito = $"Cuenta {cuenta.NombreCuenta} creada exitosamente";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleEstadoAsync(int IdCuenta, bool Activar)
        {
            var cuenta = await _context.TblCuentas.FindAsync(IdCuenta);
            if (cuenta == null)
                return new JsonResult(new { success = false, message = "Cuenta no encontrada" });

            cuenta.EsActiva = Activar;
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = $"Cuenta {(Activar ? "activada" : "desactivada")} correctamente"
            });
        }

        public class CuentaVista
        {
            public int Id { get; set; }
            public string CodigoCuenta { get; set; }
            public string NombreCuenta { get; set; }
            public string? Descripcion { get; set; }
            public string? ColorHex { get; set; }
            public bool EsActiva { get; set; }
            public int OrdenVisualizacion { get; set; }
            public int UsuariosAsignados { get; set; }
        }
    }
}