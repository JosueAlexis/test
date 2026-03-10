using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblBeneficiarios")]
    public class Beneficiario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdDocumento { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoBeneficiario { get; set; } // "PolizaVida" o "Banorte"

        [Required]
        [StringLength(255)]
        public string Nombre { get; set; }

        [StringLength(100)]
        public string? Parentesco { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Porcentaje { get; set; }

        [StringLength(500)]
        public string? Direccion { get; set; }

        public byte Orden { get; set; } = 1;

        public bool Status { get; set; } = true;

        public DateTime FechaAlta { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("IdDocumento")]
        public virtual tblDocumentosEmpleado? Documento { get; set; }
    }
}