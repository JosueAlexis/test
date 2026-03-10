using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Operadores
{
    public class EditarBasicoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditarBasicoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Empleado Empleado { get; set; } = new();

        [BindProperty]
        public IFormFile? FotoNueva { get; set; }

        [BindProperty]
        public string PuestoNombre { get; set; } = string.Empty;

        public List<PuestoEmpleado> Puestos { get; set; } = new();

        public string ImagenBase64Actual { get; set; } = string.Empty;  // Para mostrar foto actual

        [TempData]
        public string Mensaje { get; set; } = string.Empty;

        public string NombreCompleto => $"{Empleado?.Names} {Empleado?.Apellido}".Trim();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Empleado = await _context.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (Empleado == null) return NotFound();

            // Cargar imagen actual (consulta directa, como en tus otras páginas)
            ImagenBase64Actual = await _context.ImagenesEmpleados
                .Where(i => i.idEmpleado == id)
                .Select(i => i.Imagen)
                .FirstOrDefaultAsync() ?? string.Empty;

            CargarPuestos();

            if (Empleado.Puesto.HasValue)
            {
                var puestoActual = await _context.PuestoEmpleados
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.id == Empleado.Puesto.Value);

                if (puestoActual != null)
                {
                    PuestoNombre = puestoActual.Puesto ?? "";
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (id != Empleado.Id) return NotFound();

            // IMPORTANTE: Cargar puestos ANTES de cualquier validación
            CargarPuestos();

            // Recargar empleado original para actualizar
            var empleadoDb = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empleadoDb == null) return NotFound();

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(Empleado.Names) ||
                string.IsNullOrWhiteSpace(Empleado.Apellido) ||
                string.IsNullOrWhiteSpace(Empleado.Email) ||
                string.IsNullOrWhiteSpace(Empleado.Rfc) ||
                string.IsNullOrWhiteSpace(Empleado.Curp) ||
                string.IsNullOrWhiteSpace(Empleado.NumSSocial))
            {
                Mensaje = "❌ Completa los campos obligatorios (nombre, apellidos, email, RFC, CURP, NSS).";
                await CargarDatosAuxiliares();
                return Page();
            }

            // Validar que se haya ingresado la fecha de ingreso
            if (!Empleado.Fingreso.HasValue)
            {
                Mensaje = "❌ La fecha de ingreso es obligatoria.";
                await CargarDatosAuxiliares();
                return Page();
            }

            // Validar edad mínima (18 años)
            if (Empleado.Fnacimiento.HasValue)
            {
                var hoy = DateTime.Today;
                var nacimiento = Empleado.Fnacimiento.Value;
                int edad = hoy.Year - nacimiento.Year;
                if (nacimiento.Date > hoy.AddYears(-edad)) edad--;
                if (edad < 18)
                {
                    Mensaje = "❌ El empleado debe tener al menos 18 años.";
                    await CargarDatosAuxiliares();
                    return Page();
                }
            }

            // Actualizar campos permitidos
            empleadoDb.Names = Empleado.Names?.Trim();
            empleadoDb.Apellido = Empleado.Apellido?.Trim();
            empleadoDb.Apellido2 = Empleado.Apellido2?.Trim();
            empleadoDb.Email = Empleado.Email?.Trim();
            empleadoDb.Telefono = Empleado.Telefono?.Trim();
            empleadoDb.Rfc = Empleado.Rfc?.Trim().ToUpperInvariant();
            empleadoDb.Curp = Empleado.Curp?.Trim().ToUpperInvariant();
            empleadoDb.NumSSocial = Empleado.NumSSocial?.Trim();
            empleadoDb.Fnacimiento = Empleado.Fnacimiento;
            empleadoDb.TelEmergencia = Empleado.TelEmergencia?.Trim();
            empleadoDb.CodClientes = Empleado.CodClientes;
            empleadoDb.Fingreso = Empleado.Fingreso; // <--- SE GUARDA LA FECHA DE INGRESO

            // Actualizar puesto
            if (!string.IsNullOrWhiteSpace(PuestoNombre))
            {
                // DEBUG: Agregar logging detallado
                Console.WriteLine($"[DEBUG] PuestoNombre recibido: '{PuestoNombre}'");
                Console.WriteLine($"[DEBUG] PuestoNombre Length: {PuestoNombre.Length}");
                Console.WriteLine($"[DEBUG] Bytes: {string.Join(",", System.Text.Encoding.UTF8.GetBytes(PuestoNombre))}");
                Console.WriteLine($"[DEBUG] Total puestos en lista: {Puestos.Count}");

                foreach (var p in Puestos)
                {
                    Console.WriteLine($"[DEBUG] BD Puesto: '{p.Puesto}' (Length: {p.Puesto?.Length ?? 0})");
                }

                // Normalizar espacios múltiples a un solo espacio
                var puestoNormalizado = System.Text.RegularExpressions.Regex.Replace(
                    PuestoNombre.Trim(), @"\s+", " ");

                Console.WriteLine($"[DEBUG] PuestoNormalizado: '{puestoNormalizado}'");

                var puestoSel = Puestos.FirstOrDefault(p =>
                {
                    if (string.IsNullOrWhiteSpace(p.Puesto)) return false;
                    var puestoDbNormalizado = System.Text.RegularExpressions.Regex.Replace(
                        p.Puesto.Trim(), @"\s+", " ");

                    Console.WriteLine($"[DEBUG] Comparando '{puestoDbNormalizado}' == '{puestoNormalizado}': {puestoDbNormalizado.Equals(puestoNormalizado, StringComparison.OrdinalIgnoreCase)}");

                    return puestoDbNormalizado.Equals(puestoNormalizado, StringComparison.OrdinalIgnoreCase);
                });

                if (puestoSel != null)
                {
                    Console.WriteLine($"[DEBUG] ✓ Puesto encontrado: ID={puestoSel.id}");
                    empleadoDb.Puesto = puestoSel.id;
                    empleadoDb.TipEmpleado = puestoSel.idtipempleado;
                }
                else
                {
                    Console.WriteLine($"[DEBUG] ✗ NO se encontró el puesto");
                    Mensaje = $"❌ El puesto '{PuestoNombre}' no existe en el catálogo.";
                    await CargarDatosAuxiliares();
                    return Page();
                }
            }

            // Foto nueva (opcional)
            if (FotoNueva != null && FotoNueva.Length > 0)
            {
                if (FotoNueva.Length > 5 * 1024 * 1024)
                {
                    Mensaje = "❌ La fotografía no debe superar los 5 MB.";
                    await CargarDatosAuxiliares();
                    return Page();
                }

                using var ms = new MemoryStream();
                await FotoNueva.CopyToAsync(ms);
                var base64 = Convert.ToBase64String(ms.ToArray());

                // Buscar imagen existente en tblImagenes
                var imagenExistente = await _context.ImagenesEmpleados
                    .FirstOrDefaultAsync(i => i.idEmpleado == empleadoDb.Id);

                if (imagenExistente != null)
                {
                    imagenExistente.Imagen = base64;
                }
                else
                {
                    var nuevaImg = new ImagenEmpleado
                    {
                        idEmpleado = empleadoDb.Id,
                        Imagen = base64
                    };
                    _context.ImagenesEmpleados.Add(nuevaImg);
                }
            }

            // Auditoría
            empleadoDb.FechaUltimaModificacion = DateTime.Now;
            empleadoDb.UsuarioUltimaModificacion = HttpContext.Session.GetInt32("idUsuario") ?? 0;

            try
            {
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"✅ Datos básicos de {empleadoDb.Names} {empleadoDb.Apellido} actualizados correctamente.";
                return RedirectToPage("/Operadores/Detalles", new { id });
            }
            catch (Exception ex)
            {
                Mensaje = $"❌ Error al guardar: {ex.Message}";
                if (ex.InnerException != null) Mensaje += $" | {ex.InnerException.Message}";
                await CargarDatosAuxiliares();
                return Page();
            }
        }

        private async Task CargarDatosAuxiliares()
        {
            CargarPuestos();
            ImagenBase64Actual = await _context.ImagenesEmpleados
                .Where(i => i.idEmpleado == Empleado.Id)
                .Select(i => i.Imagen)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        private void CargarPuestos()
        {
            Puestos = _context.PuestoEmpleados
                .AsNoTracking()
                .OrderBy(p => p.Puesto)
                .ToList();
        }
    }
}