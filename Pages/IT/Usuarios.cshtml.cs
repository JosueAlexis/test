using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;

namespace ProyectoRH2025.Pages.IT
{
    public class UsuariosModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public UsuariosModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<UsuarioVista> Usuarios { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Filtro { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FiltroRol { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FiltroEstado { get; set; }

        public SelectList RolesFiltro { get; set; }

        [TempData]
        public string MensajeExito { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Solo IT puede acceder
            var rol = HttpContext.Session.GetInt32("idRol");
            if (rol != 5 && rol != 7 && rol != 1007) // Admin, IT, SuperAdmin
            {
                return RedirectToPage("/Login");
            }

            await CargarUsuariosAsync();
            await CargarRolesAsync();

            return Page();
        }

        private async Task CargarUsuariosAsync()
        {
            var query = _context.TblUsuarios
                .Include(u => u.Rol)
                .Where(u => !u.EsEliminado)
                .AsQueryable();

            // Filtro por texto (usuario o nombre)
            if (!string.IsNullOrWhiteSpace(Filtro))
            {
                query = query.Where(u =>
                    u.UsuarioNombre.Contains(Filtro) ||
                    (u.NombreCompleto != null && u.NombreCompleto.Contains(Filtro)));
            }

            // Filtro por rol
            if (!string.IsNullOrWhiteSpace(FiltroRol) && int.TryParse(FiltroRol, out int idRol))
            {
                query = query.Where(u => u.idRol == idRol);
            }

            // Filtro por estado
            if (!string.IsNullOrWhiteSpace(FiltroEstado) && int.TryParse(FiltroEstado, out int estado))
            {
                query = query.Where(u => u.Status == estado);
            }

            Usuarios = await query
                .Select(u => new UsuarioVista
                {
                    idUsuario = u.idUsuario,
                    UsuarioNombre = u.UsuarioNombre,
                    NombreCompleto = u.NombreCompleto,
                    CorreoElectronico = u.CorreoElectronico,
                    Status = u.Status,
                    idRol = u.idRol,
                    NombreRol = u.Rol.RolNombre,
                    DefaultPassw = u.DefaultPassw,
                    CuentasAsignadas = _context.TblUsuariosCuentas
                        .Count(c => c.IdUsuario == u.idUsuario && c.EsActivo)
                })
                .OrderBy(u => u.UsuarioNombre)
                .ToListAsync();
        }

        private async Task CargarRolesAsync()
        {
            var roles = await _context.TblRolusuario
                .OrderBy(r => r.RolNombre)
                .Select(r => new SelectListItem
                {
                    Value = r.idRol.ToString(),
                    Text = r.RolNombre,
                    Selected = r.idRol.ToString() == FiltroRol
                })
                .ToListAsync();

            RolesFiltro = new SelectList(roles, "Value", "Text");
        }

        // ==========================================
        // HANDLER: GESTIONAR CUENTAS ASIGNADAS
        // ==========================================
        public async Task<IActionResult> OnGetGestionarCuentasAsync(int idUsuario)
        {
            var usuario = await _context.TblUsuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.idUsuario == idUsuario);

            if (usuario == null)
            {
                return Content("<div class='alert alert-danger'>Usuario no encontrado</div>");
            }

            var cuentasAsignadas = await _context.TblUsuariosCuentas
                .Include(c => c.Cuenta)
                .Where(c => c.IdUsuario == idUsuario && c.EsActivo)
                .OrderBy(c => c.Cuenta.NombreCuenta)
                .ToListAsync();

            var idsAsignados = cuentasAsignadas.Select(c => c.IdCuenta).ToList();

            var cuentasDisponibles = await _context.TblCuentas
                .Where(c => c.EsActiva && !idsAsignados.Contains(c.Id))
                .OrderBy(c => c.OrdenVisualizacion)
                .ThenBy(c => c.NombreCuenta)
                .Select(c => new
                {
                    c.Id,
                    c.CodigoCuenta,
                    c.NombreCuenta,
                    c.ColorHex
                })
                .ToListAsync();

