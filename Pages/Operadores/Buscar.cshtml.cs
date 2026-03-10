using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.Models;
using System.Data;

namespace ProyectoRH2025.Pages.Operadores
{
    public class BuscarModel : PageModel
    {
        private readonly string _connectionString;

        public BuscarModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public IList<Empleado> Empleados { get; set; } = new List<Empleado>();

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }

        private int? _selectedCompany = null;

        [BindProperty(SupportsGet = true)]
        public int SelectedCompany
        {
            get { return _selectedCompany ?? 0; }
            set
            {
                if (value == 1)
                {
                    _selectedCompany = 1;
                }
                else if (value == 2)
                {
                    _selectedCompany = 2;
                }
                else
                {
                    _selectedCompany = null;
                }
            }
        }

        public string SearchMessage { get; set; }
        public string SearchType { get; set; }

        // ✅ Filtro: solo activos (Status=1) o ver todos (incluye bajas)
        [BindProperty(SupportsGet = true)]
        public bool SoloActivos { get; set; } = true;

        private void SetSearchMessage()
        {
            if (string.IsNullOrEmpty(SearchTerm) && !Empleados.Any())
            {
                if (_selectedCompany == null)
                {
                    SearchMessage = "Seleccione una compañía para ver los empleados";
                    SearchType = "info";
                }
                else
                {
                    SearchMessage = "Ingrese un término de búsqueda para filtrar empleados";
                    SearchType = "info";
                }
            }
            else if (!string.IsNullOrEmpty(SearchTerm) && !Empleados.Any())
            {
                SearchMessage = $"No se encontraron empleados que coincidan con '{SearchTerm}'";
                if (_selectedCompany == null)
                {
                    SearchMessage += ". Seleccione una compañía";
                }
                SearchType = "warning";
            }
            else if (Empleados.Any())
            {
                if (!string.IsNullOrEmpty(SearchTerm))
                {
                    SearchMessage = $"Se encontraron {Empleados.Count} empleados que coinciden con '{SearchTerm}'";
                }
                else
                {
                    SearchMessage = $"Se encontraron {Empleados.Count} empleados";
                }
                SearchType = "success";
            }
        }

        public async Task OnGetAsync(int? selectedCompany = null)
        {
            if (selectedCompany.HasValue)
            {
                SelectedCompany = selectedCompany.Value;
            }

            await BuscarEmpleadosConSPAsync();
            SetSearchMessage();
        }

        private async Task BuscarEmpleadosConSPAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand("SP_BuscarEmpleados", connection);

                command.CommandType = CommandType.StoredProcedure;

                bool cheboxStil = _selectedCompany == 1;
                bool cheboxAkna = _selectedCompany == 2;

                command.Parameters.AddWithValue("@IdUsuario", HttpContext.Session.GetInt32("idUsuario") ?? 1);
                command.Parameters.AddWithValue("@CheboxStil", cheboxStil);
                command.Parameters.AddWithValue("@CheboxAkna", cheboxAkna);
                command.Parameters.AddWithValue("@TxtConsulta", (object?)SearchTerm ?? DBNull.Value);
                command.Parameters.AddWithValue("@Tipempleado", 1);
                command.Parameters.AddWithValue("@PageNumber", 1);
                command.Parameters.AddWithValue("@PageSize", 1000);
                command.Parameters.AddWithValue("@SoloActivos", SoloActivos); // ✅ nuevo parámetro

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var empleadosTemp = new List<Empleado>();

