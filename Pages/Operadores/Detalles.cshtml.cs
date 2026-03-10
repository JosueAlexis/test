using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using ProyectoRH2025.Models.Enums;
using System.Globalization;

namespace ProyectoRH2025.Pages.Operadores
{
    public class DetallesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetallesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Empleado? Empleado { get; set; }
        public string? ImagenBase64 { get; set; }
        public string? NombrePuesto { get; set; }
        public string? FechaIngresoFormateada { get; set; }
        public string? NumeroReloj { get; set; }
        public string? FechaNacimientoFormateada { get; set; }
        public string? TipoEmpleadoDesc { get; set; }
        public string? NombreCliente { get; set; }
        public string? FechaEgresoFormateada { get; set; }

        // ════════════════════════════════════════════════════════════════
        // MODELOS DE DOCUMENTOS
        // ════════════════════════════════════════════════════════════════
        public class DocumentoLicencia
        {
            public string? NumeroLicencia { get; set; }
            public string? Clasificacion { get; set; }
            public DateTime? FechaAntiguedad { get; set; }
            public int? AnosAntiguedad { get; set; }
            public DateTime? Vigencia { get; set; }
        }

        public class DocumentoAPTO
        {
            public string? NumeroExpediente { get; set; }
            public DateTime? VigenteDesde { get; set; }
            public DateTime? VigenteHasta { get; set; }
        }

        public class DocumentoAntecedentes
        {
            public DateTime? FechaAntecedentes { get; set; }
        }

        public class DocumentoEstudioSocio
        {
            public DateTime? FechaEstudio { get; set; }
        }

        public class DocumentoEvaluacion
        {
            public DateTime? FechaEvaluacion { get; set; }
            public string? Proposito { get; set; }
            public decimal? Nota { get; set; }
            public string? NombreEvaluador { get; set; }
            public string? Comentarios { get; set; }
        }

        public class DocumentoPolizaVida
        {
            public DateTime? FechaPoliza { get; set; }
            public string? NombreBeneficiario { get; set; }
            public string? Parentesco { get; set; }
            public decimal? Porcentaje { get; set; }
            public string? Direccion { get; set; }
        }

        public class DocumentoBanorte
        {
            public string? NumCuenta { get; set; }
            public string? CLABE { get; set; }
            public string? NumTarjeta { get; set; }
            public string? NombreBeneficiario { get; set; }
            public string? Parentesco { get; set; }
            public decimal? Porcentaje { get; set; }
            public string? Direccion { get; set; }
        }

        // ✅ NUEVO: Clase para campos AKNA
        public class DocumentosAkna
        {
            public string? VisaLaserNumero { get; set; }
            public DateTime? VisaLaserVigencia { get; set; }
            public string? FastNumero { get; set; }
            public DateTime? FastVigencia { get; set; }
            public DateTime? GafeteANAMVigencia { get; set; }
            public string? GafeteANAMChip { get; set; }
            public string? GafeteANAMUsuario { get; set; }
            public string? GafeteANAMCorreo { get; set; }
        }

        // Propiedades públicas para la vista
        public DocumentoLicencia? Licencia { get; set; }
        public DocumentoAPTO? APTO { get; set; }
        public DocumentoAntecedentes? Antecedentes { get; set; }
        public DocumentoEstudioSocio? EstudioSocio { get; set; }
        public DocumentoEvaluacion? Evaluacion { get; set; }
        public DocumentoPolizaVida? PolizaVida { get; set; }
        public DocumentoBanorte? Banorte { get; set; }

        // ✅ NUEVO
        public DocumentosAkna? CamposAkna { get; set; }

