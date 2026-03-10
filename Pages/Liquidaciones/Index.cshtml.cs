using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.Data;
using ProyectoRH2025.MODELS;
using Microsoft.AspNetCore.Http;
using System.Data;
using System.Diagnostics;
using Hangfire;

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

        [BindProperty(SupportsGet = true)]
        public string? Remolque { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool TodosLosRegistros { get; set; } = false;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; }

        [BindProperty(SupportsGet = true)]
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

        public List<ClienteOpcion> ClientesDisponibles { get; set; } = new List<ClienteOpcion>();
        public string StatusFiltroTexto { get; set; } = "";
        public string EvidenciasFiltroTexto { get; set; } = "";
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

                var sw0 = Stopwatch.StartNew();
                await CargarOpcionesFiltrosAsync(connectionString);
                sw0.Stop();
                diagnostico.Add($"Opciones filtros: {sw0.ElapsedMilliseconds}ms");

                var sw1 = Stopwatch.StartNew();
                await GetEstadisticasRapidoAsync(connectionString);
                sw1.Stop();
                diagnostico.Add($"Estadísticas: {sw1.ElapsedMilliseconds}ms");

                if (!FechaInicio.HasValue && !FechaFin.HasValue && !TodosLosRegistros)
                {
                    FechaInicio = DateTime.Today;
                    FechaFin = DateTime.Today;
                    MostrandoSoloEstaSemana = false;
                }

                diagnostico.Add($"Fechas usadas: {FechaInicio?.ToString("yyyy-MM-dd")} a {FechaFin?.ToString("yyyy-MM-dd")}");

                if (TodosLosRegistros)
                {
                    diagnostico.Add("Modo: Todos los registros");
                }

                var sw2 = Stopwatch.StartNew();
                await GetLiquidacionesConFiltrosAsync(connectionString);
                sw2.Stop();
                diagnostico.Add($"Liquidaciones: {sw2.ElapsedMilliseconds}ms");

                RegistrosMostrados = Liquidaciones.Count;
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
                TotalRegistros = 0;
                FechaMinimaDisponible = DateTime.Today.AddDays(-30);
                FechaMaximaDisponible = DateTime.Today;
            }
        }

        private async Task GetLiquidacionesConFiltrosAsync(string connectionString)
        {
            var rolId = HttpContext.Session.GetInt32("idRol");

            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand("SP_GetLiquidacionesConFiltros", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 60
            };

            int maxRecords = 50000;

            if (!FechaInicio.HasValue && !FechaFin.HasValue && !TodosLosRegistros)
            {
                maxRecords = 1000;
            }
            else if (TodosLosRegistros)
            {
                if (rolId != 5)
                {
                    TempData["Error"] = "No tiene permisos para ver todos los registros.";
                    return;
                }
                maxRecords = 100000;
            }
            else
            {
                if (FechaInicio.HasValue && FechaFin.HasValue)
                {
                    var diasDiferencia = (FechaFin.Value - FechaInicio.Value).Days;

                    if (diasDiferencia <= 1) maxRecords = 10000;
                    else if (diasDiferencia <= 7) maxRecords = 25000;
                    else if (diasDiferencia <= 31) maxRecords = 50000;
                    else maxRecords = 75000;
                }
            }

            command.Parameters.Add(new SqlParameter("@SearchString", SqlDbType.NVarChar, 255)
            {
                Value = string.IsNullOrEmpty(SearchString) ? DBNull.Value : SearchString
            });

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

            command.Parameters.Add(new SqlParameter("@TodosLosRegistros", SqlDbType.Bit) { Value = TodosLosRegistros });

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
                Value = DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@OrigenFiltro", SqlDbType.NVarChar, 255)
            {
                Value = DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@DestinoFiltro", SqlDbType.NVarChar, 255)
            {
                Value = DBNull.Value
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

                var totalEvidencias = reader.GetInt32("TotalEvidencias");
                var evidenciasConImagen = reader.GetInt32("EvidenciasConImagen");

                if (totalEvidencias == 0)
                {
                    liquidacion.Evidencias.Add(new EvidenciaViewModel
                    {
                        EvidenciaId = -1,
                        FileName = "SHAREPOINT_PLACEHOLDER",
                        HasImageData = true
                    });
                }
                else
                {
                    for (int i = 0; i < totalEvidencias && i < 5; i++)
                    {
                        liquidacion.Evidencias.Add(new EvidenciaViewModel
                        {
                            EvidenciaId = 0,
                            FileName = i < evidenciasConImagen ? $"Evidencia BD {i + 1}" : $"Evidencia SP {i + 1}",
                            HasImageData = true
                        });
                    }
                }

                liquidaciones.Add(liquidacion);
            }

            Liquidaciones = liquidaciones;
        }

        // ====================================================================
        // MÉTODOS DE GENERACIÓN DE PDF
        // ====================================================================

        // 1. Método para las CASILLAS MANUALES
        public async Task<IActionResult> OnPostGenerarPDFsMasivosAsync(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] = "Debe seleccionar al menos un registro.";
                return RedirectToPage();
            }

            if (selectedIds.Length > 10)
            {
                // ---- OBTENER CORREO DINÁMICO DESDE tblUsuarios ----
                var idUsuario = HttpContext.Session.GetInt32("idUsuario");
                if (idUsuario == null || idUsuario == 0) return RedirectToPage("/Login");

                string emailUsuario = "";

                var usuarioDb = await _context.TblUsuarios.FindAsync(idUsuario);

                if (usuarioDb != null && !string.IsNullOrEmpty(usuarioDb.CorreoElectronico))
                {
                    emailUsuario = usuarioDb.CorreoElectronico;
                }

                if (string.IsNullOrEmpty(emailUsuario))
                {
                    TempData["Error"] = "Tu cuenta de usuario no tiene un Correo Electrónico registrado en el sistema para recibir el archivo PDF.";
                    return RedirectToPage();
                }
                // ---------------------------------------------------

                string scheme = Request.Scheme;
                string host = Request.Host.Value;

                // AHORA LLAMA AL NUEVO MÉTODO DE PDF MASIVO
                BackgroundJob.Enqueue<ProyectoRH2025.BackgroundJobs.IReporteMasivoJob>(job =>
                    job.GenerarPdfMasivoAsync(selectedIds, emailUsuario, scheme, host));

                TempData["Success"] = $"Se ha iniciado la consolidación de {selectedIds.Length} registros en un solo PDF. El proceso está en segundo plano. Recibirás un correo en {emailUsuario} cuando el documento esté listo.";
                return RedirectToPage("./Index");
            }

            var idsString = string.Join(",", selectedIds);
            return RedirectToPage("./GenerarPDF", new { ids = idsString });
        }

        // 2. MÉTODO CORREGIDO: Llama al mismo Stored Procedure de la tabla
        public async Task<IActionResult> OnPostGenerarPDFsPorFiltroAsync(
            string? searchStringFiltro,
            string? remolqueFiltro,
            DateTime? fechaInicioFiltro,
            DateTime? fechaFinFiltro,
            bool todosLosRegistrosFiltro,
            byte? statusFiltro,
            string? clienteFiltro,
            string? evidenciasFiltro)
        {
            try
            {
                SearchString = searchStringFiltro;
                Remolque = remolqueFiltro;
                FechaInicio = fechaInicioFiltro;
                FechaFin = fechaFinFiltro;
                TodosLosRegistros = todosLosRegistrosFiltro;
                StatusFiltro = statusFiltro;
                ClienteFiltro = clienteFiltro;
                EvidenciasFiltro = evidenciasFiltro;

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                await GetLiquidacionesConFiltrosAsync(connectionString);

                var selectedIds = Liquidaciones.Select(l => l.PodId).ToArray();

                if (selectedIds.Length == 0)
                {
                    TempData["Error"] = "No se encontraron registros con esos filtros para generar el PDF.";
                    return RedirectToPage();
                }

                // ---- OBTENER CORREO DINÁMICO DESDE tblUsuarios ----
                var idUsuario = HttpContext.Session.GetInt32("idUsuario");
                if (idUsuario == null || idUsuario == 0) return RedirectToPage("/Login");

                string emailUsuario = "";

                var usuarioDb = await _context.TblUsuarios.FindAsync(idUsuario);

                if (usuarioDb != null && !string.IsNullOrEmpty(usuarioDb.CorreoElectronico))
                {
                    emailUsuario = usuarioDb.CorreoElectronico;
                }

                if (string.IsNullOrEmpty(emailUsuario))
                {
                    TempData["Error"] = "Tu cuenta de usuario no tiene un Correo Electrónico registrado en el sistema para recibir el archivo PDF.";
                    return RedirectToPage();
                }
                // ---------------------------------------------------

                string scheme = Request.Scheme;
                string host = Request.Host.Value;

                // AHORA LLAMA AL NUEVO MÉTODO DE PDF MASIVO
                BackgroundJob.Enqueue<ProyectoRH2025.BackgroundJobs.IReporteMasivoJob>(job =>
                    job.GenerarPdfMasivoAsync(selectedIds, emailUsuario, scheme, host));

                TempData["Success"] = $"¡Excelente! Se inició la generación de un documento PDF consolidado con {selectedIds.Length} registros. Recibirás un correo en {emailUsuario} cuando el reporte esté listo.";

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ocurrió un error al preparar la descarga: {ex.Message}";
                return RedirectToPage("./Index");
            }
        }

        // ====================================================================

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

    public class ClienteOpcion
    {
        public string Nombre { get; set; } = "";
        public int Cantidad { get; set; }
    }
}