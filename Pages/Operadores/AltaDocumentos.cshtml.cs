using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.Operadores
{
    public class AltaDocumentosModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AltaDocumentosModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public int IdEmpleado { get; set; }

        public string NombreEmpleado { get; set; }

        [BindProperty]
        public bool EsEdicion { get; set; }

        [BindProperty]
        public DocumentosEmpleadoViewModel Documentos { get; set; } = new DocumentosEmpleadoViewModel();

        public string Mensaje { get; set; }

        // ✅ Propiedad calculada: indica si mostrar campos AKNA
        // codClientes=2 AND TipEmpleado=1 AND Puesto=2
        public bool MostrarCamposAkna { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var empleado = await _context.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (empleado == null) return NotFound();

            IdEmpleado = id.Value;
            NombreEmpleado = $"{empleado.Names} {empleado.Apellido}".Trim();
            EsEdicion = Request.Query["edit"] == "true";

            // ✅ Calcular si debe mostrar campos AKNA
            MostrarCamposAkna = empleado.CodClientes == "2"
                             && empleado.TipEmpleado == 1
                             && empleado.Puesto == 2;

            // Cargar TODOS los documentos del empleado
            var docs = await _context.tblDocumentosEmpleado
                .Where(d => d.idEmpleado == id.Value)
                .ToListAsync();

            // 1 - Licencia
            var licencia = docs.FirstOrDefault(d => d.idTipDocumento == 1);
            if (licencia != null)
            {
                Documentos.NumLicencia = licencia.NumLicencia;
                Documentos.VigenciaLicencia = licencia.Vigencia;
                Documentos.AnosAntiguedadLicencia = licencia.Anosantiguedad;
                Documentos.CategoriaLicencia = licencia.Clasificacion;
                Documentos.FechaAntiguedadLicencia = licencia.Fechaantiguedad;
            }

            // 2 - Apto Médico
            var apto = docs.FirstOrDefault(d => d.idTipDocumento == 2);
            if (apto != null)
            {
                Documentos.NumExpedienteMedico = apto.NumExpedienteMedico;
                Documentos.VigenteAptoDesde = apto.VigenteAptoDesde;
                Documentos.VigenteAptoHasta = apto.VigenteAptoHasta;
            }

            // 5 - Antecedentes No Penales
            var antecedentes = docs.FirstOrDefault(d => d.idTipDocumento == 5);
            if (antecedentes != null)
            {
                Documentos.FechaAntecedentesNoPenales = antecedentes.FechaAntecedentesNoPenales;
            }

            // 6 - Estudio Socioeconómico
            var estudio = docs.FirstOrDefault(d => d.idTipDocumento == 6);
            if (estudio != null)
            {
                Documentos.FechaEstudioSocioeconomico = estudio.FechaEstudioSocioeconomico;
            }

            // 7 - Evaluación de Manejo
            var evaluacion = docs.FirstOrDefault(d => d.idTipDocumento == 7);
            if (evaluacion != null)
            {
                Documentos.FechaEvaluacionManejo = evaluacion.FechaEvaluacionManejo;
                Documentos.PropositoEvaluacion = evaluacion.PropositoEvaluacion;
                Documentos.NotaEvaluacion = evaluacion.NotaEvaluacion;
                Documentos.NombreEvaluador = evaluacion.NombreEvaluador;
                Documentos.ComentarioEvaluacion = evaluacion.ComentarioEvaluacion;
            }

            // 8 - Póliza Seguro Vida
            var poliza = docs.FirstOrDefault(d => d.idTipDocumento == 8);
            if (poliza != null)
            {
                Documentos.FechaPolizaVida = poliza.FechaPolizaVida;
                Documentos.NombreBeneficiarioVida = poliza.NombreBeneficiarioVida;
                Documentos.ParentescoVida = poliza.ParentescoVida;
                Documentos.PorcentajeVida = poliza.PorcentajeVida;
                Documentos.DireccionBeneficiarioVida = poliza.DireccionBeneficiarioVida;
            }

            // 9 - Datos Banorte
            var banorte = docs.FirstOrDefault(d => d.idTipDocumento == 9);
            if (banorte != null)
            {
                Documentos.NumCuentaBanorte = banorte.NumCuentaBanorte;
                Documentos.ClaveInterbancariaBanorte = banorte.ClaveInterbancariaBanorte;
                Documentos.NumTarjetaBanorte = banorte.NumTarjetaBanorte;
                Documentos.NombreBeneficiarioBanorte = banorte.NombreBeneficiarioBanorte;
                Documentos.ParentescoBanorte = banorte.ParentescoBanorte;
                Documentos.PorcentajeBanorte = banorte.PorcentajeBanorte;
                Documentos.DireccionBeneficiarioBanorte = banorte.DireccionBeneficiarioBanorte;
            }

            // ✅ Cargar campos AKNA desde cualquier registro existente del empleado
            // (se guardan en el registro de Licencia por simplicidad, idTipDocumento=1)
            if (MostrarCamposAkna)
            {
                var docAkna = docs.FirstOrDefault(d => d.idTipDocumento == 1) ?? docs.FirstOrDefault();
                if (docAkna != null)
                {
                    Documentos.VisaLaserNumero = docAkna.VisaLaserNumero;
                    Documentos.VisaLaserVigencia = docAkna.VisaLaserVigencia;
                    Documentos.FastNumero = docAkna.FastNumero;
                    Documentos.FastVigencia = docAkna.FastVigencia;
                    Documentos.GafeteANAMVigencia = docAkna.GafeteANAMVigencia;
                    Documentos.GafeteANAMChip = docAkna.GafeteANAMChip;
                    Documentos.GafeteANAMUsuario = docAkna.GafeteANAMUsuario;
                    Documentos.GafeteANAMCorreo = docAkna.GafeteANAMCorreo;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var empleado = await _context.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == IdEmpleado);

            if (empleado == null) return NotFound();

            // ✅ Recalcular en POST para que MostrarCamposAkna esté disponible en caso de error
            MostrarCamposAkna = empleado.CodClientes == "2"
                             && empleado.TipEmpleado == 1
                             && empleado.Puesto == 2;

            NombreEmpleado = $"{empleado.Names} {empleado.Apellido}".Trim();

            try
            {
                // 1 - LICENCIA
                await GuardarOActualizarDocumento(
                    idTipoDoc: 1,
                    actualizarCampos: (doc) =>
                    {
                        doc.NumLicencia = string.IsNullOrWhiteSpace(Documentos.NumLicencia)
                            ? null : Documentos.NumLicencia.Trim();
                        doc.Vigencia = Documentos.VigenciaLicencia;
                        doc.Anosantiguedad = Documentos.AnosAntiguedadLicencia;
                        doc.Clasificacion = string.IsNullOrWhiteSpace(Documentos.CategoriaLicencia)
                            ? null : Documentos.CategoriaLicencia.Trim();
                        doc.Fechaantiguedad = Documentos.FechaAntiguedadLicencia;

                        // ✅ Guardar campos AKNA en el mismo registro de Licencia
                        if (MostrarCamposAkna)
                        {
                            doc.VisaLaserNumero = string.IsNullOrWhiteSpace(Documentos.VisaLaserNumero)
                                ? null : Documentos.VisaLaserNumero.Trim();
                            doc.VisaLaserVigencia = Documentos.VisaLaserVigencia;
                            doc.FastNumero = string.IsNullOrWhiteSpace(Documentos.FastNumero)
                                ? null : Documentos.FastNumero.Trim();
                            doc.FastVigencia = Documentos.FastVigencia;
                            doc.GafeteANAMVigencia = Documentos.GafeteANAMVigencia;
                            doc.GafeteANAMChip = string.IsNullOrWhiteSpace(Documentos.GafeteANAMChip)
                                ? null : Documentos.GafeteANAMChip.Trim();
                            doc.GafeteANAMUsuario = string.IsNullOrWhiteSpace(Documentos.GafeteANAMUsuario)
                                ? null : Documentos.GafeteANAMUsuario.Trim();
                            doc.GafeteANAMCorreo = string.IsNullOrWhiteSpace(Documentos.GafeteANAMCorreo)
                                ? null : Documentos.GafeteANAMCorreo.Trim();
                        }
                    }
                );

                // 2 - APTO MÉDICO
                await GuardarOActualizarDocumento(
                    idTipoDoc: 2,
                    actualizarCampos: (doc) =>
                    {
                        doc.NumExpedienteMedico = string.IsNullOrWhiteSpace(Documentos.NumExpedienteMedico)
                            ? null : Documentos.NumExpedienteMedico.Trim();
                        doc.VigenteAptoDesde = Documentos.VigenteAptoDesde;
                        doc.VigenteAptoHasta = Documentos.VigenteAptoHasta;
                    }
                );

                // 5 - ANTECEDENTES NO PENALES
                await GuardarOActualizarDocumento(
                    idTipoDoc: 5,
                    actualizarCampos: (doc) =>
                    {
                        doc.FechaAntecedentesNoPenales = Documentos.FechaAntecedentesNoPenales;
                    }
                );

                // 6 - ESTUDIO SOCIOECONÓMICO
                await GuardarOActualizarDocumento(
                    idTipoDoc: 6,
                    actualizarCampos: (doc) =>
                    {
                        doc.FechaEstudioSocioeconomico = Documentos.FechaEstudioSocioeconomico;
                    }
                );

                // 7 - EVALUACIÓN DE MANEJO
                await GuardarOActualizarDocumento(
                    idTipoDoc: 7,
                    actualizarCampos: (doc) =>
                    {
                        doc.FechaEvaluacionManejo = Documentos.FechaEvaluacionManejo;
                        doc.PropositoEvaluacion = string.IsNullOrWhiteSpace(Documentos.PropositoEvaluacion)
                            ? null : Documentos.PropositoEvaluacion.Trim();
                        doc.NotaEvaluacion = Documentos.NotaEvaluacion;
                        doc.NombreEvaluador = string.IsNullOrWhiteSpace(Documentos.NombreEvaluador)
                            ? null : Documentos.NombreEvaluador.Trim();
                        doc.ComentarioEvaluacion = string.IsNullOrWhiteSpace(Documentos.ComentarioEvaluacion)
                            ? null : Documentos.ComentarioEvaluacion.Trim();
                    }
                );

                // 8 - PÓLIZA SEGURO VIDA
                await GuardarOActualizarDocumento(
                    idTipoDoc: 8,
                    actualizarCampos: (doc) =>
                    {
                        doc.FechaPolizaVida = Documentos.FechaPolizaVida;
                        doc.NombreBeneficiarioVida = string.IsNullOrWhiteSpace(Documentos.NombreBeneficiarioVida)
                            ? null : Documentos.NombreBeneficiarioVida.Trim();
                        doc.ParentescoVida = string.IsNullOrWhiteSpace(Documentos.ParentescoVida)
                            ? null : Documentos.ParentescoVida.Trim();
                        doc.PorcentajeVida = Documentos.PorcentajeVida;
                        doc.DireccionBeneficiarioVida = string.IsNullOrWhiteSpace(Documentos.DireccionBeneficiarioVida)
                            ? null : Documentos.DireccionBeneficiarioVida.Trim();
                    }
                );

                // 9 - DATOS BANORTE
                await GuardarOActualizarDocumento(
                    idTipoDoc: 9,
                    actualizarCampos: (doc) =>
                    {
                        doc.NumCuentaBanorte = string.IsNullOrWhiteSpace(Documentos.NumCuentaBanorte)
                            ? null : Documentos.NumCuentaBanorte.Trim();
                        doc.ClaveInterbancariaBanorte = string.IsNullOrWhiteSpace(Documentos.ClaveInterbancariaBanorte)
                            ? null : Documentos.ClaveInterbancariaBanorte.Trim();
                        doc.NumTarjetaBanorte = string.IsNullOrWhiteSpace(Documentos.NumTarjetaBanorte)
                            ? null : Documentos.NumTarjetaBanorte.Trim();
                        doc.NombreBeneficiarioBanorte = string.IsNullOrWhiteSpace(Documentos.NombreBeneficiarioBanorte)
                            ? null : Documentos.NombreBeneficiarioBanorte.Trim();
                        doc.ParentescoBanorte = string.IsNullOrWhiteSpace(Documentos.ParentescoBanorte)
                            ? null : Documentos.ParentescoBanorte.Trim();
                        doc.PorcentajeBanorte = Documentos.PorcentajeBanorte;
                        doc.DireccionBeneficiarioBanorte = string.IsNullOrWhiteSpace(Documentos.DireccionBeneficiarioBanorte)
                            ? null : Documentos.DireccionBeneficiarioBanorte.Trim();
                    }
                );

                await _context.SaveChangesAsync();

                TempData["Mensaje"] = EsEdicion
                    ? $"Información de documentos de {empleado.Names} {empleado.Apellido} actualizada correctamente."
                    : $"Registro de {empleado.Names} {empleado.Apellido} completado exitosamente.";

                return RedirectToPage("/Operadores/Detalles", new { id = IdEmpleado });
            }
            catch (Exception ex)
            {
                Mensaje = $"Error al guardar: {ex.Message}";
                if (ex.InnerException != null)
                    Mensaje += $" | {ex.InnerException.Message}";

                return Page();
            }
        }

        private async Task GuardarOActualizarDocumento(int idTipoDoc, Action<tblDocumentosEmpleado> actualizarCampos)
        {
            var doc = await _context.tblDocumentosEmpleado
                .FirstOrDefaultAsync(d => d.idEmpleado == IdEmpleado && d.idTipDocumento == idTipoDoc);

            if (doc == null)
            {
                doc = new tblDocumentosEmpleado
                {
                    idEmpleado = IdEmpleado,
                    idTipDocumento = idTipoDoc,
                    FechaAlta = DateTime.Now
                };
                _context.tblDocumentosEmpleado.Add(doc);
            }

            actualizarCampos(doc);
        }
    }

    
}