        public bool EsEmpleadoAkna
        {
            get
            {
                if (Empleado == null || string.IsNullOrWhiteSpace(Empleado.CodClientes))
                    return false;
                return Empleado.CodClientes == "2" && Empleado.TipEmpleado == 1 && Empleado.Puesto == 2;
            }
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Empleado = await _context.Empleados
                .Include(e => e.Viviendas)
                .Include(e => e.ReferenciasPersonales.Where(r => r.Status))
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (Empleado == null) return NotFound();

            NumeroReloj = Empleado.Reloj?.ToString() ?? "Sin reloj asignado";

            FechaIngresoFormateada = Empleado.Fingreso?.ToString(
                "dddd, dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));

            if (Empleado.Puesto.HasValue)
            {
                NombrePuesto = await _context.PuestoEmpleados
                    .Where(p => p.id == Empleado.Puesto)
                    .Select(p => p.Puesto)
                    .AsNoTracking()
                    .FirstOrDefaultAsync() ?? "Sin puesto asignado";
            }

            ImagenBase64 = await _context.ImagenesEmpleados
                .Where(i => i.idEmpleado == id)
                .Select(i => i.Imagen)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (Empleado.Fnacimiento.HasValue)
            {
                var nac = Empleado.Fnacimiento.Value;
                var hoy = DateTime.Today;
                int edad = hoy.Year - nac.Year;
                if (nac.Date > hoy.AddYears(-edad)) edad--;
                FechaNacimientoFormateada =
                    $"{nac.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"))} ({edad} años)";
            }
            else
            {
                FechaNacimientoFormateada = "No disponible";
            }

            if (Empleado.TipEmpleado.HasValue)
            {
                TipoEmpleadoDesc = await _context.TblTipoEmpleado
                    .Where(t => t.id == Empleado.TipEmpleado)
                    .Select(t => t.TipEmpleado)
                    .AsNoTracking()
                    .FirstOrDefaultAsync() ?? "No especificado";
            }
            else { TipoEmpleadoDesc = "No especificado"; }

            if (!string.IsNullOrWhiteSpace(Empleado.CodClientes)
                && int.TryParse(Empleado.CodClientes, out int codParsed))
            {
                NombreCliente = await _context.TblClientes
                    .Where(c => c.codCliente == codParsed)
                    .Select(c => c.Cliente)
                    .AsNoTracking()
                    .FirstOrDefaultAsync() ?? "No asignado";
            }
            else { NombreCliente = "No asignado"; }

            if (Empleado.Status != 1)
            {
                FechaEgresoFormateada = Empleado.Fegreso.HasValue
                    ? Empleado.Fegreso.Value.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"))
                    : "Baja sin fecha registrada";
            }

            // ════════════════════════════════════════════════════════════════
            // CARGAR TODOS LOS DOCUMENTOS
            // ════════════════════════════════════════════════════════════════
            var documentos = await _context.tblDocumentosEmpleado
                .Where(d => d.idEmpleado == id)
                .AsNoTracking()
                .ToListAsync();

            // 1 - Licencia de Conducir
            var docLicencia = documentos.FirstOrDefault(d => d.idTipDocumento == 1);
            if (docLicencia != null)
            {
                Licencia = new DocumentoLicencia
                {
                    NumeroLicencia = docLicencia.NumLicencia,
                    Clasificacion = docLicencia.Clasificacion,
                    FechaAntiguedad = docLicencia.Fechaantiguedad,
                    AnosAntiguedad = docLicencia.Anosantiguedad,
                    Vigencia = docLicencia.Vigencia
                };

                // ✅ Cargar campos AKNA desde el mismo registro de licencia
                if (EsEmpleadoAkna)
                {
                    CamposAkna = new DocumentosAkna
                    {
                        VisaLaserNumero = docLicencia.VisaLaserNumero,
                        VisaLaserVigencia = docLicencia.VisaLaserVigencia,
                        FastNumero = docLicencia.FastNumero,
                        FastVigencia = docLicencia.FastVigencia,
                        GafeteANAMVigencia = docLicencia.GafeteANAMVigencia,
                        GafeteANAMChip = docLicencia.GafeteANAMChip,
                        GafeteANAMUsuario = docLicencia.GafeteANAMUsuario,
                        GafeteANAMCorreo = docLicencia.GafeteANAMCorreo
                    };
                }
            }

            // 2 - Apto Médico
            var docApto = documentos.FirstOrDefault(d => d.idTipDocumento == 2);
            if (docApto != null)
            {
                APTO = new DocumentoAPTO
                {
                    NumeroExpediente = docApto.NumExpedienteMedico,
                    VigenteDesde = docApto.VigenteAptoDesde,
                    VigenteHasta = docApto.VigenteAptoHasta
                };
            }

            // 5 - Antecedentes No Penales
            var docAntecedentes = documentos.FirstOrDefault(d => d.idTipDocumento == 5);
            if (docAntecedentes != null)
            {
                Antecedentes = new DocumentoAntecedentes
                {
                    FechaAntecedentes = docAntecedentes.FechaAntecedentesNoPenales
                };
            }

            // 6 - Estudio Socioeconómico
            var docEstudio = documentos.FirstOrDefault(d => d.idTipDocumento == 6);
            if (docEstudio != null)
            {
                EstudioSocio = new DocumentoEstudioSocio
                {
                    FechaEstudio = docEstudio.FechaEstudioSocioeconomico
                };
            }

            // 7 - Evaluación de Manejo
            var docEvaluacion = documentos.FirstOrDefault(d => d.idTipDocumento == 7);
            if (docEvaluacion != null)
            {
                Evaluacion = new DocumentoEvaluacion
                {
                    FechaEvaluacion = docEvaluacion.FechaEvaluacionManejo,
                    Proposito = docEvaluacion.PropositoEvaluacion,
                    Nota = docEvaluacion.NotaEvaluacion,
                    NombreEvaluador = docEvaluacion.NombreEvaluador,
                    Comentarios = docEvaluacion.ComentarioEvaluacion
                };
            }

            // 8 - Póliza Seguro de Vida
            var docPoliza = documentos.FirstOrDefault(d => d.idTipDocumento == 8);
            if (docPoliza != null)
            {
                PolizaVida = new DocumentoPolizaVida
                {
                    FechaPoliza = docPoliza.FechaPolizaVida,
                    NombreBeneficiario = docPoliza.NombreBeneficiarioVida,
                    Parentesco = docPoliza.ParentescoVida,
                    Porcentaje = docPoliza.PorcentajeVida,
                    Direccion = docPoliza.DireccionBeneficiarioVida
                };
            }

            // 9 - Datos Banorte
            var docBanorte = documentos.FirstOrDefault(d => d.idTipDocumento == 9);
            if (docBanorte != null)
            {
                Banorte = new DocumentoBanorte
                {
                    NumCuenta = docBanorte.NumCuentaBanorte,
                    CLABE = docBanorte.ClaveInterbancariaBanorte,
                    NumTarjeta = docBanorte.NumTarjetaBanorte,
                    NombreBeneficiario = docBanorte.NombreBeneficiarioBanorte,
                    Parentesco = docBanorte.ParentescoBanorte,
                    Porcentaje = docBanorte.PorcentajeBanorte,
                    Direccion = docBanorte.DireccionBeneficiarioBanorte
                };
            }

            return Page();
        }

        // ==========================================
        // HANDLER: DAR DE BAJA DESDE DETALLES
        // ==========================================
        public async Task<IActionResult> OnPostDarDeBajaAsync(int id, DateTime? fechaBaja)
        {
            try
            {
                var empleado = await _context.Empleados.FindAsync(id);
                if (empleado == null) return NotFound();

                // Verificar sellos activos
                bool tieneSellos = await _context.TblAsigSellos
                    .AnyAsync(a => a.idOperador == id && (a.Status == 3 || a.Status == 4));

                if (tieneSellos)
                {
                    TempData["Error"] = "No se puede dar de baja: el empleado tiene sellos activos asignados.";
                    return RedirectToPage(new { id });
                }

                empleado.Status = 2;
                empleado.Fegreso = fechaBaja ?? DateTime.Today;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ {empleado.Names} {empleado.Apellido} dado de baja el {empleado.Fegreso:dd/MM/yyyy}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al dar de baja: {ex.Message}";
            }

            return RedirectToPage(new { id });
        }

        // ==========================================
        // HANDLER: REACTIVAR DESDE DETALLES
        // ==========================================
        public async Task<IActionResult> OnPostReactivarAsync(int id)
        {
            try
            {
                var empleado = await _context.Empleados.FindAsync(id);
                if (empleado == null) return NotFound();

                empleado.Status = 1;
                empleado.Fegreso = null;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ {empleado.Names} {empleado.Apellido} reactivado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al reactivar: {ex.Message}";
            }

            return RedirectToPage(new { id });
        }
    }
}