            var html = $@"
                <div class='row'>
                    <div class='col-md-6'>
                        <div class='card mb-3'>
                            <div class='card-header bg-success text-white'>
                                <h6 class='mb-0'>
                                    <i class='fas fa-plus-circle me-2'></i>Asignar Nueva Cuenta
                                </h6>
                            </div>
                            <div class='card-body'>
                                <form id='formAsignarCuenta'>
                                    <input type='hidden' name='IdUsuarioSupervisor' value='{idUsuario}' />
                                    <div class='mb-3'>
                                        <label class='form-label'>Seleccionar cuenta</label>
                                        <select name='IdCuentaAsignar' class='form-select' required>
                                            <option value=''>-- Seleccionar cuenta --</option>";

            foreach (var cuenta in cuentasDisponibles)
            {
                html += $"<option value='{cuenta.Id}'>{cuenta.CodigoCuenta} - {cuenta.NombreCuenta}</option>";
            }

            html += $@"
                                        </select>
                                    </div>
                                    <div class='alert alert-info'>
                                        <i class='fas fa-info-circle me-2'></i>
                                        <small>
                                            El usuario <strong>{usuario.UsuarioNombre}</strong> ({usuario.Rol?.RolNombre}) 
                                            solo podrá ver las operaciones de las cuentas que le asignes.
                                        </small>
                                    </div>
                                    <button type='button' onclick='asignarCuenta({idUsuario})' class='btn btn-success w-100'>
                                        <i class='fas fa-check'></i> Asignar Cuenta
                                    </button>
                                </form>
                            </div>
                        </div>
                    </div>
                    <div class='col-md-6'>
                        <div class='card'>
                            <div class='card-header bg-info text-white'>
                                <h6 class='mb-0'>
                                    <i class='fas fa-list me-2'></i>Cuentas Asignadas ({cuentasAsignadas.Count})
                                </h6>
                            </div>
                            <div class='card-body' style='max-height: 400px; overflow-y: auto;'>";

            if (cuentasAsignadas.Any())
            {
                html += "<div class='list-group'>";
                foreach (var asignacion in cuentasAsignadas)
                {
                    var colorBox = !string.IsNullOrEmpty(asignacion.Cuenta.ColorHex)
                        ? $"<div style='width:30px; height:30px; background-color:{asignacion.Cuenta.ColorHex}; border-radius:4px; border:1px solid #dee2e6; display:inline-block; margin-right:10px;'></div>"
                        : "";

                    html += $@"
                                <div class='list-group-item d-flex justify-content-between align-items-center'>
                                    <div class='d-flex align-items-center'>
                                        {colorBox}
                                        <div>
                                            <strong>{asignacion.Cuenta.NombreCuenta}</strong><br/>
                                            <small class='text-muted'>
                                                <span class='badge bg-secondary'>{asignacion.Cuenta.CodigoCuenta}</span>
                                            </small><br/>
                                            <small class='text-muted'>
                                                <i class='fas fa-calendar'></i> Asignado: {asignacion.FechaAsignacion:dd/MM/yyyy}
                                            </small>
                                        </div>
                                    </div>
                                    <button type='button' 
                                            onclick='desasignarCuenta({asignacion.Id}, ""{asignacion.Cuenta.NombreCuenta}"")' 
                                            class='btn btn-sm btn-outline-danger'
                                            title='Quitar asignación'>
                                        <i class='fas fa-times'></i>
                                    </button>
                                </div>";
                }
                html += "</div>";
            }
            else
            {
                html += @"
                                <div class='text-center text-muted py-4'>
                                    <i class='fas fa-inbox fa-3x mb-2 opacity-50'></i>
                                    <p>No hay cuentas asignadas</p>
                                </div>";
            }

            html += @"
                            </div>
                        </div>
                    </div>
                </div>
                <script>
                    async function asignarCuenta(idSupervisor) {
                        const form = document.getElementById('formAsignarCuenta');
                        const formData = new FormData(form);
                        const idCuenta = formData.get('IdCuentaAsignar');
                        
                        if (!idCuenta) {
                            Swal.fire('Error', 'Selecciona una cuenta', 'error');
                            return;
                        }

                        try {
                            const response = await fetch('/IT/Usuarios?handler=AsignarCuenta', {
                                method: 'POST',
                                headers: {
                                    'Content-Type': 'application/x-www-form-urlencoded',
                                    'RequestVerificationToken': document.querySelector('input[name=""__RequestVerificationToken""]').value
                                },
                                body: new URLSearchParams(formData)
                            });

                            const result = await response.json();
                            
                            if (result.success) {
                                Swal.fire('Éxito', result.message, 'success').then(() => location.reload());
                            } else {
                                Swal.fire('Error', result.message, 'error');
                            }
                        } catch (error) {
                            Swal.fire('Error', 'No se pudo completar la operación', 'error');
                        }
                    }

