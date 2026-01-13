using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ProyectoRH2025.Pages.Emergencia
{
    public class PaseListaModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaseListaModel> _logger;

        public PaseListaModel(IConfiguration configuration, ILogger<PaseListaModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public List<PersonalItem> Personal { get; set; } = new();
        public string? Mensaje { get; set; }

        // Estadísticas
        public int Total { get; set; }
        public int Presentes { get; set; }
        public int Permisos { get; set; }
        public int Ausentes { get; set; }
        public int Pendientes { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var usuarioId = HttpContext.Session.GetInt32("idUsuario");
            if (!usuarioId.HasValue)
                return RedirectToPage("/Login");

            try
            {
                await CargarPersonal();
                await CargarEstadisticas();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando lista");
                Mensaje = "? Error al cargar datos";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostActualizarStatusAsync(int id, byte status)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using var command = new SqlCommand("sp_ActualizarStatusEmergencia", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@NuevoStatus", status);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando status");
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostReiniciarAsync()
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using var command = new SqlCommand("sp_ReiniciarStatusEmergencia", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return new JsonResult(new { success = true, mensaje = "? Todos los status reiniciados" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reiniciando");
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        private async Task CargarPersonal()
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var command = new SqlCommand("sp_ObtenerListaEmergencia", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Personal.Add(new PersonalItem
                {
                    Id = reader.GetInt32(0),
                    Reloj = reader.GetInt32(1),
                    Nombre = reader.GetString(2),
                    Apellido = reader.GetString(3),
                    Puesto = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Departamento = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    ReportaA = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Prioridad = reader.GetInt32(7),
                    Status = reader.GetByte(8)
                });
            }
        }

        private async Task CargarEstadisticas()
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var command = new SqlCommand("sp_ObtenerEstadisticasEmergencia", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                Total = reader.GetInt32(0);
                Presentes = reader.GetInt32(1);
                Permisos = reader.GetInt32(2);
                Ausentes = reader.GetInt32(3);
                Pendientes = reader.GetInt32(4);
            }
        }

        public class PersonalItem
        {
            public int Id { get; set; }
            public int Reloj { get; set; }
            public string Nombre { get; set; } = "";
            public string Apellido { get; set; } = "";
            public string Puesto { get; set; } = "";
            public string Departamento { get; set; } = "";
            public string ReportaA { get; set; } = "";
            public int Prioridad { get; set; }
            public byte Status { get; set; } // 0=Pendiente, 1=Presente, 2=Permiso, 3=Ausente
        }
    }
}