using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Operadores
{
    public class AltaGeneralModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AltaGeneralModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Empleado Empleado { get; set; }

        [BindProperty]
        public List<ReferenciaTemp> Referencias { get; set; } = new List<ReferenciaTemp>();

        [TempData]
        public string Mensaje { get; set; }

        public string NombreEmpleado { get; set; }

        // ← NUEVO: Bandera para saber si estamos editando o creando (wizard)
        public bool EsEdicion { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return RedirectToPage("/Operadores/AltaEmpleado");
            }

            Empleado = await _context.Empleados
                .Include(e => e.ReferenciasPersonales)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (Empleado == null)
            {
                return NotFound();
            }

            NombreEmpleado = $"{Empleado.Names} {Empleado.Apellido}";

            // Cargar referencias existentes
            Referencias = Empleado.ReferenciasPersonales
                .Where(r => r.Status)
                .Select(r => new ReferenciaTemp
                {
                    Id = r.Id,
                    Nombre = r.NombreReferencia,
                    Relacion = r.RelacionReferencia,
                    Telefono = r.TelefonoReferencia
                })
                .ToList();

            // ← NUEVO: Detectar modo edición
            EsEdicion = Request.Query["edit"] == "true";

            if (TempData["MensajeWizard"] != null)
            {
                Mensaje = TempData["MensajeWizard"].ToString();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string accion)
        {
            Empleado = await _context.Empleados
                .Include(e => e.ReferenciasPersonales)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (Empleado == null)
            {
                return NotFound();
            }

            try
            {
                var empleadoPost = Request.Form;

                // Campos familiares
                if (Enum.TryParse<EstadoCivil>(empleadoPost["Empleado.EstadoCivil"], out var estadoCivil))
                {
                    Empleado.EstadoCivil = estadoCivil;
                }
                Empleado.NombreConyuge = empleadoPost["Empleado.NombreConyuge"];
                if (byte.TryParse(empleadoPost["Empleado.NumHijos"], out var numHijos))
                {
                    Empleado.NumHijos = numHijos;
                }

                // Campos educación
                if (Enum.TryParse<Escolaridad>(empleadoPost["Empleado.Escolaridad"], out var escolaridad))
                {
                    Empleado.Escolaridad = escolaridad;
                }
                if (Enum.TryParse<NivelIngles>(empleadoPost["Empleado.NivelIngles"], out var nivelIngles))
                {
                    Empleado.NivelIngles = nivelIngles;
                }

                // Campos salud
                Empleado.Fuma = empleadoPost["Empleado.Fuma"].Contains("true");
                Empleado.Alcohol = empleadoPost["Empleado.Alcohol"].Contains("true");
                Empleado.Dopping = empleadoPost["Empleado.Dopping"].Contains("true");
                Empleado.Diabetes = empleadoPost["Empleado.Diabetes"].Contains("true");
                Empleado.Hipertension = empleadoPost["Empleado.Hipertension"].Contains("true");
                Empleado.EnfermedadCronica = empleadoPost["Empleado.EnfermedadCronica"].Contains("true");
                Empleado.TipoSangre = empleadoPost["Empleado.TipoSangre"];
                Empleado.CuentaInfonavit = empleadoPost["Empleado.CuentaInfonavit"].Contains("true");

                // Otros campos
                Empleado.ConocePersEmple = empleadoPost["Empleado.ConocePersEmple"].Contains("true");
                Empleado.NombreEmergencia = empleadoPost["Empleado.NombreEmergencia"];
                Empleado.LugarNacimiento = empleadoPost["Empleado.LugarNacimiento"];

                if (Enum.TryParse<FuenteReclutamiento>(empleadoPost["Empleado.FuenteReclutamiento"], out var fuente))
                {
                    Empleado.FuenteReclutamiento = fuente;
                }

                // Auditoría
                Empleado.FechaUltimaModificacion = DateTime.Now;
                Empleado.UsuarioUltimaModificacion = HttpContext.Session.GetInt32("idUsuario");

                // Marcar como modificado
                _context.Entry(Empleado).State = EntityState.Modified;

                // Procesar referencias personales
                foreach (var refExistente in Empleado.ReferenciasPersonales.Where(r => r.Status))
                {
                    refExistente.Status = false;
                }

                if (Referencias != null && Referencias.Any())
                {
                    foreach (var refTemp in Referencias.Where(r => !string.IsNullOrWhiteSpace(r.Nombre)))
                    {
                        if (refTemp.Id > 0)
                        {
                            var refExistente = Empleado.ReferenciasPersonales.FirstOrDefault(r => r.Id == refTemp.Id);
                            if (refExistente != null)
                            {
                                refExistente.NombreReferencia = refTemp.Nombre;
                                refExistente.RelacionReferencia = refTemp.Relacion;
                                refExistente.TelefonoReferencia = refTemp.Telefono;
                                refExistente.Status = true;
                            }
                        }
                        else
                        {
                            _context.ReferenciasPersonalesEmpleados.Add(new ReferenciaPersEmpleado
                            {
                                IdEmpleado = Empleado.Id,
                                NombreReferencia = refTemp.Nombre,
                                RelacionReferencia = refTemp.Relacion,
                                TelefonoReferencia = refTemp.Telefono,
                                Status = true
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // ← NUEVO: Lógica diferenciada para edición vs wizard
                if (EsEdicion)
                {
                    TempData["Mensaje"] = $"✅ Información general de {Empleado.Names} {Empleado.Apellido} actualizada correctamente.";
                    return RedirectToPage("/Operadores/Detalles", new { id = Empleado.Id });
                }

                // Comportamiento original del wizard
                if (accion == "continuar")
                {
                    TempData["MensajeWizard"] = "✅ Paso 2/3 completado. Datos generales guardados.";
                    return RedirectToPage("/Operadores/AltaVivienda", new { id = Empleado.Id });
                }
                else if (accion == "salir")
                {
                    TempData["Mensaje"] = $"✅ Datos generales de {Empleado.Names} {Empleado.Apellido} guardados.";
                    return RedirectToPage("/Operadores/Buscar");
                }
                else
                {
                    return RedirectToPage("/Operadores/AltaVivienda", new { id = Empleado.Id });
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"❌ Error al guardar: {ex.Message}";
                if (ex.InnerException != null)
                {
                    Mensaje += $" | Detalle: {ex.InnerException.Message}";
                }
                NombreEmpleado = $"{Empleado.Names} {Empleado.Apellido}";
                return Page();
            }
        }

        public class ReferenciaTemp
        {
            public int Id { get; set; }
            public string Nombre { get; set; }
            public string Relacion { get; set; }
            public string Telefono { get; set; }
        }
    }
}