                    async function desasignarCuenta(idAsignacion, nombreCuenta) {
                        const confirmacion = await Swal.fire({
                            title: '¿Confirmar desasignación?',
                            html: `Se quitará el acceso a la cuenta <strong>${nombreCuenta}</strong>`,
                            icon: 'warning',
                            showCancelButton: true,
                            confirmButtonColor: '#dc3545',
                            cancelButtonColor: '#6c757d',
                            confirmButtonText: 'Sí, desasignar',
                            cancelButtonText: 'Cancelar'
                        });

                        if (!confirmacion.isConfirmed) return;

                        try {
                            const formData = new FormData();
                            formData.append('IdAsignacion', idAsignacion);

                            const response = await fetch('/IT/Usuarios?handler=DesasignarCuenta', {
                                method: 'POST',
                                headers: {
                                    'RequestVerificationToken': document.querySelector('input[name=""__RequestVerificationToken""]').value
                                },
                                body: formData
                            });

                            const result = await response.json();
                            
                            if (result.success) {
                                Swal.fire('Éxito', result.message, 'success').then(() => location.reload());
                            } else {
                                Swal.fire('Error', result.message, 'error');
                            }
                        } catch (error) {
                            Swal.fire('Error', 'No se pudo completar la operación', 'error');
                        }
                    }
                </script>
            ";

            return Content(html, "text/html");
        }

        // ==========================================
        // HANDLER: VER CUENTAS (SOLO LECTURA)
        // ==========================================
        public async Task<IActionResult> OnGetVerCuentasAsync(int idUsuario)
        {
            var cuentasAsignadas = await _context.TblUsuariosCuentas
                .Include(c => c.Cuenta)
                .Where(c => c.IdUsuario == idUsuario && c.EsActivo)
                .OrderBy(c => c.Cuenta.NombreCuenta)
                .ToListAsync();

            var html = "<div class='list-group'>";

            if (cuentasAsignadas.Any())
            {
                foreach (var asignacion in cuentasAsignadas)
                {
                    var colorBox = !string.IsNullOrEmpty(asignacion.Cuenta.ColorHex)
                        ? $"<div style='width:30px; height:30px; background-color:{asignacion.Cuenta.ColorHex}; border-radius:4px; border:1px solid #dee2e6; display:inline-block; margin-right:10px;'></div>"
                        : "";

                    html += $@"
                        <div class='list-group-item'>
                            <div class='d-flex align-items-center'>
                                {colorBox}
                                <div>
                                    <h6 class='mb-1'>{asignacion.Cuenta.NombreCuenta}</h6>
                                    <small class='text-muted'>
                                        <span class='badge bg-secondary'>{asignacion.Cuenta.CodigoCuenta}</span>
                                        • Asignado: {asignacion.FechaAsignacion:dd/MM/yyyy HH:mm}
                                    </small>
                                </div>
                            </div>
                        </div>";
                }
            }
            else
            {
                html += @"
                    <div class='text-center text-muted py-4'>
                        <i class='fas fa-inbox fa-3x mb-2 opacity-50'></i>
                        <p>No hay cuentas asignadas</p>
                    </div>";
            }

            html += "</div>";
            return Content(html, "text/html");
        }

        // ==========================================
        // HANDLER: ASIGNAR CUENTA
        // ==========================================
        [BindProperty]
        public int IdUsuarioSupervisor { get; set; }

        [BindProperty]
        public int IdCuentaAsignar { get; set; }

        public async Task<IActionResult> OnPostAsignarCuentaAsync()
        {
            var idUsuarioIT = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuarioIT == null)
            {
                return new JsonResult(new { success = false, message = "Sesión expirada" });
            }

            var supervisor = await _context.TblUsuarios
                .FirstOrDefaultAsync(u => u.idUsuario == IdUsuarioSupervisor && !u.EsEliminado);

            if (supervisor == null)
            {
                return new JsonResult(new { success = false, message = "Usuario no válido" });
            }

            var cuenta = await _context.TblCuentas
                .FirstOrDefaultAsync(c => c.Id == IdCuentaAsignar && c.EsActiva);

