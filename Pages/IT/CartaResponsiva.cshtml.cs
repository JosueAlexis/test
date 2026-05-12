using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ProyectoRH2025.Pages.IT
{
    public class CartaResponsivaModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CartaResponsivaModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public TblInventarioCel NuevoEquipo { get; set; } = new();

        [BindProperty]
        public TblInventarioAccesorios Accesorios { get; set; } = new();
        public List<SelectListItem> ListaMarcas { get; set; } = new();

        [TempData]
        public string MensajeExito { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Validar sesión
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null) return RedirectToPage("/Login");

            // Cargar Marcas
            var marcas = await _context.TblMarcasCel.OrderBy(m => m.MarcaCel).ToListAsync();
            ListaMarcas = marcas.Select(m => new SelectListItem
            {
                Value = m.id.ToString(),
                Text = m.MarcaCel
            }).ToList();

            return Page();
        }

        // =======================================================
        // HANDLERS AJAX
        // =======================================================

        public async Task<JsonResult> OnGetEmpleadoPorRelojAsync(int reloj)
        {
            var empleado = await _context.Empleados
                .Where(e => e.Reloj == reloj)
                .FirstOrDefaultAsync();

            if (empleado != null)
            {
                // Como empleado.Puesto ya es int?, lo asignamos directo (si es nulo, le ponemos 0)
                int idPuesto = empleado.Puesto ?? 0;
                string nombrePuesto = "Puesto no definido";

                // Si tiene un ID de puesto válido, lo buscamos en el catálogo
                if (idPuesto > 0)
                {
                    var puestoObj = await _context.PuestoEmpleados
                        .Where(p => p.id == idPuesto)
                        .FirstOrDefaultAsync();

                    if (puestoObj != null)
                    {
                        nombrePuesto = puestoObj.Puesto;
                    }
                }

                return new JsonResult(new
                {
                    success = true,
                    idEmpleado = empleado.Id,
                    nombreCompleto = $"{empleado.Names} {empleado.Apellido} {empleado.Apellido2}".Trim(),
                    idPuesto = idPuesto,
                    nombrePuesto = nombrePuesto
                });
            }

            return new JsonResult(new { success = false });
        }
        public async Task<JsonResult> OnGetModelosPorMarcaAsync(int idMarca)
        {
            var modelos = await _context.TblModelosCel
                .Where(m => m.Marca == idMarca)
                .Select(m => new { m.id, m.Modelo })
                .ToListAsync();

            return new JsonResult(modelos);
        }

        public async Task<JsonResult> OnGetPrecioPorModeloAsync(int idModelo)
        {
            var precioObj = await _context.TblPreciosCel
                .Where(p => p.idModelo == idModelo)
                .FirstOrDefaultAsync();

            if (precioObj != null)
            {
                // Convertimos el texto de la base de datos a un decimal real
                decimal precioConvertido = 0;
                decimal.TryParse(precioObj.Precio, out precioConvertido);

                return new JsonResult(new { success = true, precio = precioConvertido, idPrecio = precioObj.id });
            }

            return new JsonResult(new { success = false });
        }
        // =======================================================
        // GUARDAR REGISTRO
        // =======================================================

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var idUsuarioSesion = HttpContext.Session.GetInt32("idUsuario");
                var idSucursalSesion = HttpContext.Session.GetInt32("idSucursal");

                if (idUsuarioSesion == null) return RedirectToPage("/Login");

                NuevoEquipo.idUsuario = idUsuarioSesion.Value;
                NuevoEquipo.idSucursal = idSucursalSesion ?? 1;
                NuevoEquipo.Fentrega = DateTime.Now;
                NuevoEquipo.idEstatus = 1;

                _context.TblInventarioCel.Add(NuevoEquipo);
                await _context.SaveChangesAsync();

                Accesorios.idInventario = NuevoEquipo.id;
                _context.TblInventarioAccesorios.Add(Accesorios);
                await _context.SaveChangesAsync();

                TempData["IdEquipoGenerado"] = NuevoEquipo.id;
                MensajeExito = "Equipo asignado correctamente. Generando carta...";

                // Redirigir a la vista de impresión (la crearemos después)
                return RedirectToPage("/IT/ImprimirCartaResponsiva", new { id = NuevoEquipo.id });
            }
            catch (Exception ex)
            {
                // Esto extraerá el mensaje exacto de rechazo de SQL Server
                string errorReal = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MensajeError = $"Detalle del error SQL: {errorReal}";
                return RedirectToPage();
            }
        }
    }
}