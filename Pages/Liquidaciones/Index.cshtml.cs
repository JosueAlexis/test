using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.Data;
using ProyectoRH2025.MODELS;
using Microsoft.AspNetCore.Http;
using System.Data;
using System.Diagnostics;

namespace ProyectoRH2025.Pages.Liquidaciones
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public IndexModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IList<LiquidacionViewModel> Liquidaciones { get; set; } = new List<LiquidacionViewModel>();

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        // NUEVO: Filtro específico para Remolque
        [BindProperty(SupportsGet = true)]
        public string? Remolque { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool TodosLosRegistros { get; set; } = false;  // NUEVO para indicar "todos los registros"

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; }
        public byte? StatusFiltro { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ClienteFiltro { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? EvidenciasFiltro { get; set; }

        public DateTime FechaMinimaDisponible { get; set; }
        public DateTime FechaMaximaDisponible { get; set; }
        public int TotalRegistros { get; set; }
        public int RegistrosMostrados { get; set; }
        public bool MostrandoSoloEstaSemana { get; set; }

        // Opciones para los dropdowns
        public List<ClienteOpcion> ClientesDisponibles { get; set; } = new List<ClienteOpcion>();

        // Para mostrar filtros activos
        public string StatusFiltroTexto { get; set; } = "";
        public string EvidenciasFiltroTexto { get; set; } = "";

        // DIAGNÓSTICO
        public string DiagnosticoTiempos { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            var stopwatchTotal = Stopwatch.StartNew();
            var diagnostico = new List<string>();

            // ---- VERIFICACIÓN DE ROL ----
            var rolId = HttpContext.Session.GetInt32("idRol");
            var rolesITPermitidos = new[] { 5, 7, 1007 };
            var idRolLiquidacionesPermitido = 1009;

            bool esLiquidaciones = rolId.HasValue && rolId.Value == idRolLiquidacionesPermitido;
            bool esAdministrativoIT = rolId.HasValue && rolesITPermitidos.Contains(rolId.Value);

            if (!esLiquidaciones && !esAdministrativoIT)
            {
                return RedirectToPage("/Login");
            }

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                // CARGAR OPCIONES DE FILTROS
                var sw0 = Stopwatch.StartNew();
                await CargarOpcionesFiltrosAsync(connectionString);
                sw0.Stop();
                diagnostico.Add($"Opciones filtros: {sw0.ElapsedMilliseconds}ms");

                // STORED PROCEDURE 1: Estadísticas rápidas
                var sw1 = Stopwatch.StartNew();
                await GetEstadisticasRapidoAsync(connectionString);
                sw1.Stop();
                diagnostico.Add($"Estadísticas: {sw1.ElapsedMilliseconds}ms");

                // IMPORTANTE: NO sobrescribir fechas si vienen de la URL
                // Solo establecer fechas por defecto si NO se especificaron Y NO es "Todos los registros"
                if (!FechaInicio.HasValue && !FechaFin.HasValue && !TodosLosRegistros)
                {
                    // SOLO en este caso aplicar fechas por defecto
                    FechaInicio = DateTime.Today;
                    FechaFin = DateTime.Today;
                    MostrandoSoloEstaSemana = false;
                }

                // DEBUGGING: Agregar información de las fechas que se están usando
                diagnostico.Add($"Fechas usadas: {FechaInicio?.ToString("yyyy-MM-dd")} a {FechaFin?.ToString("yyyy-MM-dd")}");

                // Si viene TodosLosRegistros, no aplicar fechas por defecto
                if (TodosLosRegistros)
                {
                    diagnostico.Add("Modo: Todos los registros");
                }

                // STORED PROCEDURE 2: Liquidaciones con filtros
                var sw2 = Stopwatch.StartNew();
                await GetLiquidacionesConFiltrosAsync(connectionString);
                sw2.Stop();
                diagnostico.Add($"Liquidaciones: {sw2.ElapsedMilliseconds}ms");

                RegistrosMostrados = Liquidaciones.Count;

                // Preparar textos para mostrar filtros activos
                PrepararFiltrosActivos();

                stopwatchTotal.Stop();
                diagnostico.Add($"TOTAL: {stopwatchTotal.ElapsedMilliseconds}ms");
                DiagnosticoTiempos = string.Join(" | ", diagnostico);

                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                DiagnosticoTiempos = $"ERROR: {ex.Message}";
                Liquidaciones = new List<LiquidacionViewModel>();
                return Page();
            }
        }

        private async Task CargarOpcionesFiltrosAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand("SP_GetFiltroOpciones", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 5
            };

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ClientesDisponibles.Add(new ClienteOpcion
                {
                    Nombre = reader.GetString("Valor"),
                    Cantidad = reader.GetInt32("Cantidad")
                });
            }
        }

        private async Task GetEstadisticasRapidoAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand("SP_GetLiquidacionesEstadisticas", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 10
            };

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                TotalRegistros = reader.GetInt32("TotalRegistros");
                FechaMinimaDisponible = reader.IsDBNull("FechaMinimaDisponible")
                    ? DateTime.Today.AddDays(-30)
                    : reader.GetDateTime("FechaMinimaDisponible");
                FechaMaximaDisponible = reader.IsDBNull("FechaMaximaDisponible")
                    ? DateTime.Today
                    : reader.GetDateTime("FechaMaximaDisponible");
            }
            else
            {
                // Si no hay datos, usar valores por defecto dinámicos
                TotalRegistros = 0;
                FechaMinimaDisponible = DateTime.Today.AddDays(-30);
                FechaMaximaDisponible = DateTime.Today;
            }
        }

        private async Task GetLiquidacionesConFiltrosAsync(string connectionString)
        {
            // Obtener el rol para validaciones
            var rolId = HttpContext.Session.GetInt32("idRol");

            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand("SP_GetLiquidacionesConFiltros", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30  // Aumentado para consultas grandes
            };

            // NUEVA LÓGICA: Límites más generosos para todos los roles
            int maxRecords = 50000; // Por defecto, límite muy alto

            // Solo aplicar límites restrictivos si NO hay filtros de fecha específicos
            if (!FechaInicio.HasValue && !FechaFin.HasValue && !TodosLosRegistros)
            {
                // Sin filtros de fecha = consulta por defecto = límite moderado
                maxRecords = 1000;
            }
            else if (TodosLosRegistros)
            {
                // "Todos los registros" - verificar permisos
                if (rolId != 5)
                {
                    TempData["Error"] = "No tiene permisos para ver todos los registros.";
                    return;
                }
                maxRecords = 100000; // Límite muy alto para "todos"
            }
            else
            {
                // Si hay filtros de fecha específicos, permitir muchos registros
                // para que vean TODOS los del período solicitado
                if (FechaInicio.HasValue && FechaFin.HasValue)
                {
                    var diasDiferencia = (FechaFin.Value - FechaInicio.Value).Days;

                    if (diasDiferencia <= 1)      // 1 día = TODOS los registros del día
                        maxRecords = 10000;       // Muy alto para cubrir cualquier día
                    else if (diasDiferencia <= 7) // 1 semana = TODOS los de la semana  
                        maxRecords = 25000;       // Muy alto para cubrir cualquier semana
                    else if (diasDiferencia <= 31) // 1 mes = TODOS los del mes
                        maxRecords = 50000;       // Muy alto para cubrir cualquier mes
                    else                          // Más de 1 mes
                        maxRecords = 75000;       // Para rangos muy amplios
                }
            }

            // Parámetros básicos
            command.Parameters.Add(new SqlParameter("@SearchString", SqlDbType.NVarChar, 255)
            {
                Value = string.IsNullOrEmpty(SearchString) ? DBNull.Value : SearchString
            });

            // NUEVO: Parámetro específico para Remolque
            command.Parameters.Add(new SqlParameter("@RemolqueFiltro", SqlDbType.NVarChar, 255)
            {
                Value = string.IsNullOrEmpty(Remolque) ? DBNull.Value : Remolque
            });

            command.Parameters.Add(new SqlParameter("@FechaInicio", SqlDbType.DateTime)
            {
                Value = FechaInicio ?? (object)DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@FechaFin", SqlDbType.DateTime)
            {
                Value = FechaFin ?? (object)DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@MaxRecords", SqlDbType.Int) { Value = maxRecords });

            // NUEVO: Parámetro para todos los registros
            command.Parameters.Add(new SqlParameter("@TodosLosRegistros", SqlDbType.Bit) { Value = TodosLosRegistros });

            // PARÁMETROS DE FILTROS (ajustados a tu SP actual)
            command.Parameters.Add(new SqlParameter("@StatusFiltro", SqlDbType.TinyInt)
            {
                Value = StatusFiltro.HasValue ? StatusFiltro.Value : DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@ClienteFiltro", SqlDbType.NVarChar, 255)
            {
                Value = string.IsNullOrEmpty(ClienteFiltro) ? DBNull.Value : ClienteFiltro
            });
            command.Parameters.Add(new SqlParameter("@ConductorFiltro", SqlDbType.NVarChar, 255)
            {
                Value = DBNull.Value  // No usado en la vista actual
            });
            command.Parameters.Add(new SqlParameter("@OrigenFiltro", SqlDbType.NVarChar, 255)
            {
                Value = DBNull.Value  // No usado en la vista actual
            });
            command.Parameters.Add(new SqlParameter("@DestinoFiltro", SqlDbType.NVarChar, 255)
            {
                Value = DBNull.Value  // No usado en la vista actual
            });
            command.Parameters.Add(new SqlParameter("@EvidenciasFiltro", SqlDbType.NVarChar, 20)
            {
                Value = string.IsNullOrEmpty(EvidenciasFiltro) ? DBNull.Value : EvidenciasFiltro
            });

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var liquidaciones = new List<LiquidacionViewModel>();

            while (await reader.ReadAsync())
            {
                var liquidacion = new LiquidacionViewModel
                {
                    PodId = reader.GetInt32("POD_ID"),
                    Folio = reader.IsDBNull("Folio") ? null : reader.GetString("Folio"),
                    Cliente = reader.IsDBNull("Cliente") ? null : reader.GetString("Cliente"),
                    Tractor = reader.IsDBNull("Tractor") ? null : reader.GetString("Tractor"),
                    Remolque = reader.IsDBNull("Remolque") ? null : reader.GetString("Remolque"),
                    FechaSalida = reader.IsDBNull("FechaSalida") ? null : reader.GetDateTime("FechaSalida"),
                    Origen = reader.IsDBNull("Origen") ? null : reader.GetString("Origen"),
                    Destino = reader.IsDBNull("Destino") ? null : reader.GetString("Destino"),
                    Plant = reader.IsDBNull("Plant") ? null : reader.GetString("Plant"),
                    DriverName = reader.IsDBNull("DriverName") ? null : reader.GetString("DriverName"),
                    Status = ConvertStatusToString(reader.IsDBNull("Status") ? null : reader.GetByte("Status")),
                    PodRecordCaptureDate = reader.IsDBNull("CaptureDate") ? null : reader.GetDateTime("CaptureDate"),
                    PodRecordImageUrl = reader.IsDBNull("ImageUrl") ? null : reader.GetString("ImageUrl"),
                    Evidencias = new List<EvidenciaViewModel>()
                };

                // Agregar evidencias simplificadas (solo conteos)
                var totalEvidencias = reader.GetInt32("TotalEvidencias");
                var evidenciasConImagen = reader.GetInt32("EvidenciasConImagen");

                // Crear evidencias "fake" solo para que la vista funcione
                for (int i = 0; i < totalEvidencias && i < 5; i++)
                {
                    liquidacion.Evidencias.Add(new EvidenciaViewModel
                    {
                        EvidenciaId = 0,
                        FileName = $"Evidencia {i + 1}",
                        HasImageData = i < evidenciasConImagen
                    });
                }

                liquidaciones.Add(liquidacion);
            }

            Liquidaciones = liquidaciones;
        }

        public async Task<IActionResult> OnPostGenerarPDFsMasivosAsync(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] = "Debe seleccionar al menos un registro.";
                return RedirectToPage();
            }

            // Redirigir a la página de GenerarPDF con los IDs seleccionados
            var idsString = string.Join(",", selectedIds);
            return RedirectToPage("./GenerarPDF", new { ids = idsString });
        }

        private void PrepararFiltrosActivos()
        {
            if (StatusFiltro.HasValue)
            {
                StatusFiltroTexto = StatusFiltro.Value switch
                {
                    0 => "En Tránsito",
                    1 => "Entregado",
                    2 => "Pendiente",
                    _ => "Desconocido"
                };
            }

            if (!string.IsNullOrEmpty(EvidenciasFiltro))
            {
                EvidenciasFiltroTexto = EvidenciasFiltro switch
                {
                    "sin_evidencias" => "Sin evidencias",
                    "con_evidencias" => "Con evidencias",
                    "solo_imagenes" => "Solo con imágenes",
                    _ => EvidenciasFiltro
                };
            }
        }

        private string ConvertStatusToString(byte? statusValue)
        {
            if (!statusValue.HasValue) return "Desconocido";
            return statusValue.Value switch
            {
                0 => "En Tránsito",
                1 => "Entregado",
                2 => "Pendiente",
                _ => $"Código: {statusValue.Value}"
            };
        }
    }

    // DTO para opciones de filtros
    public class ClienteOpcion
    {
        public string Nombre { get; set; } = "";
        public int Cantidad { get; set; }
    }
}