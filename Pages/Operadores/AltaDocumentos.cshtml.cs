using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.Collections.Generic;
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

        // 🆕 Beneficiarios
        [BindProperty]
        public List<BeneficiarioTemp> BeneficiariosPoliza { get; set; } = new List<BeneficiarioTemp>();

        [BindProperty]
        public List<BeneficiarioTemp> BeneficiariosBanorte { get; set; } = new List<BeneficiarioTemp>();

        public string Mensaje { get; set; }

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

            MostrarCamposAkna = empleado.CodClientes == "2"
                             && empleado.TipEmpleado == 1
                             && empleado.Puesto == 2;

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

                // 🆕 Cargar beneficiarios de póliza
                BeneficiariosPoliza = await _context.Beneficiarios
                    .Where(b => b.IdDocumento == poliza.id && b.TipoBeneficiario == "PolizaVida" && b.Status)
                    .OrderBy(b => b.Orden)
                    .Select(b => new BeneficiarioTemp
                    {
                        Id = b.Id,
                        Nombre = b.Nombre,
                        Parentesco = b.Parentesco,
                        Porcentaje = b.Porcentaje,
                        Direccion = b.Direccion
                    })
                    .ToListAsync();
            }

            // Asegurar mínimo 3 filas para captura
            while (BeneficiariosPoliza.Count < 3)
            {
                BeneficiariosPoliza.Add(new BeneficiarioTemp());
            }

            // 9 - Datos Banorte
            var banorte = docs.FirstOrDefault(d => d.idTipDocumento == 9);
            if (banorte != null)
            {
                Documentos.NumCuentaBanorte = banorte.NumCuentaBanorte;
                Documentos.ClaveInterbancariaBanorte = banorte.ClaveInterbancariaBanorte;
                Documentos.NumTarjetaBanorte = banorte.NumTarjetaBanorte;

                // 🆕 Cargar beneficiarios de Banorte
                BeneficiariosBanorte = await _context.Beneficiarios
                    .Where(b => b.IdDocumento == banorte.id && b.TipoBeneficiario == "Banorte" && b.Status)
                    .OrderBy(b => b.Orden)
                    .Select(b => new BeneficiarioTemp
                    {
                        Id = b.Id,
                        Nombre = b.Nombre,
                        Parentesco = b.Parentesco,
                        Porcentaje = b.Porcentaje,
                        Direccion = b.Direccion
                    })
                    .ToListAsync();
            }

            // Asegurar mínimo 3 filas para captura
            while (BeneficiariosBanorte.Count < 3)
            {
                BeneficiariosBanorte.Add(new BeneficiarioTemp());
            }

            // Campos AKNA
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

            MostrarCamposAkna = empleado.CodClientes == "2"
                             && empleado.TipEmpleado == 1
                             && empleado.Puesto == 2;

            NombreEmpleado = $"{empleado.Names} {empleado.Apellido}".Trim();

            // 🆕 Validar beneficiarios (Mínimo 1, Máximo 3)
            var beneficiariosPolizaValidos = BeneficiariosPoliza?.Where(b => !string.IsNullOrWhiteSpace(b.Nombre)).ToList() ?? new List<BeneficiarioTemp>();
            var beneficiariosBanorteValidos = BeneficiariosBanorte?.Where(b => !string.IsNullOrWhiteSpace(b.Nombre)).ToList() ?? new List<BeneficiarioTemp>();

            if (beneficiariosPolizaValidos.Count < 1 || beneficiariosPolizaValidos.Count > 3)
            {
                Mensaje = "❌ Debe ingresar entre 1 y 3 beneficiarios para la Póliza de Seguro de Vida.";
                return Page();
            }

            if (beneficiariosBanorteValidos.Count < 1 || beneficiariosBanorteValidos.Count > 3)
            {
                Mensaje = "❌ Debe ingresar entre 1 y 3 beneficiarios para Datos Banorte.";
                return Page();
            }

            // Validar que los porcentajes sumen 100%
            var totalPoliza = beneficiariosPolizaValidos.Sum(b => b.Porcentaje ?? 0);
            if (Math.Abs(totalPoliza - 100) > 0.01m)
            {
                Mensaje = $"❌ Los porcentajes de beneficiarios de Póliza deben sumar 100%. Suma actual: {totalPoliza}%";
                return Page();
            }

            var totalBanorte = beneficiariosBanorteValidos.Sum(b => b.Porcentaje ?? 0);
            if (Math.Abs(totalBanorte - 100) > 0.01m)
            {
                Mensaje = $"❌ Los porcentajes de beneficiarios de Banorte deben sumar 100%. Suma actual: {totalBanorte}%";
                return Page();
            }

            try
            {
                // 1 - LICENCIA
                var docLicenciaId = await GuardarOActualizarDocumento(
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
                var docPolizaId = await GuardarOActualizarDocumento(
                    idTipoDoc: 8,
                    actualizarCampos: (doc) =>
                    {
                        doc.FechaPolizaVida = Documentos.FechaPolizaVida;
                    }
                );

                // 🆕 Guardar beneficiarios de Póliza
                await GuardarBeneficiarios(docPolizaId, "PolizaVida", beneficiariosPolizaValidos);

                // 9 - DATOS BANORTE
                var docBanorteId = await GuardarOActualizarDocumento(
                    idTipoDoc: 9,
                    actualizarCampos: (doc) =>
                    {
                        doc.NumCuentaBanorte = string.IsNullOrWhiteSpace(Documentos.NumCuentaBanorte)
                            ? null : Documentos.NumCuentaBanorte.Trim();
                        doc.ClaveInterbancariaBanorte = string.IsNullOrWhiteSpace(Documentos.ClaveInterbancariaBanorte)
                            ? null : Documentos.ClaveInterbancariaBanorte.Trim();
                        doc.NumTarjetaBanorte = string.IsNullOrWhiteSpace(Documentos.NumTarjetaBanorte)
                            ? null : Documentos.NumTarjetaBanorte.Trim();
                    }
                );

                // 🆕 Guardar beneficiarios de Banorte
                await GuardarBeneficiarios(docBanorteId, "Banorte", beneficiariosBanorteValidos);

                await _context.SaveChangesAsync();

                TempData["Mensaje"] = EsEdicion
                    ? $"✅ Información de documentos de {empleado.Names} {empleado.Apellido} actualizada correctamente."
                    : $"✅ Registro de {empleado.Names} {empleado.Apellido} completado exitosamente.";

                return RedirectToPage("/Operadores/Detalles", new { id = IdEmpleado });
            }
            catch (Exception ex)
            {
                Mensaje = $"❌ Error al guardar: {ex.Message}";
                if (ex.InnerException != null)
                    Mensaje += $" | {ex.InnerException.Message}";

                return Page();
            }
        }

        private async Task<int> GuardarOActualizarDocumento(int idTipoDoc, Action<tblDocumentosEmpleado> actualizarCampos)
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
                await _context.SaveChangesAsync(); // Necesario para obtener el ID
            }

            actualizarCampos(doc);
            return doc.id;
        }

        // 🆕 Método para guardar beneficiarios
        private async Task GuardarBeneficiarios(int idDocumento, string tipoBeneficiario, List<BeneficiarioTemp> beneficiarios)
        {
            // Desactivar beneficiarios existentes
            var beneficiariosExistentes = await _context.Beneficiarios
                .Where(b => b.IdDocumento == idDocumento && b.TipoBeneficiario == tipoBeneficiario)
                .ToListAsync();

            foreach (var benef in beneficiariosExistentes)
            {
                benef.Status = false;
            }

            // Guardar nuevos beneficiarios
            byte orden = 1;
            foreach (var benefTemp in beneficiarios)
            {
                if (benefTemp.Id > 0)
                {
                    // Actualizar existente
                    var benef = beneficiariosExistentes.FirstOrDefault(b => b.Id == benefTemp.Id);
                    if (benef != null)
                    {
                        benef.Nombre = benefTemp.Nombre?.Trim();
                        benef.Parentesco = benefTemp.Parentesco?.Trim();
                        benef.Porcentaje = benefTemp.Porcentaje;
                        benef.Direccion = benefTemp.Direccion?.Trim();
                        benef.Orden = orden;
                        benef.Status = true;
                    }
                }
                else
                {
                    // Crear nuevo
                    _context.Beneficiarios.Add(new Beneficiario
                    {
                        IdDocumento = idDocumento,
                        TipoBeneficiario = tipoBeneficiario,
                        Nombre = benefTemp.Nombre?.Trim(),
                        Parentesco = benefTemp.Parentesco?.Trim(),
                        Porcentaje = benefTemp.Porcentaje,
                        Direccion = benefTemp.Direccion?.Trim(),
                        Orden = orden,
                        Status = true,
                        FechaAlta = DateTime.Now
                    });
                }
                orden++;
            }
        }
    }

    // 🆕 Clase temporal para binding de beneficiarios
    public class BeneficiarioTemp
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Parentesco { get; set; }
        public decimal? Porcentaje { get; set; }
        public string Direccion { get; set; }
    }
}