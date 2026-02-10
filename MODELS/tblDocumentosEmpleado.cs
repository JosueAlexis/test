using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblDocumentosEmpleado")]
    public class tblDocumentosEmpleado
    {
        [Key]
        public int id { get; set; }

        public int idEmpleado { get; set; }

        public int? idTipDocumento { get; set; }

        // ════════════════════════════════════════════════════════════════
        // CAMPOS DE LICENCIA (idTipDocumento = 1)
        // ════════════════════════════════════════════════════════════════
        public string? NumLicencia { get; set; }
        public DateTime? Vigencia { get; set; }
        public int? Anosantiguedad { get; set; }
        public string? Clasificacion { get; set; }
        public DateTime? Fechaantiguedad { get; set; }

        // ════════════════════════════════════════════════════════════════
        // CAMPOS DE APTO MÉDICO (idTipDocumento = 2)
        // ════════════════════════════════════════════════════════════════
        public string? NumExpedienteMedico { get; set; }
        public DateTime? VigenteAptoDesde { get; set; }
        public DateTime? VigenteAptoHasta { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ANTECEDENTES NO PENALES (idTipDocumento = 5)
        // ════════════════════════════════════════════════════════════════
        public DateTime? FechaAntecedentesNoPenales { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ESTUDIO SOCIOECONÓMICO (idTipDocumento = 6)
        // ════════════════════════════════════════════════════════════════
        public DateTime? FechaEstudioSocioeconomico { get; set; }

        // ════════════════════════════════════════════════════════════════
        // EVALUACIÓN DE MANEJO (idTipDocumento = 7)
        // ════════════════════════════════════════════════════════════════
        public DateTime? FechaEvaluacionManejo { get; set; }
        public string? PropositoEvaluacion { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? NotaEvaluacion { get; set; }

        public string? NombreEvaluador { get; set; }
        public string? ComentarioEvaluacion { get; set; }

        // ════════════════════════════════════════════════════════════════
        // PÓLIZA SEGURO VIDA (idTipDocumento = 8)
        // ════════════════════════════════════════════════════════════════
        public DateTime? FechaPolizaVida { get; set; }
        public string? NombreBeneficiarioVida { get; set; }
        public string? ParentescoVida { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? PorcentajeVida { get; set; }

        public string? DireccionBeneficiarioVida { get; set; }

        // ════════════════════════════════════════════════════════════════
        // DATOS BANORTE (idTipDocumento = 9)
        // ════════════════════════════════════════════════════════════════
        public string? NumCuentaBanorte { get; set; }
        public string? ClaveInterbancariaBanorte { get; set; }
        public string? NumTarjetaBanorte { get; set; }
        public string? NombreBeneficiarioBanorte { get; set; }
        public string? ParentescoBanorte { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? PorcentajeBanorte { get; set; }

        public string? DireccionBeneficiarioBanorte { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ✅ NUEVOS CAMPOS AKNA
        // Solo para empleados: codClientes=2 AND TipEmpleado=1 AND Puesto=2
        // ════════════════════════════════════════════════════════════════

        // ── VISA LASER ────────────────────────────────────────
        /// <summary>
        /// Vigencia de la Visa Laser (Solo AKNA)
        /// </summary>
        public DateTime? VisaLaserVigencia { get; set; }

        /// <summary>
        /// Número de Visa Laser (Solo AKNA)
        /// </summary>
        public string? VisaLaserNumero { get; set; }

        // ── FAST ──────────────────────────────────────────────
        /// <summary>
        /// Vigencia del FAST (Solo AKNA)
        /// </summary>
        public DateTime? FastVigencia { get; set; }

        /// <summary>
        /// Número de identificación FAST (Solo AKNA)
        /// </summary>
        public string? FastNumero { get; set; }

        // ── GAFETE ANAM ───────────────────────────────────────
        /// <summary>
        /// Vigencia del Gafete ANAM (Solo AKNA)
        /// </summary>
        public DateTime? GafeteANAMVigencia { get; set; }

        /// <summary>
        /// Número de Chip del Gafete ANAM (Solo AKNA)
        /// </summary>
        public string? GafeteANAMChip { get; set; }

        /// <summary>
        /// Usuario ANAM (Solo AKNA)
        /// </summary>
        public string? GafeteANAMUsuario { get; set; }

        /// <summary>
        /// Correo registrado en ANAM (Solo AKNA)
        /// </summary>
        public string? GafeteANAMCorreo { get; set; }

        // ════════════════════════════════════════════════════════════════
        // CAMPOS DE AUDITORÍA
        // ════════════════════════════════════════════════════════════════
        public int? Status { get; set; }
        public DateTime? FechaAlta { get; set; }
        public int? idUsuarioAlta { get; set; }
        public int? Editor { get; set; }

        // ════════════════════════════════════════════════════════════════
        // RELACIÓN DE NAVEGACIÓN
        // ════════════════════════════════════════════════════════════════
        [ForeignKey("idEmpleado")]
        public virtual Empleado? Empleado { get; set; }
    }
}