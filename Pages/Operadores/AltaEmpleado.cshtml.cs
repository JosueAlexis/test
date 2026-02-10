using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ProyectoRH2025.Pages.Operadores
{
    public class AltaEmpleadoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AltaEmpleadoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Empleado Empleado { get; set; } = new Empleado();

        [BindProperty]
        public IFormFile FotoArchivo { get; set; }

        [BindProperty]
        public string PuestoNombre { get; set; }

        [TempData]
        public string Mensaje { get; set; } = string.Empty;

        public List<PuestoEmpleado> Puestos { get; set; } = new List<PuestoEmpleado>();

        public void OnGet()
        {
            CargarPuestos();
            Empleado.Fingreso = DateTime.Now;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            CargarPuestos();

            try
            {
                // Log de debug
                System.Diagnostics.Debug.WriteLine($"=== INICIO POST - PASO 1: ALTA BÁSICA ===");
                System.Diagnostics.Debug.WriteLine($"FotoArchivo: {FotoArchivo?.FileName ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"FotoArchivo Length: {FotoArchivo?.Length ?? 0}");
                System.Diagnostics.Debug.WriteLine($"PuestoNombre: {PuestoNombre ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"CodClientes: {Empleado.CodClientes ?? "NULL"}");

                // ========== VALIDACIONES ==========

                if (string.IsNullOrWhiteSpace(Empleado.Names) ||
                    string.IsNullOrWhiteSpace(Empleado.Apellido) ||
                    string.IsNullOrWhiteSpace(Empleado.Email) ||
                    string.IsNullOrWhiteSpace(Empleado.Rfc) ||
                    string.IsNullOrWhiteSpace(Empleado.Curp) ||
                    string.IsNullOrWhiteSpace(Empleado.NumSSocial))
                {
                    Mensaje = "❌ Todos los campos obligatorios deben estar completos.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Campos incompletos");
                    return Page();
                }

                // Validación de foto
                if (FotoArchivo == null || FotoArchivo.Length == 0)
                {
                    Mensaje = "❌ La fotografía es obligatoria.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Sin foto");
                    return Page();
                }

                if (FotoArchivo.Length > 5 * 1024 * 1024)
                {
                    Mensaje = "❌ La fotografía no debe superar los 5MB.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Foto muy grande");
                    return Page();
                }

                // Validar fecha de nacimiento (mayor de 18 años)
                if (!Empleado.Fnacimiento.HasValue)
                {
                    Mensaje = "❌ La fecha de nacimiento es obligatoria.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Sin fecha nacimiento");
                    return Page();
                }

                var edad = DateTime.Now.Year - Empleado.Fnacimiento.Value.Year;
                if (Empleado.Fnacimiento.Value.Date > DateTime.Now.AddYears(-edad)) edad--;

                if (edad < 18)
                {
                    Mensaje = "❌ El empleado debe ser mayor de 18 años.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Menor de 18 años. Edad: {edad}");
                    return Page();
                }

                // Validar empresa
                if (string.IsNullOrWhiteSpace(Empleado.CodClientes) ||
                    (Empleado.CodClientes != "1" && Empleado.CodClientes != "2"))
                {
                    Mensaje = $"❌ Debe seleccionar una empresa válida (AKNA o STIL). Valor recibido: '{Empleado.CodClientes}'";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Empresa inválida: {Empleado.CodClientes}");
                    return Page();
                }

                // Validar puesto
                if (string.IsNullOrWhiteSpace(PuestoNombre))
                {
                    Mensaje = "❌ Debe seleccionar un puesto.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Sin puesto");
                    return Page();
                }

                // Buscar el puesto seleccionado
                var puestoSeleccionado = Puestos.FirstOrDefault(p =>
                    p.Puesto.Equals(PuestoNombre, StringComparison.OrdinalIgnoreCase));

                if (puestoSeleccionado == null)
                {
                    Mensaje = $"❌ El puesto '{PuestoNombre}' no es válido.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: Puesto no encontrado: {PuestoNombre}");
                    return Page();
                }

                System.Diagnostics.Debug.WriteLine($"✅ Validaciones OK");

                // ========== ASIGNAR VALORES ==========

                Empleado.Puesto = puestoSeleccionado.id;
                Empleado.TipEmpleado = puestoSeleccionado.idtipempleado;
                Empleado.FechaAlta = DateTime.Now;
                Empleado.IdUsuarioAlta = HttpContext.Session.GetInt32("idUsuario") ?? 0;
                Empleado.Editor = Empleado.IdUsuarioAlta;
                Empleado.Status = 1;

                System.Diagnostics.Debug.WriteLine($"Puesto ID: {Empleado.Puesto}");
                System.Diagnostics.Debug.WriteLine($"TipEmpleado: {Empleado.TipEmpleado}");
                System.Diagnostics.Debug.WriteLine($"Usuario: {Empleado.IdUsuarioAlta}");

                // ========== GUARDAR EMPLEADO ==========

                _context.Empleados.Add(Empleado);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"✅ Empleado guardado con ID: {Empleado.Id}");

                // ========== PROCESAR Y GUARDAR IMAGEN ==========

                using (var ms = new MemoryStream())
                {
                    await FotoArchivo.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    var base64 = Convert.ToBase64String(bytes);

                    System.Diagnostics.Debug.WriteLine($"Imagen Base64 length: {base64.Length}");

                    var img = new ImagenEmpleado
                    {
                        idEmpleado = Empleado.Id,
                        Imagen = base64
                    };

                    _context.ImagenesEmpleados.Add(img);
                    await _context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✅ Imagen guardada con ID: {img.id}");
                }

                // ========== MENSAJE DE ÉXITO + REDIRECCIÓN A PASO 2 ==========

                string empresa = Empleado.CodClientes == "1" ? "STIL" : "AKNA";
                string nombreCompleto = $"{Empleado.Names} {Empleado.Apellido}";

                System.Diagnostics.Debug.WriteLine($"=== PASO 1 COMPLETADO - Redirigiendo a Paso 2 ===");

                // ✅ REDIRECCIÓN AL WIZARD PASO 2
                TempData["MensajeWizard"] = $"✅ Empleado {nombreCompleto} registrado. Complete la información adicional.";
                TempData["EmpleadoId"] = Empleado.Id;
                TempData["EmpleadoNombre"] = nombreCompleto;

                return RedirectToPage("/Operadores/AltaGeneral", new { id = Empleado.Id });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");

                Mensaje = $"❌ Error al guardar: {ex.Message}";
                if (ex.InnerException != null)
                {
                    Mensaje += $"|Detalle: {ex.InnerException.Message}";
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return Page();
            }
        }

        private void CargarPuestos()
        {
            Puestos = _context.PuestoEmpleados
                .Include(p => p.TipoEmpleado)
                .AsNoTracking()
                .OrderBy(p => p.Puesto)
                .ToList()
                .Select(p => new PuestoEmpleado
                {
                    id = p.id,
                    Puesto = p.Puesto?.Replace("\r", "").Replace("\n", "").Trim() ?? "",
                    idtipempleado = p.idtipempleado
                })
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Puestos cargados: {Puestos.Count}");
        }
    }
}