using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Services;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Sellos
{
    public class GestionSellosModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly SellosAuditoriaService _auditoriaService;

        public GestionSellosModel(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            SellosAuditoriaService auditoriaService)
        {
            _context = context;
            _environment = environment;
            _auditoriaService = auditoriaService;
        }

        // ==========================================
        // PROPIEDADES
        // ==========================================
        public IList<TblSellos> ListaSellos { get; set; } = default!;

        [BindProperty]
        public int SupervisorSeleccionadoId { get; set; }

        [BindProperty]
        public int CantidadAsignar { get; set; }

        [BindProperty]
        public List<int> SellosSeleccionados { get; set; } = new List<int>();

        public SelectList ListaSupervisores { get; set; }

        [BindProperty]
        public IFormFile ArchivoExcel { get; set; }

        [BindProperty]
        public int SupervisorADesasignar { get; set; }

        // --- Nuevas Propiedades para Agregar Manualmente ---
        [BindProperty(SupportsGet = true)]
        public string NumeroSelloCheck { get; set; }  // Para validación AJAX

        [BindProperty]
        public string NumeroSello { get; set; }

        [BindProperty]
        public DateTime? FechaEntregaManual { get; set; }

        [BindProperty]
        public string RecibioManual { get; set; }

        // --- Mensajes ---
        [TempData]
        public string MensajeExito { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        // ==========================================
        // MÉTODO GET (Carga inicial)
        // ==========================================
        public async Task OnGetAsync()
        {
            await CargarDatosIniciales();
        }

        private async Task CargarDatosIniciales()
        {
            // 1. Cargar TODO el Inventario
            ListaSellos = await _context.TblSellos
                .Include(s => s.Supervisor)
                .OrderBy(s => s.Sello)
                .ToListAsync();

            // 2. Cargar Supervisores
            var supervisores = await _context.TblUsuarios
                .Where(u => u.idRol == 2 && u.Status == 1)
                .ToListAsync();

            ListaSupervisores = new SelectList(supervisores, "idUsuario", "UsuarioNombre");
        }

        // ==========================================
        // NUEVO: VALIDACIÓN EN VIVO (AJAX)
        // ==========================================
        public async Task<IActionResult> OnGetCheckSelloAsync(string numeroSelloCheck)
        {
            if (string.IsNullOrWhiteSpace(numeroSelloCheck))
            {
                return new JsonResult(new { existe = false, mensaje = "" });
            }

            bool existe = await _context.TblSellos
                .AnyAsync(s => s.Sello.Trim().ToLower() == numeroSelloCheck.Trim().ToLower());

            return new JsonResult(new
            {
                existe,
                mensaje = existe ? $"El sello {numeroSelloCheck} ya existe." : "Disponible ✓"
            });
        }

        // ==========================================
        // NUEVO: AGREGAR SELLO MANUAL
        // ==========================================
        public async Task<IActionResult> OnPostAgregarSelloManualAsync()
        {
            if (string.IsNullOrWhiteSpace(NumeroSello))
            {
                MensajeError = "El número de sello es obligatorio.";
                await CargarDatosIniciales();
                return Page();
            }

            // Validación final (por seguridad)
            if (await _context.TblSellos.AnyAsync(s => s.Sello.Trim().ToLower() == NumeroSello.Trim().ToLower()))
            {
                MensajeError = $"El sello {NumeroSello} ya existe en el sistema.";
                await CargarDatosIniciales();
                return Page();
            }

            try
            {
                var nuevoSello = new TblSellos
                {
                    Sello = NumeroSello.Trim(),
                    Status = 1,  // Disponible
                    Fentrega = FechaEntregaManual ?? DateTime.Now,
                    Recibio = string.IsNullOrWhiteSpace(RecibioManual) ? null : RecibioManual.Trim(),
                    Alta = HttpContext.Session.GetInt32("idUsuario"),
                    SupervisorId = null,
                    FechaAsignacion = null
                };

                _context.TblSellos.Add(nuevoSello);

                // Auditoría
                var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                await _auditoriaService.RegistrarImportacion(
                    nuevoSello,
                    usuarioId,
                    usuarioNombre,
                    ip,
                    "Alta manual individual vía interfaz web"
                );

                await _context.SaveChangesAsync();

                MensajeExito = $"Sello {NumeroSello} agregado correctamente como disponible.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al agregar el sello: {ex.Message}";
                await CargarDatosIniciales();
                return Page();
            }
        }

        // ==========================================
        // HANDLER: ASIGNAR SUPERVISOR CON AUDITORÍA
        // ==========================================
        public async Task<IActionResult> OnPostAsignarAsync()
        {
            if (SupervisorSeleccionadoId <= 0 || CantidadAsignar <= 0)
            {
                MensajeError = "Selecciona un supervisor y una cantidad válida.";
                await CargarDatosIniciales();
                return Page();
            }

            // 1. Validar sellos pendientes
            var sellosPendientes = await _context.TblSellos
                .Where(s => s.SupervisorId == SupervisorSeleccionadoId &&
                            s.Status == 4 &&
                            s.FechaAsignacion <= DateTime.Now.AddDays(-4))
                .ToListAsync();

            if (sellosPendientes.Any())
            {
                MensajeError = "Este supervisor tiene sellos pendientes hace más de 4 días.";
                await CargarDatosIniciales();
                return Page();
            }

            // 2. Obtener disponibles
            var disponibles = await _context.TblSellos
                .Where(s => s.Status == 1) // Status 1 = Activo
                .OrderBy(x => Guid.NewGuid())
                .ToListAsync();

            if (disponibles.Count < CantidadAsignar)
            {
                MensajeError = $"No hay suficientes sellos disponibles (Solo hay {disponibles.Count}).";
                await CargarDatosIniciales();
                return Page();
            }

            // 3. Selección aleatoria permitiendo hasta 2 consecutivos seguidos
            var disponiblesRandom = disponibles
                .OrderBy(x => Guid.NewGuid())   // ← mezcla aleatoria
                .ToList();

            var asignados = new List<TblSellos>();
            const int maxConsecutivosPermitidos = 2;

            foreach (var sello in disponiblesRandom)
            {
                if (asignados.Count >= CantidadAsignar) break;

                bool puedeAgregar = true;

                // Si ya tenemos suficientes al final, revisamos cuántos consecutivos hay
                if (asignados.Count >= maxConsecutivosPermitidos)
                {
                    // Tomamos los últimos maxConsecutivosPermitidos sellos asignados
                    var ultimos = asignados.TakeLast(maxConsecutivosPermitidos).ToList();
                    var ultimoNumero = Convert.ToInt32(ultimos.Last().Sello);
                    var nuevoNumero = Convert.ToInt32(sello.Sello);

                    if (Math.Abs(nuevoNumero - ultimoNumero) == 1)
                    {
                        // Contamos cuántos consecutivos hay al final
                        int contadorConsecutivos = 1; // ya contamos el último
                        for (int i = ultimos.Count - 2; i >= 0; i--)
                        {
                            var anterior = Convert.ToInt32(ultimos[i].Sello);
                            if (Math.Abs(anterior - ultimoNumero) == 1)
                            {
                                contadorConsecutivos++;
                                ultimoNumero = anterior; // seguimos la cadena
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (contadorConsecutivos >= maxConsecutivosPermitidos)
                        {
                            puedeAgregar = false;
                        }
                    }
                }

                if (puedeAgregar)
                {
                    asignados.Add(sello);
                }
            }

            if (asignados.Count < CantidadAsignar)
            {
                MensajeError = $"No se pudieron seleccionar {CantidadAsignar} sellos con la regla actual (máx {maxConsecutivosPermitidos} consecutivos). Intenta con una cantidad menor o relaja la regla.";
                await CargarDatosIniciales();
                return Page();
            }

            // 4. Obtener datos de sesión para auditoría
            var usuarioId = HttpContext.Session.GetInt32("idUsuario");
            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            // 5. Guardar cambios CON AUDITORÍA
            foreach (var sello in asignados)
            {
                var statusAnterior = sello.Status;
                var supervisorAnterior = sello.SupervisorId;
                var supervisorNombreAnterior = sello.Supervisor?.NombreCompleto ?? sello.Supervisor?.UsuarioNombre;

                // Status 14 = Asignado a Supervisor
                sello.Status = 14;
                sello.SupervisorId = SupervisorSeleccionadoId;
                sello.FechaAsignacion = DateTime.Now;

                // Cargar supervisor nuevo para obtener nombre
                await _context.Entry(sello).Reference(s => s.Supervisor).LoadAsync();

                // REGISTRAR EN AUDITORÍA
                await _auditoriaService.RegistrarAsignacion(
                    sello,
                    statusAnterior,
                    supervisorAnterior,
                    supervisorNombreAnterior,
                    usuarioId,
                    usuarioNombre,
                    ip,
                    $"Asignación automática de {asignados.Count} sellos no consecutivos"
                );
            }

            await _context.SaveChangesAsync();
            MensajeExito = $"Se asignaron correctamente {asignados.Count} sellos.";

            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: ASIGNAR SELLOS SELECCIONADOS CON AUDITORÍA
        // ==========================================
        public async Task<IActionResult> OnPostAsignarSeleccionadosAsync()
        {
            if (SupervisorSeleccionadoId <= 0)
            {
                MensajeError = "Debes seleccionar un supervisor.";
                await CargarDatosIniciales();
                return Page();
            }

            if (SellosSeleccionados == null || !SellosSeleccionados.Any())
            {
                MensajeError = "No se seleccionaron sellos.";
                await CargarDatosIniciales();
                return Page();
            }

            var sellos = await _context.TblSellos
                .Where(s => SellosSeleccionados.Contains(s.Id) && s.Status == 1)
                .ToListAsync();

            if (sellos.Count != SellosSeleccionados.Count)
            {
                MensajeError = "Algunos sellos ya no están disponibles.";
                await CargarDatosIniciales();
                return Page();
            }

            var usuarioId = HttpContext.Session.GetInt32("idUsuario");
            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            foreach (var sello in sellos)
            {
                var statusAnterior = sello.Status;
                var supervisorAnterior = sello.SupervisorId;
                var supervisorNombreAnterior = sello.Supervisor?.NombreCompleto ?? sello.Supervisor?.UsuarioNombre;

                sello.Status = 14; // Asignado
                sello.SupervisorId = SupervisorSeleccionadoId;
                sello.FechaAsignacion = DateTime.Now;

                await _context.Entry(sello).Reference(s => s.Supervisor).LoadAsync();

                await _auditoriaService.RegistrarAsignacion(
                    sello,
                    statusAnterior,
                    supervisorAnterior,
                    supervisorNombreAnterior,
                    usuarioId,
                    usuarioNombre,
                    ip,
                    "Asignación manual/selectiva individual"
                );
            }

            await _context.SaveChangesAsync();

            MensajeExito = $"Se asignaron correctamente {sellos.Count} sello(s) seleccionados.";
            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: DESASIGNAR POR SUPERVISOR CON AUDITORÍA
        // ==========================================
        public async Task<IActionResult> OnPostDesasignarPorSupervisorAsync()
        {
            if (SupervisorADesasignar <= 0)
            {
                MensajeError = "Selecciona un supervisor válido.";
                return RedirectToPage();
            }

            try
            {
                var sellos = await _context.TblSellos
                    .Include(s => s.Supervisor)
                    .Where(s => s.SupervisorId == SupervisorADesasignar && s.Status == 14)
                    .ToListAsync();

                if (!sellos.Any())
                {
                    MensajeError = "Este supervisor no tiene sellos asignados.";
                    return RedirectToPage();
                }

                // Obtener datos de sesión
                var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                foreach (var sello in sellos)
                {
                    var statusAnterior = sello.Status;
                    var supervisorAnterior = sello.SupervisorId;
                    var supervisorNombreAnterior = sello.Supervisor?.NombreCompleto ?? sello.Supervisor?.UsuarioNombre;
                    var fechaAsignacionAnterior = sello.FechaAsignacion;

                    // Volver a Status 1 (Activo)
                    sello.Status = 1;
                    sello.SupervisorId = null;
                    sello.FechaAsignacion = null;

                    // REGISTRAR EN AUDITORÍA
                    await _auditoriaService.RegistrarDesasignacion(
                        sello,
                        statusAnterior,
                        supervisorAnterior,
                        supervisorNombreAnterior,
                        fechaAsignacionAnterior,
                        usuarioId,
                        usuarioNombre,
                        ip,
                        $"Desasignación masiva - Total: {sellos.Count} sellos"
                    );
                }

                await _context.SaveChangesAsync();
                MensajeExito = $"Se desasignaron {sellos.Count} sellos del supervisor correctamente.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al desasignar: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: DESASIGNAR SELLOS SELECCIONADOS CON AUDITORÍA
        // ==========================================
        public async Task<IActionResult> OnPostDesasignarSeleccionadosAsync()
        {
            if (SellosSeleccionados == null || !SellosSeleccionados.Any())
            {
                MensajeError = "No se seleccionaron sellos para desasignar.";
                return RedirectToPage();
            }

            try
            {
                var sellos = await _context.TblSellos
                    .Include(s => s.Supervisor)
                    .Where(s => SellosSeleccionados.Contains(s.Id) && s.Status == 14)
                    .ToListAsync();

                if (!sellos.Any())
                {
                    MensajeError = "Los sellos seleccionados no están asignados o no existen.";
                    return RedirectToPage();
                }

                // Obtener datos de sesión
                var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                foreach (var sello in sellos)
                {
                    var statusAnterior = sello.Status;
                    var supervisorAnterior = sello.SupervisorId;
                    var supervisorNombreAnterior = sello.Supervisor?.NombreCompleto ?? sello.Supervisor?.UsuarioNombre;
                    var fechaAsignacionAnterior = sello.FechaAsignacion;

                    // Volver a Status 1 (Activo)
                    sello.Status = 1;
                    sello.SupervisorId = null;
                    sello.FechaAsignacion = null;

                    // REGISTRAR EN AUDITORÍA
                    await _auditoriaService.RegistrarDesasignacion(
                        sello,
                        statusAnterior,
                        supervisorAnterior,
                        supervisorNombreAnterior,
                        fechaAsignacionAnterior,
                        usuarioId,
                        usuarioNombre,
                        ip,
                        "Desasignación selectiva individual"
                    );
                }

                await _context.SaveChangesAsync();
                MensajeExito = $"Se desasignaron {sellos.Count} sello(s) correctamente.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al desasignar: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: IMPORTAR EXCEL CON AUDITORÍA
        // ==========================================
        public async Task<IActionResult> OnPostImportarAsync()
        {
            if (ArchivoExcel == null || ArchivoExcel.Length == 0)
            {
                MensajeError = "Selecciona un archivo válido.";
                return RedirectToPage();
            }

            try
            {
                var sellosNuevos = new List<TblSellos>();

                using (var stream = new MemoryStream())
                {
                    await ArchivoExcel.CopyToAsync(stream);

                    using var workbook = new XLWorkbook(stream);
                    var hoja = workbook.Worksheet(1);

                    foreach (var fila in hoja.RowsUsed().Skip(1))
                    {
                        var numeroSello = fila.Cell(1).GetString().Trim();
                        var fechaTexto = fila.Cell(2).GetString().Trim();
                        var recibidoPor = fila.Cell(3).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(numeroSello)) continue;

                        if (_context.TblSellos.Any(s => s.Sello == numeroSello)) continue;

                        if (!DateTime.TryParse(fechaTexto, out DateTime fechaEntrega))
                            fechaEntrega = DateTime.Now;

                        sellosNuevos.Add(new TblSellos
                        {
                            Sello = numeroSello,
                            Fentrega = fechaEntrega,
                            Recibio = recibidoPor,
                            Status = 1, // Activo/Disponible
                            SupervisorId = null,
                            FechaAsignacion = null,
                            Alta = HttpContext.Session.GetInt32("idUsuario")
                        });
                    }

                    if (sellosNuevos.Count > 0)
                    {
                        _context.TblSellos.AddRange(sellosNuevos);
                        await _context.SaveChangesAsync();

                        // Obtener datos de sesión
                        var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                        var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                        // REGISTRAR AUDITORÍA DE CADA SELLO IMPORTADO
                        foreach (var sello in sellosNuevos)
                        {
                            await _auditoriaService.RegistrarImportacion(
                                sello,
                                usuarioId,
                                usuarioNombre,
                                ip,
                                $"Importado desde Excel - Recibido por: {sello.Recibio}"
                            );
                        }

                        await _context.SaveChangesAsync();
                        MensajeExito = $"Se importaron {sellosNuevos.Count} sellos correctamente.";
                    }
                    else
                    {
                        MensajeError = "No se importaron sellos (puede que ya existan o el archivo esté vacío).";
                    }
                }
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al importar: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}