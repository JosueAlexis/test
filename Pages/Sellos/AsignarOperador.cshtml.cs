using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace ProyectoRH2025.Pages.Sellos
{
    public class AsignarOperadorModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AsignarOperadorModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SelectListItem> Sellos { get; set; } = new();
        public List<SelectListItem> Operadores { get; set; } = new();
        public List<SelectListItem> Unidades { get; set; } = new();
        public List<SelectListItem> TiposAsignacion { get; set; } = new();

        [BindProperty, Required(ErrorMessage = "Debe seleccionar un sello")]
        public int IdSello { get; set; }

        [BindProperty, Required(ErrorMessage = "Debe seleccionar un operador")]
        public int IdOperador { get; set; }

        [BindProperty]
        public int? IdOperador2 { get; set; }

        [BindProperty, Required(ErrorMessage = "Debe seleccionar una unidad")]
        public int IdUnidad { get; set; }

        [BindProperty, Required(ErrorMessage = "Debe seleccionar el tipo de asignación")]
        public int TipoAsignacion { get; set; }

        [BindProperty, Required(ErrorMessage = "La ruta es obligatoria")]
        public string? Ruta { get; set; }

        [BindProperty, Required(ErrorMessage = "La caja es obligatoria")]
        public string? Caja { get; set; }

        [BindProperty]
        public string? Comentarios { get; set; }

        public string? Mensaje { get; set; }
        public string? MensajeExito { get; set; }

        // ==========================================
        // MÉTODO GET
        // ==========================================
        public async Task<IActionResult> OnGetAsync()
        {
            await CargarDatosAsync();
            return Page();
        }

        // ==========================================
        // MÉTODO POST
        // ==========================================
        public async Task<IActionResult> OnPostAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");
            if (idUsuario == null)
            {
                Mensaje = "⚠️ Sesión expirada.";
                await CargarDatosAsync();
                return Page();
            }

            if (!ModelState.IsValid)
            {
                Mensaje = "❌ Por favor completa todos los campos obligatorios.";
                await CargarDatosAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Ruta) || string.IsNullOrWhiteSpace(Caja))
            {
                Mensaje = "❌ Los campos Ruta y Caja no pueden estar vacíos.";
                await CargarDatosAsync();
                return Page();
            }

            // ✅ Validar que el sello pertenezca al usuario (supervisor) logueado
            var sello = await _context.TblSellos
                .FirstOrDefaultAsync(s => s.Id == IdSello && s.Status == 14 && s.SupervisorId == idUsuario);

            if (sello == null)
            {
                Mensaje = "❌ El sello no está disponible o no está asignado a usted.";
                await CargarDatosAsync();
                return Page();
            }

            // ✅ Validación de comboy: no puede ser el mismo operador
            if (TipoAsignacion == 1 && IdOperador2.HasValue && IdOperador == IdOperador2.Value)
            {
                Mensaje = "❌ No puedes seleccionar el mismo operador dos veces en Comboy.";
                await CargarDatosAsync();
                return Page();
            }

            // ✅ VALIDAR QUE LA UNIDAD PERTENEZCA A LAS CUENTAS DEL SUPERVISOR (CON SP)
            var unidadValida = await ValidarUnidadPerteneceAUsuario(idUsuario.Value, IdUnidad);

            if (!unidadValida)
            {
                Mensaje = "❌ La unidad seleccionada no pertenece a tus cuentas asignadas.";
                await CargarDatosAsync();
                return Page();
            }

            // ✅ Crear la asignación
            var asignacion = new TblAsigSellos
            {
                idSello = IdSello,
                idUsuario = idUsuario.Value,
                Fentrega = DateTime.Now,
                idOperador = IdOperador,
                idOperador2 = TipoAsignacion == 1 ? IdOperador2 : null,
                TipoAsignacion = TipoAsignacion,
                idUnidad = IdUnidad,
                Ruta = Ruta.Trim(),
                Caja = Caja.Trim(),
                Comentarios = Comentarios?.Trim(),
                Status = 4, // Estado Trámite
                idSeAsigno = idUsuario.Value,
                FechaStatus4 = DateTime.Now
            };

            _context.TblAsigSellos.Add(asignacion);

            // ✅ Cambiar status del sello a 4 (Trámite)
            sello.Status = 4;

            await _context.SaveChangesAsync();

            Mensaje = "✅ Sello asignado correctamente al operador.";
            MensajeExito = "true";

            await CargarDatosAsync();
            return Page();
        }

        // ==========================================
        // CARGAR DATOS
        // ==========================================
        private async Task CargarDatosAsync()
        {
            var idUsuario = HttpContext.Session.GetInt32("idUsuario");

            // ✅ SELLOS: Solo mostrar los asignados al usuario logueado
            Sellos = await _context.TblSellos
                .Where(s => s.Status == 14 && s.SupervisorId == idUsuario)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Sello
                })
                .OrderBy(s => s.Text)
                .ToListAsync();

            if (!Sellos.Any())
            {
                Sellos.Add(new SelectListItem
                {
                    Value = "",
                    Text = "-- No tienes sellos asignados --",
                    Disabled = true,
                    Selected = true
                });
            }
            else
            {
                Sellos.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Seleccionar Sello --"
                });
            }

            // ✅ OPERADORES
            var operadoresData = await _context.Empleados
                .Where(e => e.Puesto == 1 && e.Status == 1 && e.CodClientes == "1")
                .Select(e => new
                {
                    e.Id,
                    e.Reloj,
                    e.Names,
                    e.Apellido,
                    e.Apellido2
                })
                .ToListAsync();

            Operadores = operadoresData
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = $"{e.Reloj} - {e.Names} {e.Apellido} {e.Apellido2}"
                })
                .OrderBy(e => e.Text)
                .ToList();

            Operadores.Insert(0, new SelectListItem
            {
                Value = "",
                Text = "-- Seleccionar Operador --"
            });

            // ✅ UNIDADES USANDO STORED PROCEDURE
            if (idUsuario.HasValue)
            {
                var unidadesData = await ObtenerUnidadesPorUsuarioSP(idUsuario.Value);

                if (unidadesData.Any())
                {
                    Unidades = unidadesData
                        .Select(u => new SelectListItem
                        {
                            Value = u.Id.ToString(),
                            Text = string.IsNullOrEmpty(u.Placas)
                                ? $"Unidad {u.NumUnidad}"
                                : $"Unidad {u.NumUnidad} - {u.Placas}"
                        })
                        .ToList();

                    Unidades.Insert(0, new SelectListItem
                    {
                        Value = "",
                        Text = "-- Seleccionar Unidad --"
                    });
                }
                else
                {
                    Unidades.Add(new SelectListItem
                    {
                        Value = "",
                        Text = "-- No tienes unidades asignadas --",
                        Disabled = true,
                        Selected = true
                    });
                }
            }

            // ✅ TIPOS DE ASIGNACIÓN
            TiposAsignacion = await _context.TblTipoAsignacion
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Nombre
                })
                .ToListAsync();

            TiposAsignacion.Insert(0, new SelectListItem
            {
                Value = "",
                Text = "-- Seleccionar Tipo --"
            });
        }

        // ==========================================
        // 🔥 MÉTODO PRIVADO: OBTENER UNIDADES POR USUARIO (STORED PROCEDURE)
        // ==========================================
        private async Task<List<UnidadDTO>> ObtenerUnidadesPorUsuarioSP(int idUsuario)
        {
            var unidades = new List<UnidadDTO>();

            try
            {
                var idUsuarioParam = new SqlParameter("@IdUsuario", idUsuario);

                // Ejecutar SP usando FromSqlRaw
                var resultado = await _context.Database
                    .SqlQueryRaw<UnidadDTO>(
                        "EXEC sp_ObtenerUnidadesPorUsuario @IdUsuario",
                        idUsuarioParam
                    )
                    .ToListAsync();

                return resultado;
            }
            catch (Exception ex)
            {
                // Log del error
                Console.WriteLine($"Error al ejecutar SP: {ex.Message}");
                return new List<UnidadDTO>();
            }
        }

        // ==========================================
        // 🔥 MÉTODO PRIVADO: VALIDAR UNIDAD PERTENECE A USUARIO
        // ==========================================
        private async Task<bool> ValidarUnidadPerteneceAUsuario(int idUsuario, int idUnidad)
        {
            try
            {
                var unidades = await ObtenerUnidadesPorUsuarioSP(idUsuario);
                return unidades.Any(u => u.Id == idUnidad);
            }
            catch
            {
                return false;
            }
        }

        // ==========================================
        // DTO PARA UNIDADES
        // ==========================================
        public class UnidadDTO
        {
            public int Id { get; set; }
            public int NumUnidad { get; set; }
            public string? Placas { get; set; }
            public int IdCuenta { get; set; }
            public string? NombreCuenta { get; set; }
        }
    }
}