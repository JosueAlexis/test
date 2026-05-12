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
        public int CuentaSeleccionadaId { get; set; } // ✅ Ahora usamos Cuenta

        [BindProperty]
        public int CantidadAsignar { get; set; }

        [BindProperty]
        public List<int> SellosSeleccionados { get; set; } = new List<int>();

        public SelectList ListaCuentas { get; set; } // ✅ Lista de Cuentas

        [BindProperty]
        public IFormFile ArchivoExcel { get; set; }

        [BindProperty]
        public int CuentaADesasignar { get; set; } // ✅ Para vaciar una cuenta

        // --- Propiedades para Agregar Manualmente ---
        [BindProperty(SupportsGet = true)]
        public string NumeroSelloCheck { get; set; }

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
            // ✅ Cargar inventario incluyendo la relación a la Cuenta
            ListaSellos = await _context.TblSellos
                .Include(s => s.Cuenta)
                .OrderBy(s => s.Sello)
                .ToListAsync();

            // ✅ Cargar solo cuentas activas
            var cuentas = await _context.TblCuentas
                .Where(c => c.EsActiva)
                .OrderBy(c => c.OrdenVisualizacion)
                .ToListAsync();

            ListaCuentas = new SelectList(cuentas, "Id", "NombreCuenta");
        }

        // ==========================================
        // VALIDACIÓN EN VIVO (AJAX)
        // ==========================================
        public async Task<IActionResult> OnGetCheckSelloAsync(string numeroSelloCheck)
        {
            if (string.IsNullOrWhiteSpace(numeroSelloCheck))
                return new JsonResult(new { existe = false, mensaje = "" });

            bool existe = await _context.TblSellos
                .AnyAsync(s => s.Sello.Trim().ToLower() == numeroSelloCheck.Trim().ToLower());

            return new JsonResult(new
            {
                existe,
                mensaje = existe ? $"El sello {numeroSelloCheck} ya existe." : "Disponible ✓"
            });
        }

        // ==========================================
        // AGREGAR SELLO MANUAL
        // ==========================================
        public async Task<IActionResult> OnPostAgregarSelloManualAsync()
        {
            if (string.IsNullOrWhiteSpace(NumeroSello))
            {
                MensajeError = "El número de sello es obligatorio.";
                await CargarDatosIniciales();
                return Page();
            }

            if (await _context.TblSellos.AnyAsync(s => s.Sello.Trim().ToLower() == NumeroSello.Trim().ToLower()))
            {
                MensajeError = $"El sello {NumeroSello} ya existe en el sistema.";
                await CargarDatosIniciales();
                return Page();
            }

            var usuarioId = HttpContext.Session.GetInt32("idUsuario");
            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (!usuarioId.HasValue) return RedirectToPage("/Login");

            try
            {
                var nuevoSello = new TblSellos
                {
                    Sello = NumeroSello.Trim(),
                    Status = 1,
                    Fentrega = FechaEntregaManual ?? DateTime.Now,
                    Recibio = string.IsNullOrWhiteSpace(RecibioManual) ? null : RecibioManual.Trim(),
                    Alta = usuarioId.Value,
                    IdCuenta = null, // Nace en Central (sin cuenta)
                    FechaAsignacion = null
                };

                _context.TblSellos.Add(nuevoSello);
                await _context.SaveChangesAsync();

                await _auditoriaService.RegistrarImportacion(
                    nuevoSello, usuarioId, usuarioNombre, ip, "Alta manual web"
                );
                await _context.SaveChangesAsync();

                MensajeExito = $"✅ Sello {NumeroSello} agregado correctamente como disponible.";
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
        // HANDLER: ASIGNAR A CUENTA (AUTOMÁTICO / RANDOM)
        // ==========================================
        public async Task<IActionResult> OnPostAsignarAsync()
        {
            if (CuentaSeleccionadaId <= 0 || CantidadAsignar <= 0)
            {
                MensajeError = "Selecciona una cuenta y una cantidad válida.";
                await CargarDatosIniciales();
                return Page();
            }

            var disponibles = await _context.TblSellos
                .Where(s => s.Status == 1)
                .OrderBy(x => Guid.NewGuid())
                .ToListAsync();

            if (disponibles.Count < CantidadAsignar)
            {
                MensajeError = $"No hay suficientes sellos disponibles (Solo hay {disponibles.Count}).";
                await CargarDatosIniciales();
                return Page();
            }

            var disponiblesRandom = disponibles.OrderBy(x => Guid.NewGuid()).ToList();
            var asignados = new List<TblSellos>();
            const int maxConsecutivosPermitidos = 2;

            foreach (var sello in disponiblesRandom)
            {
                if (asignados.Count >= CantidadAsignar) break;

                bool puedeAgregar = true;
                if (asignados.Count >= maxConsecutivosPermitidos)
                {
                    var ultimos = asignados.TakeLast(maxConsecutivosPermitidos).ToList();
                    var ultimoNumero = Convert.ToInt32(ultimos.Last().Sello);
                    var nuevoNumero = Convert.ToInt32(sello.Sello);

                    if (Math.Abs(nuevoNumero - ultimoNumero) == 1)
                    {
                        int contadorConsecutivos = 1;
                        for (int i = ultimos.Count - 2; i >= 0; i--)
                        {
                            var anterior = Convert.ToInt32(ultimos[i].Sello);
                            if (Math.Abs(anterior - ultimoNumero) == 1)
                            {
                                contadorConsecutivos++;
                                ultimoNumero = anterior;
                            }
                            else break;
                        }
                        if (contadorConsecutivos >= maxConsecutivosPermitidos) puedeAgregar = false;
                    }
                }
                if (puedeAgregar) asignados.Add(sello);
            }

            if (asignados.Count < CantidadAsignar)
            {
                MensajeError = $"No se pudieron seleccionar los sellos. Intenta con menos cantidad.";
                await CargarDatosIniciales();
                return Page();
            }

            var usuarioId = HttpContext.Session.GetInt32("idUsuario");
            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var cuentaDb = await _context.TblCuentas.FindAsync(CuentaSeleccionadaId);
            string nombreCuenta = cuentaDb?.NombreCuenta ?? "Cuenta Desconocida";

            foreach (var sello in asignados)
            {
                var statusAnterior = sello.Status;

                sello.Status = 14;
                sello.IdCuenta = CuentaSeleccionadaId; // ✅ SE ASIGNA A LA CUENTA
                sello.FechaAsignacion = DateTime.Now;

                await _auditoriaService.RegistrarAsignacion(
                    sello, statusAnterior, null, nombreCuenta, usuarioId, usuarioNombre, ip,
                    $"Asignación automática a la cuenta {nombreCuenta}"
                );
            }

            await _context.SaveChangesAsync();
            MensajeExito = $"Se asignaron correctamente {asignados.Count} sellos a {nombreCuenta}.";
            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: ASIGNAR SELLOS SELECCIONADOS
        // ==========================================
        public async Task<IActionResult> OnPostAsignarSeleccionadosAsync()
        {
            if (CuentaSeleccionadaId <= 0)
            {
                MensajeError = "Debes seleccionar una cuenta destino.";
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

            var cuentaDb = await _context.TblCuentas.FindAsync(CuentaSeleccionadaId);
            string nombreCuenta = cuentaDb?.NombreCuenta ?? "Cuenta Desconocida";

            foreach (var sello in sellos)
            {
                var statusAnterior = sello.Status;

                sello.Status = 14;
                sello.IdCuenta = CuentaSeleccionadaId; // ✅ SE ASIGNA A LA CUENTA
                sello.FechaAsignacion = DateTime.Now;

                await _auditoriaService.RegistrarAsignacion(
                    sello, statusAnterior, null, nombreCuenta, usuarioId, usuarioNombre, ip,
                    $"Asignación manual selectiva a {nombreCuenta}"
                );
            }

            await _context.SaveChangesAsync();
            MensajeExito = $"Se asignaron correctamente {sellos.Count} sello(s) a {nombreCuenta}.";
            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: DESASIGNAR POR CUENTA (MASIVO)
        // ==========================================
        public async Task<IActionResult> OnPostDesasignarPorCuentaAsync()
        {
            if (CuentaADesasignar <= 0)
            {
                MensajeError = "Selecciona una cuenta válida.";
                return RedirectToPage();
            }

            try
            {
                var sellos = await _context.TblSellos
                    .Where(s => s.IdCuenta == CuentaADesasignar && s.Status == 14)
                    .ToListAsync();

                if (!sellos.Any())
                {
                    MensajeError = "Esta cuenta no tiene sellos en su inventario local.";
                    return RedirectToPage();
                }

                var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                var cuentaDb = await _context.TblCuentas.FindAsync(CuentaADesasignar);
                string nombreCuenta = cuentaDb?.NombreCuenta ?? "Cuenta";

                foreach (var sello in sellos)
                {
                    var statusAnterior = sello.Status;
                    var fechaAsignacionAnterior = sello.FechaAsignacion;

                    sello.Status = 1; // Vuelve a Central
                    sello.IdCuenta = null; // ✅ SE DESVINCULA DE LA CUENTA
                    sello.FechaAsignacion = null;

                    await _auditoriaService.RegistrarDesasignacion(
                        sello, statusAnterior, null, nombreCuenta, fechaAsignacionAnterior, usuarioId, usuarioNombre, ip,
                        $"Desasignación masiva de la cuenta {nombreCuenta}"
                    );
                }

                await _context.SaveChangesAsync();
                MensajeExito = $"Se devolvieron {sellos.Count} sellos de {nombreCuenta} al almacén central.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al desasignar: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ==========================================
        // HANDLER: DESASIGNAR SELLOS SELECCIONADOS
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
                    .Include(s => s.Cuenta) // ✅ Incluimos la cuenta para leer el nombre en la auditoría
                    .Where(s => SellosSeleccionados.Contains(s.Id) && s.Status == 14)
                    .ToListAsync();

                if (!sellos.Any())
                {
                    MensajeError = "Los sellos seleccionados no están asignados a ninguna cuenta.";
                    return RedirectToPage();
                }

                var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                foreach (var sello in sellos)
                {
                    var statusAnterior = sello.Status;
                    var fechaAsignacionAnterior = sello.FechaAsignacion;
                    string nombreCuentaAnterior = sello.Cuenta?.NombreCuenta ?? "Cuenta Desconocida";

                    sello.Status = 1;
                    sello.IdCuenta = null; // ✅ SE DESVINCULA
                    sello.FechaAsignacion = null;

                    await _auditoriaService.RegistrarDesasignacion(
                        sello, statusAnterior, null, nombreCuentaAnterior, fechaAsignacionAnterior, usuarioId, usuarioNombre, ip,
                        "Devolución selectiva individual a central"
                    );
                }

                await _context.SaveChangesAsync();
                MensajeExito = $"Se devolvieron {sellos.Count} sello(s) seleccionados a central.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al desasignar: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ==========================================
        // IMPORTAR EXCEL
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
                            Status = 1,
                            IdCuenta = null, // Nace sin cuenta
                            FechaAsignacion = null,
                            Alta = HttpContext.Session.GetInt32("idUsuario")
                        });
                    }

                    if (sellosNuevos.Count > 0)
                    {
                        _context.TblSellos.AddRange(sellosNuevos);
                        await _context.SaveChangesAsync();

                        var usuarioId = HttpContext.Session.GetInt32("idUsuario");
                        var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
                        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                        foreach (var sello in sellosNuevos)
                        {
                            await _auditoriaService.RegistrarImportacion(sello, usuarioId, usuarioNombre, ip, $"Importado desde Excel");
                        }

                        await _context.SaveChangesAsync();
                        MensajeExito = $"Se importaron {sellosNuevos.Count} sellos correctamente.";
                    }
                    else MensajeError = "No se importaron sellos (puede que ya existan o el archivo esté vacío).";
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