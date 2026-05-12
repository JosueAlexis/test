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

        // ✅ Nuevas listas estructuradas para enviar el IdCuenta a la vista
        public List<SelloOptionDTO> SellosList { get; set; } = new();
        public List<UnidadDTO> UnidadesList { get; set; } = new();

        public List<SelectListItem> Operadores { get; set; } = new();
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

            // Obtener permisos del usuario
            var misCuentasIds = await _context.TblUsuariosCuentas
                .Where(uc => uc.IdUsuario == idUsuario.Value && uc.EsActivo)
                .Select(uc => uc.IdCuenta)
                .ToListAsync();

            bool esSuperUsuario = misCuentasIds.Contains(7);

            var sello = await _context.TblSellos.FirstOrDefaultAsync(s => s.Id == IdSello && s.Status == 14);

            if (sello == null)
            {
                Mensaje = "❌ El sello no está disponible o no existe.";
                await CargarDatosAsync();
                return Page();
            }

            // ✅ Validar que el sello pertenezca a las cuentas del usuario (si no es maestro)
            if (!esSuperUsuario && (!sello.IdCuenta.HasValue || !misCuentasIds.Contains(sello.IdCuenta.Value)))
            {
                Mensaje = "❌ No tienes permisos sobre la cuenta de este sello.";
                await CargarDatosAsync();
                return Page();
            }

            if (TipoAsignacion == 1 && IdOperador2.HasValue && IdOperador == IdOperador2.Value)
            {
                Mensaje = "❌ No puedes seleccionar el mismo operador dos veces en Comboy.";
                await CargarDatosAsync();
                return Page();
            }

            var unidadValida = await ValidarUnidadPerteneceAUsuario(idUsuario.Value, IdUnidad);

            if (!unidadValida)
            {
                Mensaje = "❌ La unidad seleccionada no pertenece a tus cuentas asignadas.";
                await CargarDatosAsync();
                return Page();
            }

            // Crear la asignación
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
                Status = 4,
                idSeAsigno = idUsuario.Value,
                FechaStatus4 = DateTime.Now
            };

            _context.TblAsigSellos.Add(asignacion);
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
            if (idUsuario == null) return;

            var misCuentasIds = await _context.TblUsuariosCuentas
                .Where(uc => uc.IdUsuario == idUsuario.Value && uc.EsActivo)
                .Select(uc => uc.IdCuenta)
                .ToListAsync();

            bool esSuperUsuario = misCuentasIds.Contains(7);

            // 1. ✅ CARGAR SELLOS MOSTRANDO LA CUENTA
            string sqlSellos = @"
                SELECT 
                    s.Id, 
                    s.Sello, 
                    s.IdCuenta, 
                    ISNULL(c.NombreCuenta, 'Sin Cuenta Asignada') AS NombreCuenta
                FROM tblSellos s
                LEFT JOIN tblCuentas c ON s.IdCuenta = c.Id
                WHERE s.Status = 14";

            var sellosData = await _context.Database.SqlQueryRaw<SelloOptionDTO>(sqlSellos).ToListAsync();

            // Filtrar por permisos si no es cuenta maestra
            if (!esSuperUsuario)
            {
                sellosData = sellosData.Where(s => s.IdCuenta.HasValue && misCuentasIds.Contains(s.IdCuenta.Value)).ToList();
            }

            SellosList = sellosData.OrderBy(s => s.Sello).ToList();

            // 2. OPERADORES
            var operadoresData = await _context.Empleados
                .Where(e => e.Puesto == 1 && e.Status == 1 && e.CodClientes == "1")
                .Select(e => new { e.Id, e.Reloj, e.Names, e.Apellido, e.Apellido2 })
                .ToListAsync();

            Operadores = operadoresData
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = $"{e.Reloj} - {e.Names} {e.Apellido} {e.Apellido2}" })
                .OrderBy(e => e.Text)
                .ToList();

            Operadores.Insert(0, new SelectListItem { Value = "", Text = "-- Seleccionar Operador --" });

            // 3. ✅ CARGAR TODAS LAS UNIDADES PERMITIDAS
            UnidadesList = await ObtenerUnidadesPorUsuarioSP(idUsuario.Value);

            // 4. TIPOS DE ASIGNACIÓN
            TiposAsignacion = await _context.TblTipoAsignacion
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Nombre })
                .ToListAsync();

            TiposAsignacion.Insert(0, new SelectListItem { Value = "", Text = "-- Seleccionar Tipo --" });
        }

        private async Task<List<UnidadDTO>> ObtenerUnidadesPorUsuarioSP(int idUsuario)
        {
            try
            {
                var idUsuarioParam = new SqlParameter("@IdUsuario", idUsuario);

                return await _context.Database
                    .SqlQueryRaw<UnidadDTO>(
                        "EXEC sp_ObtenerUnidadesPorUsuario @IdUsuario",
                        idUsuarioParam
                    )
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al ejecutar SP: {ex.Message}");
                return new List<UnidadDTO>();
            }
        }

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
        // DTOs PARA VISTA
        // ==========================================
        public class UnidadDTO
        {
            public int Id { get; set; }
            public int NumUnidad { get; set; }
            public string? Placas { get; set; }
            public int IdCuenta { get; set; }
            public string? NombreCuenta { get; set; }
        }

        public class SelloOptionDTO
        {
            public int Id { get; set; }
            public string Sello { get; set; }
            public int? IdCuenta { get; set; }
            public string NombreCuenta { get; set; }
        }
    }
}