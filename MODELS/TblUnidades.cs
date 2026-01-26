using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblUnidades")]
    public class TblUnidades
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("NumUnidad")]
        public int NumUnidad { get; set; } // ✅ INT

        [Required]
        [StringLength(50)]
        [Column("Placas")]
        public string Placas { get; set; } = string.Empty; // ✅ VARCHAR(50) NOT NULL

        [Column("Pool")]
        public int Pool { get; set; }

        [Column("CodCliente")]
        public int CodCliente { get; set; }

        [Column("AnoUnidad")]
        public int AnoUnidad { get; set; }

        [Column("idSucursal")]
        public int? IdSucursal { get; set; } // ✅ NULLABLE

        [Column("IdCuenta")]
        public int IdCuenta { get; set; }

        // ✅ Campos de Comodín
        [Column("EsComodin")]
        public bool EsComodin { get; set; } = false;

        [Column("FechaActivacionComodin")]
        public DateTime? FechaActivacionComodin { get; set; }

        [Column("FechaExpiracionComodin")]
        public DateTime? FechaExpiracionComodin { get; set; }

        // ✅ Status
        [Column("idStatus")]
        public int IdStatus { get; set; } = 1;

        // ===== RELACIONES =====

        [ForeignKey("IdStatus")]
        public virtual TblStatus? Status { get; set; }

        [ForeignKey("IdCuenta")]
        public virtual TblCuentas? Cuenta { get; set; }

        [ForeignKey("Pool")]
        public virtual TblPool? PoolNavigation { get; set; }

        [ForeignKey("CodCliente")]
        public virtual TblClientes? Cliente { get; set; }

        [ForeignKey("IdSucursal")]
        public virtual TblSucursal? Sucursal { get; set; }

        // ===== PROPIEDADES CALCULADAS =====

        [NotMapped]
        public bool EstaActiva => IdStatus == 1;

        [NotMapped]
        public bool EstaVencida => EsComodin &&
                                   FechaExpiracionComodin.HasValue &&
                                   FechaExpiracionComodin.Value < DateTime.Now;

        [NotMapped]
        public int? DiasRestantes
        {
            get
            {
                if (!EsComodin || !FechaExpiracionComodin.HasValue)
                    return null;

                var dias = (FechaExpiracionComodin.Value - DateTime.Now).Days;
                return dias > 0 ? dias : 0;
            }
        }

        [NotMapped]
        public string EstadoComodin
        {
            get
            {
                if (!EsComodin) return "Normal";
                if (!DiasRestantes.HasValue) return "Sin fecha";
                if (DiasRestantes.Value == 0) return "Vencida";
                if (DiasRestantes.Value == 1) return "Crítica";
                if (DiasRestantes.Value <= 2) return "Alerta";
                return "Activa";
            }
        }
    }
}