            if (cuenta == null)
            {
                return new JsonResult(new { success = false, message = "Cuenta no válida o inactiva" });
            }

            var existeAsignacion = await _context.TblUsuariosCuentas
                .AnyAsync(c => c.IdUsuario == IdUsuarioSupervisor &&
                              c.IdCuenta == IdCuentaAsignar &&
                              c.EsActivo);

            if (existeAsignacion)
            {
                return new JsonResult(new { success = false, message = "Esta cuenta ya está asignada" });
            }

            var asignacion = new TblUsuariosCuentas
            {
                IdUsuario = IdUsuarioSupervisor,
                IdCuenta = IdCuentaAsignar,
                FechaAsignacion = DateTime.Now,
                AsignadoPor = idUsuarioIT.Value,
                EsActivo = true
            };

            _context.TblUsuariosCuentas.Add(asignacion);
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = $"Cuenta {cuenta.NombreCuenta} asignada correctamente"
            });
        }

        // ==========================================
        // HANDLER: DESASIGNAR CUENTA
        // ==========================================
        [BindProperty]
        public int IdAsignacion { get; set; }

        public async Task<IActionResult> OnPostDesasignarCuentaAsync()
        {
            var idUsuarioIT = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuarioIT == null)
            {
                return new JsonResult(new { success = false, message = "Sesión expirada" });
            }

            var asignacion = await _context.TblUsuariosCuentas
                .Include(c => c.Cuenta)
                .FirstOrDefaultAsync(c => c.Id == IdAsignacion);

            if (asignacion == null)
            {
                return new JsonResult(new { success = false, message = "Asignación no encontrada" });
            }

            asignacion.EsActivo = false;
            asignacion.FechaDesactivacion = DateTime.Now;
            asignacion.DesactivadoPor = idUsuarioIT.Value;

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = $"Cuenta {asignacion.Cuenta.NombreCuenta} desasignada correctamente"
            });
        }

        // ==========================================
        // ✅ HANDLER: ELIMINAR USUARIO (JSON RESPONSE)
        // ==========================================
        [BindProperty]
        public int IdUsuario { get; set; }

        [BindProperty]
        public string MotivoEliminacion { get; set; }

        public async Task<IActionResult> OnPostEliminarUsuarioAsync()
        {
            var idUsuarioIT = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuarioIT == null)
            {
                return new JsonResult(new { success = false, message = "Sesión expirada" });
            }

            try
            {
                // ✅ EJECUTAR STORED PROCEDURE
                var nombreUsuarioParam = new SqlParameter
                {
                    ParameterName = "@NombreUsuario",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    Size = 200,
                    Direction = System.Data.ParameterDirection.Output
                };

                var successParam = new SqlParameter
                {
                    ParameterName = "@Success",
                    SqlDbType = System.Data.SqlDbType.Bit,
                    Direction = System.Data.ParameterDirection.Output
                };

                var messageParam = new SqlParameter
                {
                    ParameterName = "@Message",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    Size = 500,
                    Direction = System.Data.ParameterDirection.Output
                };

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_EliminarUsuario @IdUsuario, @IdUsuarioIT, @MotivoEliminacion, @NombreUsuario OUTPUT, @Success OUTPUT, @Message OUTPUT",
                    new SqlParameter("@IdUsuario", IdUsuario),
                    new SqlParameter("@IdUsuarioIT", idUsuarioIT.Value),
                    new SqlParameter("@MotivoEliminacion", (object)MotivoEliminacion ?? DBNull.Value),
                    nombreUsuarioParam,
                    successParam,
                    messageParam
                );

                var success = (bool)successParam.Value;
                var message = messageParam.Value?.ToString() ?? "Error desconocido";

                return new JsonResult(new
                {
                    success = success,
                    message = message
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        public class UsuarioVista
        {
            public int idUsuario { get; set; }
            public string UsuarioNombre { get; set; }
            public string? NombreCompleto { get; set; }
            public string? CorreoElectronico { get; set; }
            public int Status { get; set; }
            public int idRol { get; set; }
            public string NombreRol { get; set; }
            public int? DefaultPassw { get; set; }
            public int CuentasAsignadas { get; set; }
        }
    }
    public class ResultadoEliminarUsuario
    {
        public string NombreUsuario { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}