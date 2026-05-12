using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.IT
{
    public class ImportarAKNAModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ImportarAKNAModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public List<FilaImportacion> Filas { get; set; } = new();

        [TempData]
        public string MensajeExito { get; set; }

        [TempData]
        public string MensajeError { get; set; }

        public void OnGet()
        {
        }

        // PASO 1: LEER EL ARCHIVO Y VALIDAR (VISTA PREVIA)
        public async Task<IActionResult> OnPostAnalizarAsync(IFormFile archivoCsv)
        {
            if (archivoCsv == null || archivoCsv.Length == 0)
            {
                MensajeError = "Por favor selecciona un archivo CSV.";
                return Page();
            }

            try
            {
                using var stream = archivoCsv.OpenReadStream();
                using var parser = new TextFieldParser(stream);
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;

                // Saltar el encabezado
                if (!parser.EndOfData) parser.ReadFields();

                int numeroFila = 2;
                while (!parser.EndOfData)
                {
                    var campos = parser.ReadFields();

                    // Solo requerimos que tenga al menos 3 columnas para poder leer el Reloj
                    if (campos != null && campos.Length >= 3)
                    {
                        var fila = new FilaImportacion
                        {
                            FilaOrigen = numeroFila,
                            // Leemos de forma segura: Si la columna existe, la toma. Si no, la deja vacía ("")
                            Reloj = campos.Length > 2 ? campos[2].Trim() : "",
                            IMEI = campos.Length > 3 ? campos[3].Trim() : "",
                            Modelo = campos.Length > 4 ? campos[4].Trim() : "",
                            Telefono = campos.Length > 5 ? campos[5].Trim() : "",
                            AccesoriosTexto = campos.Length > 6 ? campos[6].Trim() : ""
                        };

                        // 🤖 EL ROBOT LECTOR DE ACCESORIOS (OPCIÓN A) 🤖
                        string acc = fila.AccesoriosTexto.ToLower();
                        fila.Funda = acc.Contains("funda");
                        fila.Vidrio = acc.Contains("vidrio") || acc.Contains("mica");
                        fila.Soporte = acc.Contains("soporte");

                        // Lógica para cargadores
                        if (acc.Contains("cargador"))
                        {
                            if (acc.Contains("carro") || acc.Contains("cenicero")) fila.CargadorCenicero = true;
                            if (acc.Contains("pared") || (!acc.Contains("carro") && !acc.Contains("cenicero"))) fila.CargadorPared = true;
                        }

                        // Validar contra la BD
                        await ValidarFilaAsync(fila);
                        Filas.Add(fila);
                    }
                    numeroFila++;
                }

                if (!Filas.Any()) MensajeError = "El archivo no contiene datos válidos o el formato es incorrecto.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al leer el archivo: {ex.Message}";
            }

            return Page();
        }
        public async Task<IActionResult> OnPostConfirmarAsync()
        {
            if (Filas == null || !Filas.Any())
            {
                MensajeError = "No hay datos para importar.";
                return Page();
            }

            var idUsuarioSesion = HttpContext.Session.GetInt32("idUsuario");
            var idSucursalSesion = HttpContext.Session.GetInt32("idSucursal") ?? 1;

            if (idUsuarioSesion == null) return RedirectToPage("/Login");

            bool hayErrores = false;

            foreach (var fila in Filas)
            {
                await ValidarFilaAsync(fila);
                if (!fila.EsValida) hayErrores = true;
            }

            if (hayErrores)
            {
                MensajeError = "Aún hay filas con errores. Por favor, corrígelas o elimínalas antes de continuar.";
                return Page();
            }

            int importados = 0;
            foreach (var fila in Filas)
            {
                var nuevoEquipo = new TblInventarioCel
                {
                    idempleado = fila.IdEmpleadoReal,
                    idModelo = fila.IdModeloReal,
                    idMarca = fila.IdMarcaReal,
                    NoTelefono = fila.Telefono,
                    IMEI = fila.IMEI,
                    idPuesto = fila.IdPuestoReal,
                    idUsuario = idUsuarioSesion.Value,
                    idSucursal = idSucursalSesion,
                    Fentrega = DateTime.Now,
                    idEstatus = 1,
                    idHuella = 0,
                    Comentarios = $"[Importado de Excel] {fila.AccesoriosTexto}"
                };

                _context.TblInventarioCel.Add(nuevoEquipo);
                await _context.SaveChangesAsync();

                var accesorios = new TblInventarioAccesorios
                {
                    idInventario = nuevoEquipo.id,
                    FundaUsoRudo = fila.Funda,
                    CargadorPared = fila.CargadorPared,
                    CargadorCenicero = fila.CargadorCenicero,
                    VidrioTemplado = fila.Vidrio,
                    SoporteCamion = fila.Soporte
                };

                _context.TblInventarioAccesorios.Add(accesorios);
                await _context.SaveChangesAsync();
                importados++;
            }

            TempData["MensajeExito"] = $"¡Se importaron {importados} equipos correctamente!";
            return RedirectToPage("/IT/InventarioCelulares");
        }

        private async Task ValidarFilaAsync(FilaImportacion fila)
        {
            fila.MensajeError = "";

            // 1. Validar Reloj (CORREGIDO: comparamos el int directo, sin .ToString())
            if (int.TryParse(fila.Reloj, out int relojNum))
            {
                var emp = await _context.Empleados.FirstOrDefaultAsync(e => e.Reloj == relojNum);
                if (emp == null) fila.MensajeError += "El Reloj no existe. ";
                else
                {
                    fila.IdEmpleadoReal = emp.Id;
                    fila.NombreEmpleado = $"{emp.Names} {emp.Apellido}".Trim();

                    int idPuesto = 0;
                    if (emp.Puesto != null) int.TryParse(emp.Puesto.ToString(), out idPuesto);
                    fila.IdPuestoReal = idPuesto;
                }
            }
            else fila.MensajeError += "Reloj inválido. ";

            // 2. Validar Modelo
            var modeloBD = await _context.TblModelosCel.FirstOrDefaultAsync(m => m.Modelo == fila.Modelo);
            if (modeloBD == null) fila.MensajeError += "Modelo no existe en el catálogo. ";
            else
            {
                fila.IdModeloReal = modeloBD.id;
                fila.IdMarcaReal = modeloBD.Marca;
            }

            // 3. Validar Duplicados
            if (await _context.TblInventarioCel.AnyAsync(i => i.IMEI == fila.IMEI))
                fila.MensajeError += "IMEI ya registrado. ";

            if (await _context.TblInventarioCel.AnyAsync(i => i.NoTelefono == fila.Telefono))
                fila.MensajeError += "Teléfono ya registrado. ";

            fila.EsValida = string.IsNullOrEmpty(fila.MensajeError);
        }
    }

    public class FilaImportacion
    {
        public int FilaOrigen { get; set; }
        public string Reloj { get; set; }
        public string NombreEmpleado { get; set; }
        public string IMEI { get; set; }
        public string Modelo { get; set; }
        public string Telefono { get; set; }
        public string AccesoriosTexto { get; set; }
        public bool Funda { get; set; }
        public bool CargadorPared { get; set; }
        public bool CargadorCenicero { get; set; }
        public bool Vidrio { get; set; }
        public bool Soporte { get; set; }
        public bool EsValida { get; set; }
        public string MensajeError { get; set; }
        public int IdEmpleadoReal { get; set; }
        public int IdModeloReal { get; set; }
        public int IdMarcaReal { get; set; }
        public int IdPuestoReal { get; set; }
    }
}