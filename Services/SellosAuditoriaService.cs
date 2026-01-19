using ProyectoRH2025.Data;
using ProyectoRH2025.Models;

namespace ProyectoRH2025.Services
{
    public class SellosAuditoriaService
    {
        private readonly ApplicationDbContext _context;

        public SellosAuditoriaService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RegistrarAsignacion(
            TblSellos sello,
            int statusAnterior,
            int? supervisorIdAnterior,
            string? supervisorNombreAnterior,
            int? usuarioId,
            string? usuarioNombre,
            string? ip,
            string? comentario = null)
        {
            var historial = new TblSellosHistorial
            {
                SelloId = sello.Id,
                NumeroSello = sello.Sello,
                TipoMovimiento = "Asignacion",
                StatusAnterior = statusAnterior,
                StatusNuevo = sello.Status,
                SupervisorIdAnterior = supervisorIdAnterior,
                SupervisorIdNuevo = sello.SupervisorId,
                SupervisorNombreAnterior = supervisorNombreAnterior,
                SupervisorNombreNuevo = ObtenerNombreSupervisor(sello),
                FechaMovimiento = DateTime.Now,
                FechaAsignacionAnterior = null,
                FechaAsignacionNueva = sello.FechaAsignacion,
                UsuarioId = usuarioId,
                UsuarioNombre = usuarioNombre,
                Comentario = comentario ?? "Asignación de sello",
                IP = ip
            };

            _context.TblSellosHistorial.Add(historial);
        }

        public async Task RegistrarDesasignacion(
            TblSellos sello,
            int statusAnterior,
            int? supervisorIdAnterior,
            string? supervisorNombreAnterior,
            DateTime? fechaAsignacionAnterior,
            int? usuarioId,
            string? usuarioNombre,
            string? ip,
            string? comentario = null)
        {
            var historial = new TblSellosHistorial
            {
                SelloId = sello.Id,
                NumeroSello = sello.Sello,
                TipoMovimiento = "Desasignacion",
                StatusAnterior = statusAnterior,
                StatusNuevo = sello.Status,
                SupervisorIdAnterior = supervisorIdAnterior,
                SupervisorIdNuevo = null,
                SupervisorNombreAnterior = supervisorNombreAnterior,
                SupervisorNombreNuevo = null,
                FechaMovimiento = DateTime.Now,
                FechaAsignacionAnterior = fechaAsignacionAnterior,
                FechaAsignacionNueva = null,
                UsuarioId = usuarioId,
                UsuarioNombre = usuarioNombre,
                Comentario = comentario ?? "Desasignación de sello",
                IP = ip
            };

            _context.TblSellosHistorial.Add(historial);
        }

        public async Task RegistrarImportacion(
            TblSellos sello,
            int? usuarioId,
            string? usuarioNombre,
            string? ip,
            string? comentario = null)
        {
            var historial = new TblSellosHistorial
            {
                SelloId = sello.Id,
                NumeroSello = sello.Sello,
                TipoMovimiento = "Importacion",
                StatusAnterior = 0,
                StatusNuevo = sello.Status,
                SupervisorIdAnterior = null,
                SupervisorIdNuevo = null,
                SupervisorNombreAnterior = null,
                SupervisorNombreNuevo = null,
                FechaMovimiento = DateTime.Now,
                FechaAsignacionAnterior = null,
                FechaAsignacionNueva = null,
                UsuarioId = usuarioId,
                UsuarioNombre = usuarioNombre,
                Comentario = comentario ?? $"Sello importado - Recibido por: {sello.Recibio}",
                IP = ip
            };

            _context.TblSellosHistorial.Add(historial);
        }

        private string? ObtenerNombreSupervisor(TblSellos sello)
        {
            return sello.Supervisor?.NombreCompleto ?? sello.Supervisor?.UsuarioNombre;
        }
    }
}