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

        // Propiedades para mensajes (mantienen la misma interfaz)
        public string SearchMessage { get; set; }
        public string SearchType { get; set; }

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

            // Buscar empleados usando stored procedure
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

                // Convertir tu lógica a parámetros del SP
                bool cheboxStil = _selectedCompany == 1;
                bool cheboxAkna = _selectedCompany == 2;

                // Parámetros del SP (adaptando tu lógica original)
                command.Parameters.AddWithValue("@IdUsuario", HttpContext.Session.GetInt32("idUsuario") ?? 1);
                command.Parameters.AddWithValue("@CheboxStil", cheboxStil);
                command.Parameters.AddWithValue("@CheboxAkna", cheboxAkna);
                command.Parameters.AddWithValue("@TxtConsulta", (object?)SearchTerm ?? DBNull.Value);
                command.Parameters.AddWithValue("@Tipempleado", 1); // Ajustar según tu lógica
                command.Parameters.AddWithValue("@PageNumber", 1);
                command.Parameters.AddWithValue("@PageSize", 1000); // Cargar todos para mantener interfaz

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var empleadosTemp = new List<Empleado>();

                // Leer empleados y convertir a tu modelo Empleado
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
                        Status = reader.IsDBNull("Status") ? null : reader.GetInt32("Status"),
                        TipEmpleado = reader.IsDBNull("TipEmpleado") ? null : reader.GetInt32("TipEmpleado"),
                        FechaAlta = reader.IsDBNull("FechaAlta") ? null : reader.GetDateTime("FechaAlta"),
                        IdUsuarioAlta = reader.IsDBNull("IdUsuarioAlta") ? null : reader.GetInt32("IdUsuarioAlta"),
                        Editor = reader.IsDBNull("Editor") ? null : reader.GetInt32("Editor"),
                        // Campos adicionales que pueden venir del SP
                        Fnacimiento = reader.IsDBNull("Fnacimiento") ? null : reader.GetDateTime("Fnacimiento"),
                        Puesto = reader.IsDBNull("Puesto") ? null : reader.GetInt32("Puesto"),
                        Fegreso = reader.IsDBNull("Fegreso") ? null : reader.GetDateTime("Fegreso"),
                        TelEmergencia = reader.IsDBNull("TelEmergencia") ? null : reader.GetString("TelEmergencia"),
                        Jinmediato = reader.IsDBNull("Jinmediato") ? null : reader.GetInt32("Jinmediato")
                    });
                }

                Empleados = empleadosTemp;

                // Saltar las otras tablas del SP (conteo y permisos) ya que no se usan en esta interfaz
                // pero si las necesitas, puedes leerlas aquí con reader.NextResult()
            }
            catch (Exception ex)
            {
                // Log del error si es necesario
                Console.WriteLine($"Error en búsqueda: {ex.Message}");
                Empleados = new List<Empleado>();
            }
        }
    }
}