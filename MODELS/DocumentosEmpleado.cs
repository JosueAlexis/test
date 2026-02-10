using System;
using System.ComponentModel.DataAnnotations;

namespace ProyectoRH2025.Models
{
    public class DocumentosEmpleadoViewModel
    {
        // ════════════════════════════════════════════════════════════════
        // LICENCIA
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Número de Licencia")]
        public string NumLicencia { get; set; }

        [Display(Name = "Vigencia de la Licencia")]
        [DataType(DataType.Date)]
        public DateTime? VigenciaLicencia { get; set; }

        [Display(Name = "Antigüedad (años)")]
        public int? AnosAntiguedadLicencia { get; set; }

        [Display(Name = "Categoría / Clasificación")]
        public string CategoriaLicencia { get; set; }

        [Display(Name = "Fecha de Antigüedad")]
        [DataType(DataType.Date)]
        public DateTime? FechaAntiguedadLicencia { get; set; }

        // ════════════════════════════════════════════════════════════════
        // APTO MÉDICO
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Número de Expediente Médico")]
        public string NumExpedienteMedico { get; set; }

        [Display(Name = "Vigente Apto desde")]
        [DataType(DataType.Date)]
        public DateTime? VigenteAptoDesde { get; set; }

        [Display(Name = "Vigente Apto hasta")]
        [DataType(DataType.Date)]
        public DateTime? VigenteAptoHasta { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ANTECEDENTES NO PENALES
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Fecha última Carta de Antecedentes No Penales")]
        [DataType(DataType.Date)]
        public DateTime? FechaAntecedentesNoPenales { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ESTUDIO SOCIOECONÓMICO
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Fecha de realización del Estudio Socioeconómico")]
        [DataType(DataType.Date)]
        public DateTime? FechaEstudioSocioeconomico { get; set; }

        // ════════════════════════════════════════════════════════════════
        // EVALUACIÓN DE MANEJO
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Fecha de Evaluación de Manejo")]
        [DataType(DataType.Date)]
        public DateTime? FechaEvaluacionManejo { get; set; }

        [Display(Name = "Propósito de la Evaluación")]
        public string PropositoEvaluacion { get; set; }

        [Display(Name = "Nota / Calificación")]
        [Range(0, 100, ErrorMessage = "La calificación debe estar entre 0 y 100")]
        public decimal? NotaEvaluacion { get; set; }

        [Display(Name = "Nombre del Evaluador")]
        public string NombreEvaluador { get; set; }

        [Display(Name = "Comentarios / Observaciones")]
        public string ComentarioEvaluacion { get; set; }

        // ════════════════════════════════════════════════════════════════
        // PÓLIZA SEGURO VIDA
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Fecha de Llenado de Póliza")]
        [DataType(DataType.Date)]
        public DateTime? FechaPolizaVida { get; set; }

        [Display(Name = "Nombre del Beneficiario")]
        public string NombreBeneficiarioVida { get; set; }

        [Display(Name = "Parentesco")]
        public string ParentescoVida { get; set; }

        [Display(Name = "Porcentaje (%)")]
        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100")]
        public decimal? PorcentajeVida { get; set; }

        [Display(Name = "Dirección del Beneficiario")]
        public string DireccionBeneficiarioVida { get; set; }

        // ════════════════════════════════════════════════════════════════
        // BANORTE
        // ════════════════════════════════════════════════════════════════
        [Display(Name = "Número de Cuenta Banorte")]
        public string NumCuentaBanorte { get; set; }

        [Display(Name = "Clave Interbancaria (CLABE)")]
        [StringLength(18, MinimumLength = 18, ErrorMessage = "La CLABE debe tener exactamente 18 dígitos")]
        [RegularExpression(@"^\d{18}$", ErrorMessage = "La CLABE debe contener solo números")]
        public string ClaveInterbancariaBanorte { get; set; }

        [Display(Name = "Número de Tarjeta Banorte")]
        [StringLength(16, MinimumLength = 16, ErrorMessage = "El número de tarjeta debe tener 16 dígitos")]
        public string NumTarjetaBanorte { get; set; }

        [Display(Name = "Nombre del Beneficiario")]
        public string NombreBeneficiarioBanorte { get; set; }

        [Display(Name = "Parentesco")]
        public string ParentescoBanorte { get; set; }

        [Display(Name = "Porcentaje (%)")]
        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100")]
        public decimal? PorcentajeBanorte { get; set; }

        [Display(Name = "Dirección del Beneficiario")]
        public string DireccionBeneficiarioBanorte { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ✅ CAMPOS AKNA
        // Solo visibles cuando: codClientes=2 AND TipEmpleado=1 AND Puesto=2
        // ════════════════════════════════════════════════════════════════

        // VISA LASER
        [Display(Name = "Número de Visa Laser")]
        public string VisaLaserNumero { get; set; }

        [Display(Name = "Vigencia Visa Laser")]
        [DataType(DataType.Date)]
        public DateTime? VisaLaserVigencia { get; set; }

        // FAST
        [Display(Name = "Número de Identificación FAST")]
        public string FastNumero { get; set; }

        [Display(Name = "Vigencia FAST")]
        [DataType(DataType.Date)]
        public DateTime? FastVigencia { get; set; }

        // GAFETE ANAM
        [Display(Name = "Vigencia Gafete ANAM")]
        [DataType(DataType.Date)]
        public DateTime? GafeteANAMVigencia { get; set; }

        [Display(Name = "Número de Chip ANAM")]
        public string GafeteANAMChip { get; set; }

        [Display(Name = "Usuario ANAM")]
        public string GafeteANAMUsuario { get; set; }

        [Display(Name = "Correo Registrado ANAM")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
        public string GafeteANAMCorreo { get; set; }
    }
}