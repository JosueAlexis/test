using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;

namespace ProyectoRH2025.Services
{
    /// <summary>
    /// Servicio centralizado para gestión de permisos y validación de accesos
    /// </summary>
    public class PermisosService
    {
        private readonly ApplicationDbContext _context;

        public PermisosService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========================================
        // VERIFICACIÓN DE PERMISOS
        // ========================================

        /// <summary>
        /// Verifica si un usuario tiene permiso para una opción específica
        /// </summary>
        public async Task<bool> TienePermisoAsync(int idUsuario, string nombreOpcion)
        {
            try
            {
                var usuario = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == idUsuario && u.Status == 1);

                if (usuario == null) return false;

                var opcion = await _context.TblOpcion
                    .FirstOrDefaultAsync(o => o.OpcNombre == nombreOpcion);

                if (opcion == null) return false;

                var tienePermiso = await _context.TblPermiso
                    .AnyAsync(p => p.idRolUsua == usuario.idRol
                                   && p.idOpcion == opcion.idOpcion
                                   && p.Permiso == true);

                return tienePermiso;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si un usuario tiene permiso para un módulo completo
        /// </summary>
        public async Task<bool> TienePermisoModuloAsync(int idUsuario, string nombreModulo)
        {
            try
            {
                var usuario = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == idUsuario && u.Status == 1);

                if (usuario == null) return false;

                var modulo = await _context.TblModulo
                    .FirstOrDefaultAsync(m => m.ModuloNombre == nombreModulo);

                if (modulo == null) return false;

                // Verificar si tiene al menos un permiso en el módulo
                var tienePermiso = await _context.TblPermiso
                    .AnyAsync(p => p.idRolUsua == usuario.idRol
                                   && p.idModulo == modulo.idModulo
                                   && p.Permiso == true);

                return tienePermiso;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si un rol es administrador (IT)
        /// </summary>
        public bool EsAdministrador(int? idRol)
        {
            var rolesIT = new[] { 5, 7, 1007 }; // Admin, IT, SuperAdmin
            return idRol.HasValue && rolesIT.Contains(idRol.Value);
        }

        /// <summary>
        /// Verifica si un usuario tiene acceso a una cuenta específica
        /// </summary>
        public async Task<bool> TieneAccesoCuentaAsync(int idUsuario, int idCuenta)
        {
            try
            {
                var usuario = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == idUsuario);

                if (usuario == null) return false;

                // Los administradores tienen acceso a todas las cuentas
                if (EsAdministrador(usuario.idRol)) return true;

                // Verificar asignación específica
                var tieneAcceso = await _context.TblUsuariosCuentas
                    .AnyAsync(uc => uc.IdUsuario == idUsuario
                                    && uc.IdCuenta == idCuenta
                                    && uc.EsActivo);

                return tieneAcceso;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // CONSULTA DE PERMISOS
        // ========================================

        /// <summary>
        /// Obtiene todas las opciones a las que un usuario tiene acceso
        /// </summary>
        public async Task<List<string>> ObtenerOpcionesUsuarioAsync(int idUsuario)
        {
            try
            {
                var usuario = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == idUsuario);

                if (usuario == null) return new List<string>();

                var opciones = await (from p in _context.TblPermiso
                                      join o in _context.TblOpcion on p.idOpcion equals o.idOpcion
                                      where p.idRolUsua == usuario.idRol && p.Permiso == true
                                      select o.OpcNombre).ToListAsync();

                return opciones;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Obtiene todos los módulos a los que un usuario tiene acceso
        /// </summary>
        public async Task<List<string>> ObtenerModulosUsuarioAsync(int idUsuario)
        {
            try
            {
                var usuario = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == idUsuario);

                if (usuario == null) return new List<string>();

                var modulos = await (from p in _context.TblPermiso
                                     join m in _context.TblModulo on p.idModulo equals m.idModulo
                                     where p.idRolUsua == usuario.idRol && p.Permiso == true
                                     select m.ModuloNombre)
                                     .Distinct()
                                     .ToListAsync();

                return modulos;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Obtiene todas las cuentas asignadas a un supervisor/coordinador
        /// </summary>
        public async Task<List<TblCuentas>> ObtenerCuentasUsuarioAsync(int idUsuario)
        {
            try
            {
                var usuario = await _context.TblUsuarios
                    .FirstOrDefaultAsync(u => u.idUsuario == idUsuario);

                if (usuario == null) return new List<TblCuentas>();

                // Los administradores ven todas las cuentas activas
                if (EsAdministrador(usuario.idRol))
                {
                    return await _context.TblCuentas
                        .Where(c => c.EsActiva)
                        .OrderBy(c => c.OrdenVisualizacion)
                        .ThenBy(c => c.NombreCuenta)
                        .ToListAsync();
                }

                // Usuarios normales solo ven sus cuentas asignadas
                var cuentas = await (from uc in _context.TblUsuariosCuentas
                                     join c in _context.TblCuentas on uc.IdCuenta equals c.Id
                                     where uc.IdUsuario == idUsuario
                                           && uc.EsActivo
                                           && c.EsActiva
                                     orderby c.OrdenVisualizacion, c.NombreCuenta
                                     select c).ToListAsync();

                return cuentas;
            }
            catch
            {
                return new List<TblCuentas>();
            }
        }

        /// <summary>
        /// Obtiene los IDs de las cuentas de un usuario (útil para filtros)
        /// </summary>
        public async Task<List<int>> ObtenerIdsCuentasUsuarioAsync(int idUsuario)
        {
            var cuentas = await ObtenerCuentasUsuarioAsync(idUsuario);
            return cuentas.Select(c => c.Id).ToList();
        }

        // ========================================
        // GESTIÓN DE PERMISOS (ADMIN)
        // ========================================

        /// <summary>
        /// Asigna múltiples permisos a un rol
        /// </summary>
        public async Task<bool> AsignarPermisosAsync(int idRol, List<int> idsOpciones)
        {
            try
            {
                // Eliminar permisos existentes
                var permisosExistentes = await _context.TblPermiso
                    .Where(p => p.idRolUsua == idRol)
                    .ToListAsync();

                _context.TblPermiso.RemoveRange(permisosExistentes);

                // Agregar nuevos permisos
                if (idsOpciones != null && idsOpciones.Any())
                {
                    var nuevosPermisos = new List<TblPermiso>();

                    foreach (var idOpcion in idsOpciones)
                    {
                        var opcion = await _context.TblOpcion
                            .FirstOrDefaultAsync(o => o.idOpcion == idOpcion);

                        if (opcion != null)
                        {
                            nuevosPermisos.Add(new TblPermiso
                            {
                                idRolUsua = idRol,
                                idOpcion = idOpcion,
                                idModulo = opcion.ModID,
                                Permiso = true
                            });
                        }
                    }

                    await _context.TblPermiso.AddRangeAsync(nuevosPermisos);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Asigna una cuenta a un supervisor/coordinador
        /// </summary>
        public async Task<bool> AsignarCuentaAsync(int idUsuario, int idCuenta, int asignadoPor)
        {
            try
            {
                // Verificar si ya existe una asignación activa
                var existente = await _context.TblUsuariosCuentas
                    .FirstOrDefaultAsync(uc => uc.IdUsuario == idUsuario
                                               && uc.IdCuenta == idCuenta
                                               && uc.EsActivo);

                if (existente != null)
                {
                    return false; // Ya existe
                }

                var nuevaAsignacion = new TblUsuariosCuentas
                {
                    IdUsuario = idUsuario,
                    IdCuenta = idCuenta,
                    FechaAsignacion = DateTime.Now,
                    AsignadoPor = asignadoPor,
                    EsActivo = true
                };

                await _context.TblUsuariosCuentas.AddAsync(nuevaAsignacion);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Desactiva la asignación de una cuenta a un usuario
        /// </summary>
        public async Task<bool> DesasignarCuentaAsync(int idUsuario, int idCuenta, int desactivadoPor)
        {
            try
            {
                var asignacion = await _context.TblUsuariosCuentas
                    .FirstOrDefaultAsync(uc => uc.IdUsuario == idUsuario
                                               && uc.IdCuenta == idCuenta
                                               && uc.EsActivo);

                if (asignacion == null)
                {
                    return false; // No existe
                }

                asignacion.EsActivo = false;
                asignacion.FechaDesactivacion = DateTime.Now;
                asignacion.DesactivadoPor = desactivadoPor;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // UTILIDADES
        // ========================================

        /// <summary>
        /// Obtiene el nombre del rol de un usuario
        /// </summary>
        public async Task<string> ObtenerNombreRolAsync(int idUsuario)
        {
            try
            {
                var rolNombre = await (from u in _context.TblUsuarios
                                       join r in _context.TblRolusuario on u.idRol equals r.idRol
                                       where u.idUsuario == idUsuario
                                       select r.RolNombre).FirstOrDefaultAsync();

                return rolNombre ?? "Sin rol";
            }
            catch
            {
                return "Sin rol";
            }
        }

        /// <summary>
        /// Valida si un usuario puede acceder a ciertos datos según su cuenta
        /// Útil para filtrar consultas
        /// </summary>
        public async Task<IQueryable<T>> FiltrarPorCuentasUsuarioAsync<T>(
            int idUsuario,
            IQueryable<T> query,
            Func<T, int> selectorCuenta) where T : class
        {
            var usuario = await _context.TblUsuarios
                .FirstOrDefaultAsync(u => u.idUsuario == idUsuario);

            if (usuario == null) return query.Where(_ => false);

            // Admin ve todo
            if (EsAdministrador(usuario.idRol)) return query;

            // Obtener cuentas del usuario
            var idsCuentas = await ObtenerIdsCuentasUsuarioAsync(idUsuario);

            if (!idsCuentas.Any()) return query.Where(_ => false);

            // Filtrar por cuentas
            return query.Where(item => idsCuentas.Contains(selectorCuenta(item)));
        }
    }
}