using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Models.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Operadores
{
    public class AltaViviendaModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AltaViviendaModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ViviendaEmple DomicilioActual { get; set; } = new ViviendaEmple();

        [BindProperty]
        public ViviendaEmple DomicilioAnterior1 { get; set; } = new ViviendaEmple();

        [BindProperty]
        public ViviendaEmple DomicilioAnterior2 { get; set; } = new ViviendaEmple();

        [BindProperty]
        public bool TieneAutoPropio { get; set; }

        [TempData]
        public string Mensaje { get; set; }

        public string NombreEmpleado { get; set; }

        public bool EsEdicion { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var empleado = await _context.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id.Value);

            if (empleado == null) return NotFound();

            NombreEmpleado = $"{empleado.Names} {empleado.Apellido}";

            var domicilios = await _context.tblViviendaEmple
                .AsNoTracking()
                .Where(v => v.idEmpleado == id.Value)
                .ToListAsync();

            var domActual = domicilios.FirstOrDefault(v => v.TipoDomicilio == TipoDomicilio.Actual);
            var domAnt1 = domicilios.FirstOrDefault(v => v.TipoDomicilio == TipoDomicilio.Anterior1);
            var domAnt2 = domicilios.FirstOrDefault(v => v.TipoDomicilio == TipoDomicilio.Anterior2);

            if (domActual != null)
            {
                DomicilioActual = domActual;
                TieneAutoPropio = domActual.AutoPropio ?? false;
            }

            if (domAnt1 != null) DomicilioAnterior1 = domAnt1;
            if (domAnt2 != null) DomicilioAnterior2 = domAnt2;

            EsEdicion = Request.Query["edit"] == "true";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var empleado = await _context.Empleados
                .AsNoTracking()
                .Select(e => new { e.Id, e.Names, e.Apellido })
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empleado == null) return NotFound();

            try
            {
                if (string.IsNullOrWhiteSpace(DomicilioActual.Calle) ||
                    string.IsNullOrWhiteSpace(DomicilioActual.Ciudad) ||
                    string.IsNullOrWhiteSpace(DomicilioActual.Estado))
                {
                    Mensaje = "❌ Debe completar al menos Calle, Ciudad y Estado del domicilio actual.";
                    NombreEmpleado = $"{empleado.Names} {empleado.Apellido}";
                    return Page();
                }

                if (!DomicilioActual.TipoVivienda.HasValue)
                {
                    Mensaje = "❌ Debe seleccionar el tipo de vivienda.";
                    NombreEmpleado = $"{empleado.Names} {empleado.Apellido}";
                    return Page();
                }

                await GuardarDomicilio(DomicilioActual, TipoDomicilio.Actual, id);

                if (TieneDatos(DomicilioAnterior1))
                {
                    await GuardarDomicilio(DomicilioAnterior1, TipoDomicilio.Anterior1, id);
                }

                if (TieneDatos(DomicilioAnterior2))
                {
                    await GuardarDomicilio(DomicilioAnterior2, TipoDomicilio.Anterior2, id);
                }

                await _context.SaveChangesAsync();

                if (EsEdicion)
                {
                    TempData["Mensaje"] = $"✅ Información de vivienda de {empleado.Names} {empleado.Apellido} actualizada correctamente.";
                    return RedirectToPage("/Operadores/Detalles", new { id = empleado.Id });
                }
                else
                {
                    TempData["Mensaje"] = $"✅ Vivienda guardada. Ahora completa los documentos del empleado.";
                    return RedirectToPage("/Operadores/AltaDocumentos", new { id = empleado.Id });
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"❌ Error al guardar: {ex.Message}";
                if (ex.InnerException != null)
                {
                    Mensaje += $" | Detalle: {ex.InnerException.Message}";
                }
                NombreEmpleado = $"{empleado.Names} {empleado.Apellido}";
                return Page();
            }
        }

        private async Task GuardarDomicilio(ViviendaEmple domicilio, TipoDomicilio tipo, int empleadoId)
        {
            var existente = await _context.tblViviendaEmple
                .FirstOrDefaultAsync(v => v.idEmpleado == empleadoId && v.TipoDomicilio == tipo);

            if (existente != null)
            {
                existente.Calle = domicilio.Calle;
                existente.NoExterior = domicilio.NoExterior;
                existente.NoInterior = domicilio.NoInterior;
                existente.Colonia = domicilio.Colonia;
                existente.Ciudad = domicilio.Ciudad;
                existente.Municipio = domicilio.Municipio;
                existente.Estado = domicilio.Estado;
                existente.Pais = domicilio.Pais ?? "México";
                existente.codPostal = domicilio.codPostal;
                existente.TipoVivienda = domicilio.TipoVivienda;
                existente.NoCredito = domicilio.NoCredito;
                existente.AutoPropio = tipo == TipoDomicilio.Actual ? TieneAutoPropio : (bool?)null;
            }
            else
            {
                var nuevo = new ViviendaEmple
                {
                    idEmpleado = empleadoId,
                    TipoDomicilio = tipo,
                    Calle = domicilio.Calle,
                    NoExterior = domicilio.NoExterior,
                    NoInterior = domicilio.NoInterior,
                    Colonia = domicilio.Colonia,
                    Ciudad = domicilio.Ciudad,
                    Municipio = domicilio.Municipio,
                    Estado = domicilio.Estado,
                    Pais = domicilio.Pais ?? "México",
                    codPostal = domicilio.codPostal,
                    TipoVivienda = domicilio.TipoVivienda,
                    NoCredito = domicilio.NoCredito,
                    AutoPropio = tipo == TipoDomicilio.Actual ? TieneAutoPropio : (bool?)null
                };
                _context.tblViviendaEmple.Add(nuevo);
            }
        }

        private bool TieneDatos(ViviendaEmple dom)
        {
            return !string.IsNullOrWhiteSpace(dom?.Calle) ||
                   !string.IsNullOrWhiteSpace(dom?.Ciudad) ||
                   !string.IsNullOrWhiteSpace(dom?.Estado);
        }
    }
}