                while (await reader.ReadAsync())
                {
                    empleadosTemp.Add(new Empleado
                    {
                        Id = reader.GetInt32("Id"),
                        Reloj = reader.IsDBNull("Reloj") ? null : reader.GetInt32("Reloj"),
                        Names = reader.IsDBNull("Names") ? null : reader.GetString("Names"),
                        Apellido = reader.IsDBNull("Apellido") ? null : reader.GetString("Apellido"),
                        Apellido2 = reader.IsDBNull("Apellido2") ? null : reader.GetString("Apellido2"),
                        Fingreso = reader.IsDBNull("Fingreso") ? null : reader.GetDateTime("Fingreso"),
                        Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                        Telefono = reader.IsDBNull("Telefono") ? null : reader.GetString("Telefono"),
                        Rfc = reader.IsDBNull("Rfc") ? null : reader.GetString("Rfc"),
                        Curp = reader.IsDBNull("Curp") ? null : reader.GetString("Curp"),
                        NumSSocial = reader.IsDBNull("NumSSocial") ? null : reader.GetString("NumSSocial"),
                        CodClientes = reader.IsDBNull("CodClientes") ? null : reader.GetString("CodClientes"),
                        Status = reader.IsDBNull("Status") ? 0 : reader.GetInt32("Status"),
                        TipEmpleado = reader.IsDBNull("TipEmpleado") ? null : reader.GetInt32("TipEmpleado"),
                        FechaAlta = reader.IsDBNull("FechaAlta") ? null : reader.GetDateTime("FechaAlta"),
                        IdUsuarioAlta = reader.IsDBNull("IdUsuarioAlta") ? 0 : reader.GetInt32("IdUsuarioAlta"),
                        Editor = reader.IsDBNull("Editor") ? null : reader.GetInt32("Editor"),
                        Fnacimiento = reader.IsDBNull("Fnacimiento") ? null : reader.GetDateTime("Fnacimiento"),
                        Puesto = reader.IsDBNull("Puesto") ? null : reader.GetInt32("Puesto"),
                        Fegreso = reader.IsDBNull("Fegreso") ? null : reader.GetDateTime("Fegreso"),
                        TelEmergencia = reader.IsDBNull("TelEmergencia") ? null : reader.GetString("TelEmergencia"),
                        Jinmediato = reader.IsDBNull("Jinmediato") ? null : reader.GetInt32("Jinmediato")
                    });
                }

                Empleados = empleadosTemp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en búsqueda: {ex.Message}");
                Empleados = new List<Empleado>();
            }
        }
        // ==========================================
        // HANDLER: DAR DE BAJA (Soft delete - Status = 2)
        // ==========================================
        public async Task<IActionResult> OnPostDarDeBajaAsync(int id, DateTime? fechaBaja, string? motivoBaja)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"UPDATE tblEmpleados 
                            SET Status = 2, 
                                Fegreso = @FechaEgreso,
                                FechaUltimaModificacion = GETDATE(),
                                UsuarioUltimaModificacion = @IdUsuario
                            WHERE Id = @Id";

                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@FechaEgreso", (object?)fechaBaja ?? DateTime.Today);
                cmd.Parameters.AddWithValue("@IdUsuario", HttpContext.Session.GetInt32("idUsuario") ?? 0);

                await cmd.ExecuteNonQueryAsync();

                TempData["Success"] = $"✅ Empleado dado de baja correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al dar de baja: {ex.Message}";
            }

            return RedirectToPage(new { SelectedCompany, SearchTerm, SoloActivos });
        }

        // ==========================================
        // HANDLER: ELIMINAR (Hard delete)
        // ==========================================
        public async Task<IActionResult> OnPostEliminarAsync(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Verificar si tiene asignaciones de sellos activas antes de eliminar
                var sqlCheck = @"SELECT COUNT(*) FROM tblAsigSellos 
                                 WHERE idOperador = @Id AND Status IN (3, 4)";
                using var cmdCheck = new SqlCommand(sqlCheck, connection);
                cmdCheck.Parameters.AddWithValue("@Id", id);
                var count = (int)await cmdCheck.ExecuteScalarAsync();

                if (count > 0)
                {
                    TempData["Error"] = "No se puede eliminar: el empleado tiene sellos activos asignados. Use 'Dar de Baja' en su lugar.";
                    return RedirectToPage(new { SelectedCompany, SearchTerm, SoloActivos });
                }

                var sql = "DELETE FROM tblEmpleados WHERE Id = @Id";
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();

                TempData["Success"] = "✅ Empleado eliminado permanentemente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar: {ex.Message}";
            }

            return RedirectToPage(new { SelectedCompany, SearchTerm, SoloActivos });
        }
    }
}