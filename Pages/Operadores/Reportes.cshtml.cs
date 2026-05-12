using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ClosedXML.Excel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Operadores
{
    public class ReportesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ReportesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public OpcionesReporte Opciones { get; set; } = new OpcionesReporte();

        public void OnGet()
        {
            // Valores por defecto al abrir la página
            Opciones.FiltroEstatus = "activos"; // Por defecto solo traer a los que están laborando
            Opciones.IncluirBasicos = true;
            Opciones.IncluirGeneral = true;
            Opciones.IncluirVivienda = true;
            Opciones.IncluirDocumentos = true;
        }

        public async Task<IActionResult> OnPostExportarExcelAsync()
        {
            // 1. Iniciamos la consulta base
            var query = _context.Empleados.AsQueryable();

            // 2. Filtro de Estatus (el Status es de tipo numérico int)
            if (Opciones.FiltroEstatus == "activos")
            {
                // Asumimos que 1 significa Activo
                query = query.Where(e => e.Status == 1);
            }
            else if (Opciones.FiltroEstatus == "inactivos")
            {
                // Asumimos que 0 significa Inactivo (Cambiar por 2 u otro valor si tu BD usa otro número)
                query = query.Where(e => e.Status == 0);
            }
            // Si es "todos", no agregamos ningún filtro Where.

            // Ejecutamos la consulta de empleados
            var empleados = await query.ToListAsync();
            var listaIds = empleados.Select(e => (int?)e.Id).ToList();

            // Si solicitaron documentos, consultamos la tabla de documentos
            var documentos = Opciones.IncluirDocumentos && empleados.Any()
                ? await _context.tblDocumentosEmpleado
                    .Where(d => listaIds.Contains(d.idEmpleado))
                    .ToListAsync()
                : null;

            // Si solicitaron vivienda, consultamos la tabla de viviendas
            var viviendas = Opciones.IncluirVivienda && empleados.Any()
                ? await _context.tblViviendaEmple
                    .Where(v => listaIds.Contains(v.idEmpleado))
                    .ToListAsync()
                : null;

            // 4. Generar el archivo Excel con ClosedXML
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Reporte Empleados");
                int currentRow = 1;
                int currentCol = 1;

                // --- CABECERAS ---
                if (Opciones.IncluirBasicos)
                {
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Reloj";
                    worksheet.Cell(currentRow, currentCol++).Value = "Estatus";
                    worksheet.Cell(currentRow, currentCol++).Value = "Nombres";
                    worksheet.Cell(currentRow, currentCol++).Value = "Apellidos";
                    worksheet.Cell(currentRow, currentCol++).Value = "Empresa";
                    worksheet.Cell(currentRow, currentCol++).Value = "Fecha Ingreso";
                    worksheet.Cell(currentRow, currentCol++).Value = "Email";
                    worksheet.Cell(currentRow, currentCol++).Value = "Teléfono";
                    worksheet.Cell(currentRow, currentCol++).Value = "Tel. Emergencia";
                    worksheet.Cell(currentRow, currentCol++).Value = "RFC";
                    worksheet.Cell(currentRow, currentCol++).Value = "CURP";
                    worksheet.Cell(currentRow, currentCol++).Value = "NSS";
                }

                if (Opciones.IncluirGeneral)
                {
                    worksheet.Cell(currentRow, currentCol++).Value = "Fecha Nacimiento";
                    worksheet.Cell(currentRow, currentCol++).Value = "Estado Civil";
                    worksheet.Cell(currentRow, currentCol++).Value = "Nombre Cónyuge";
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Hijos";
                    worksheet.Cell(currentRow, currentCol++).Value = "Escolaridad";
                    worksheet.Cell(currentRow, currentCol++).Value = "Nivel Inglés";
                    worksheet.Cell(currentRow, currentCol++).Value = "Tipo Sangre";
                    worksheet.Cell(currentRow, currentCol++).Value = "Fuma";
                    worksheet.Cell(currentRow, currentCol++).Value = "Consume Alcohol";
                    worksheet.Cell(currentRow, currentCol++).Value = "Dopping";
                    worksheet.Cell(currentRow, currentCol++).Value = "Diabetes";
                    worksheet.Cell(currentRow, currentCol++).Value = "Hipertensión";
                    worksheet.Cell(currentRow, currentCol++).Value = "Enf. Crónica";
                    worksheet.Cell(currentRow, currentCol++).Value = "Cuenta Infonavit";
                }

                if (Opciones.IncluirVivienda)
                {
                    worksheet.Cell(currentRow, currentCol++).Value = "Calle";
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Ext";
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Int";
                    worksheet.Cell(currentRow, currentCol++).Value = "Colonia";
                    worksheet.Cell(currentRow, currentCol++).Value = "Ciudad";
                    worksheet.Cell(currentRow, currentCol++).Value = "Estado";
                    worksheet.Cell(currentRow, currentCol++).Value = "C.P.";
                    worksheet.Cell(currentRow, currentCol++).Value = "Tipo Vivienda";
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Crédito (Si aplica)";
                    worksheet.Cell(currentRow, currentCol++).Value = "Tiene Auto Propio";
                }

                if (Opciones.IncluirDocumentos)
                {
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Licencia";
                    worksheet.Cell(currentRow, currentCol++).Value = "Vencimiento Licencia";
                    worksheet.Cell(currentRow, currentCol++).Value = "No. Apto Médico";
                    worksheet.Cell(currentRow, currentCol++).Value = "Vencimiento Apto Médico";
                    worksheet.Cell(currentRow, currentCol++).Value = "Gafete ANAM (Vigencia)";
                    worksheet.Cell(currentRow, currentCol++).Value = "Gafete ANAM (Chip)";
                    worksheet.Cell(currentRow, currentCol++).Value = "Visa Láser (No.)";
                    worksheet.Cell(currentRow, currentCol++).Value = "Visa Láser (Vigencia)";
                    worksheet.Cell(currentRow, currentCol++).Value = "FAST (No.)";
                    worksheet.Cell(currentRow, currentCol++).Value = "FAST (Vigencia)";
                }

                // Dar formato a las cabeceras
                int maxCols = currentCol > 1 ? currentCol - 1 : 1;
                var headerRange = worksheet.Range(1, 1, 1, maxCols);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.ForestGreen;
                headerRange.Style.Font.FontColor = XLColor.White;

                // --- DATOS ---
                foreach (var emp in empleados)
                {
                    currentRow++;
                    currentCol = 1;

                    // Buscamos si tiene documentos y vivienda ligados
                    var docsEmp = documentos?.FirstOrDefault(d => d.idEmpleado == emp.Id);
                    var vivEmp = viviendas?.FirstOrDefault(v => v.idEmpleado == emp.Id);

                    if (Opciones.IncluirBasicos)
                    {
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Reloj;
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Status == 1 ? "Activo" : "Inactivo";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Names;
                        worksheet.Cell(currentRow, currentCol++).Value = $"{emp.Apellido} {emp.Apellido2}".Trim();
                        worksheet.Cell(currentRow, currentCol++).Value = emp.CodClientes == "1" ? "STIL" : (emp.CodClientes == "2" ? "AKNA" : emp.CodClientes);
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Fingreso?.ToString("dd/MM/yyyy") ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Email;
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Telefono ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.TelEmergencia ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Rfc;
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Curp;
                        worksheet.Cell(currentRow, currentCol++).Value = emp.NumSSocial;
                    }

                    if (Opciones.IncluirGeneral)
                    {
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Fnacimiento?.ToString("dd/MM/yyyy") ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.EstadoCivil.ToString();
                        worksheet.Cell(currentRow, currentCol++).Value = emp.NombreConyuge ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.NumHijos ?? 0;
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Escolaridad.ToString();
                        worksheet.Cell(currentRow, currentCol++).Value = emp.NivelIngles.ToString();
                        worksheet.Cell(currentRow, currentCol++).Value = emp.TipoSangre ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Fuma == true ? "Sí" : "No";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Alcohol == true ? "Sí" : "No";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Dopping == true ? "Sí" : "No";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Diabetes == true ? "Sí" : "No";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.Hipertension == true ? "Sí" : "No";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.EnfermedadCronica == true ? "Sí" : "No";
                        worksheet.Cell(currentRow, currentCol++).Value = emp.CuentaInfonavit == true ? "Sí" : "No";
                    }

                    if (Opciones.IncluirVivienda)
                    {
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.Calle ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.NoExterior?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.NoInterior?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.Colonia ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.Ciudad ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.Estado ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.codPostal?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.TipoVivienda?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.NoCredito?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = vivEmp?.AutoPropio == true ? "Sí" : "No";
                    }

                    if (Opciones.IncluirDocumentos)
                    {
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.NumLicencia?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.Vigencia?.ToString("dd/MM/yyyy") ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.NumExpedienteMedico?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.VigenteAptoHasta?.ToString("dd/MM/yyyy") ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.GafeteANAMVigencia?.ToString("dd/MM/yyyy") ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.GafeteANAMChip?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.VisaLaserNumero?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.VisaLaserVigencia?.ToString("dd/MM/yyyy") ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.FastNumero?.ToString() ?? "";
                        worksheet.Cell(currentRow, currentCol++).Value = docsEmp?.FastVigencia?.ToString("dd/MM/yyyy") ?? "";
                    }
                }

                // Aplicar el Autofiltro de Excel 
                if (empleados.Count > 0)
                {
                    worksheet.Range(1, 1, currentRow, maxCols).SetAutoFilter();
                }
                else
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "No hay empleados registrados con los criterios seleccionados.";
                    worksheet.Range(currentRow, 1, currentRow, maxCols).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    // Nombramos el archivo dinámicamente según el estatus
                    string estatusNombre = Opciones.FiltroEstatus == "activos" ? "Activos" : (Opciones.FiltroEstatus == "inactivos" ? "Inactivos" : "Todos");
                    string fileName = $"BaseDeDatos_RH_{estatusNombre}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                    return File(content, contentType, fileName);
                }
            }
        }
    }

    public class OpcionesReporte
    {
        public string FiltroEstatus { get; set; } // "activos", "inactivos", "todos"
        public bool IncluirBasicos { get; set; }
        public bool IncluirGeneral { get; set; }
        public bool IncluirVivienda { get; set; }
        public bool IncluirDocumentos { get; set; }
    }
}