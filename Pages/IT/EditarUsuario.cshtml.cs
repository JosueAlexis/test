using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace ProyectoRH2025.Pages.IT
{
    public class EditarUsuarioModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly PermisosService _permisosService;

        public EditarUsuarioModel(ApplicationDbContext context, PermisosService permisosService)
        {
            _context = context;
            _permisosService = permisosService;
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

        // ✅ PERMISOS
        [BindProperty]
        public List<int> PermisosSeleccionados { get; set; } = new();

        public List<ModuloConOpciones> ModulosConOpciones { get; set; } = new();

        // ✅ CUENTAS
        [BindProperty]
        public List<int> CuentasSeleccionadas { get; set; } = new();

        public List<CuentaDisponible> CuentasDisponibles { get; set; } = new();

        public List<SelectListItem> Roles { get; set; } = new();
        public string? MensajeError { get; set; }
        public string? MensajeExito { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            // Verificar permisos
            var rol = HttpContext.Session.GetInt32("idRol");
            if (!_permisosService.EsAdministrador(rol))
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
            await CargarModulosYPermisos();
            await CargarCuentas();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Verificar permisos
            var rol = HttpContext.Session.GetInt32("idRol");
            if (!_permisosService.EsAdministrador(rol))
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
                    await CargarModulosYPermisos();
                    await CargarCuentas();
                    return Page();
                }

                if (NuevaPassword != ConfirmarPassword)
                {
                    MensajeError = "Las contraseñas no coinciden.";
                    await CargarRoles();
                    await CargarModulosYPermisos();
                    await CargarCuentas();
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
                    await CargarModulosYPermisos();
                    await CargarCuentas();
                    return Page();
                }

                // Verificar que el nombre de usuario no esté siendo usado por otro usuario
                var usuarioDuplicado = await _context.TblUsuarios
                    .AnyAsync(u => u.UsuarioNombre == Usuario.UsuarioNombre && u.idUsuario != Usuario.idUsuario);

                if (usuarioDuplicado)
                {
                    MensajeError = "Ya existe otro usuario con ese nombre.";
                    await CargarRoles();
                    await CargarModulosYPermisos();
                    await CargarCuentas();
                    return Page();
                }

                // Verificar que no se esté eliminando el último administrador
                if (usuarioExistente.Status == 1 && Usuario.Status == 0)
                {
                    if (_permisosService.EsAdministrador(usuarioExistente.idRol))
                    {
                        var cantidadAdminsActivos = await _context.TblUsuarios
                            .Where(u => (u.idRol == 5 || u.idRol == 7 || u.idRol == 1007)
                                   && u.Status == 1
                                   && u.idUsuario != Usuario.idUsuario)
                            .CountAsync();

                        if (cantidadAdminsActivos == 0)
                        {
                            MensajeError = "No se puede desactivar el último administrador del sistema.";
                            await CargarRoles();
                            await CargarModulosYPermisos();
                            await CargarCuentas();
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
                usuarioExistente.DefaultPassw = ForzarCambioPassword ? 1 : 0;

                // ✅ GUARDAR PERMISOS
                await GuardarPermisos();

                // ✅ GUARDAR CUENTAS
                await GuardarCuentas();

                await _context.SaveChangesAsync();

                MensajeExito = "Usuario actualizado exitosamente (Permisos y Cuentas guardados).";

                NuevaPassword = string.Empty;
                ConfirmarPassword = string.Empty;

                await CargarRoles();
                await CargarModulosYPermisos();
                await CargarCuentas();
                return Page();
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al actualizar usuario: {ex.Message}";
                await CargarRoles();
                await CargarModulosYPermisos();
                await CargarCuentas();
                return Page();
            }
        }

        // ========================================
        // MÉTODOS AUXILIARES
        // ========================================

        private async Task CargarModulosYPermisos()
        {
            // ✅ CARGA DINÁMICA: Traemos TODOS los módulos de la BD ordenados alfabéticamente
            var modulos = await _context.TblModulo
                .OrderBy(m => m.ModuloNombre)
                .ToListAsync();

            // Traemos TODAS las opciones
            var opciones = await _context.TblOpcion
                .OrderBy(o => o.ModID)
                .ThenBy(o => o.OpcNombre)
                .ToListAsync();

            // Obtenemos los permisos que el ROL ya tiene asignados
            var permisosActuales = await _context.TblPermiso
                .Where(p => p.idRolUsua == Usuario.idRol && p.Permiso == true)
                .Select(p => p.idOpcion)
                .ToListAsync();

            // Construimos las tarjetas visuales (solo de módulos que tengan al menos 1 opción)
            ModulosConOpciones = modulos.Select(m => new ModuloConOpciones
            {
                IdModulo = m.idModulo,
                NombreModulo = m.ModuloNombre,
                Opciones = opciones
                    .Where(o => o.ModID == m.idModulo)
                    .Select(o => new OpcionConPermiso
                    {
                        IdOpcion = o.idOpcion,
                        NombreOpcion = o.OpcNombre,
                        TienePermiso = permisosActuales.Contains(o.idOpcion)
                    }).ToList()
            }).Where(m => m.Opciones.Any()).ToList();

            PermisosSeleccionados = permisosActuales;
        }

        private async Task GuardarPermisos()
        {
            await _permisosService.AsignarPermisosAsync(Usuario.idRol, PermisosSeleccionados ?? new List<int>());
        }

        private async Task CargarCuentas()
        {
            var todasCuentas = await _context.TblCuentas
                .Where(c => c.EsActiva)
                .OrderBy(c => c.OrdenVisualizacion)
                .ThenBy(c => c.NombreCuenta)
                .ToListAsync();

            var cuentasAsignadas = await _context.TblUsuariosCuentas
                .Where(uc => uc.IdUsuario == Usuario.idUsuario && uc.EsActivo)
                .Select(uc => uc.IdCuenta)
                .ToListAsync();

            CuentasDisponibles = todasCuentas.Select(c => new CuentaDisponible
            {
                IdCuenta = c.Id,
                CodigoCuenta = c.CodigoCuenta,
                NombreCuenta = c.NombreCuenta,
                ColorHex = c.ColorHex,
                EstaAsignada = cuentasAsignadas.Contains(c.Id)
            }).ToList();

            CuentasSeleccionadas = cuentasAsignadas;
        }

        private async Task GuardarCuentas()
        {
            var idUsuarioActual = HttpContext.Session.GetInt32("idUsuario") ?? 0;

            var asignacionesActuales = await _context.TblUsuariosCuentas
                .Where(uc => uc.IdUsuario == Usuario.idUsuario)
                .ToListAsync();

            var cuentasActuales = asignacionesActuales
                .Where(a => a.EsActivo)
                .Select(a => a.IdCuenta)
                .ToList();

            var cuentasNuevas = CuentasSeleccionadas ?? new List<int>();

            var cuentasAgregar = cuentasNuevas.Except(cuentasActuales).ToList();
            var cuentasRemover = cuentasActuales.Except(cuentasNuevas).ToList();

            foreach (var idCuenta in cuentasAgregar)
            {
                var asignacionPrevia = asignacionesActuales
                    .FirstOrDefault(a => a.IdCuenta == idCuenta && !a.EsActivo);

                if (asignacionPrevia != null)
                {
                    asignacionPrevia.EsActivo = true;
                    asignacionPrevia.FechaAsignacion = DateTime.Now;
                    asignacionPrevia.AsignadoPor = idUsuarioActual;
                    asignacionPrevia.FechaDesactivacion = null;
                    asignacionPrevia.DesactivadoPor = null;
                }
                else
                {
                    await _context.TblUsuariosCuentas.AddAsync(new TblUsuariosCuentas
                    {
                        IdUsuario = Usuario.idUsuario,
                        IdCuenta = idCuenta,
                        FechaAsignacion = DateTime.Now,
                        AsignadoPor = idUsuarioActual,
                        EsActivo = true
                    });
                }
            }

            foreach (var idCuenta in cuentasRemover)
            {
                var asignacion = asignacionesActuales
                    .FirstOrDefault(a => a.IdCuenta == idCuenta && a.EsActivo);

                if (asignacion != null)
                {
                    asignacion.EsActivo = false;
                    asignacion.FechaDesactivacion = DateTime.Now;
                    asignacion.DesactivadoPor = idUsuarioActual;
                }
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

    public class ModuloConOpciones
    {
        public int IdModulo { get; set; }
        public string NombreModulo { get; set; } = string.Empty;
        public List<OpcionConPermiso> Opciones { get; set; } = new();
    }

    public class OpcionConPermiso
    {
        public int IdOpcion { get; set; }
        public string NombreOpcion { get; set; } = string.Empty;
        public bool TienePermiso { get; set; }
    }

    public class CuentaDisponible
    {
        public int IdCuenta { get; set; }
        public string CodigoCuenta { get; set; } = string.Empty;
        public string NombreCuenta { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public bool EstaAsignada { get; set; }
    }
}