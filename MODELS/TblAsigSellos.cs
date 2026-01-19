using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProyectoRH2025.MODELS;

namespace ProyectoRH2025.Models
{
    [Table("tblAsigSellos")]
    public class TblAsigSellos
    {
        [Key]
        public int id { get; set; }

        public int idSello { get; set; }
        public int idUsuario { get; set; }
        public DateTime Fentrega { get; set; }
        public int idOperador { get; set; }
        public int? idOperador2 { get; set; }
        public int idUnidad { get; set; }
        public string? Ruta { get; set; }
        public string? Caja { get; set; }
        public string? Comentarios { get; set; }
        public int Status { get; set; }
        public int? editor { get; set; }
        public int? idSeAsigno { get; set; }
        public DateTime? FechaStatus4 { get; set; }
        public int TipoAsignacion { get; set; }

        // ✅ CAMPOS PARA QR
        public string? QR_Code { get; set; }
        public DateTime? QR_FechaGeneracion { get; set; }

        [Required] // ✅ IMPORTANTE: Marcar como requerido
        public bool QR_Entregado { get; set; } // ✅ SIN el "?" - NO es nullable

        public DateTime? FechaEntrega { get; set; }
        public DateTime? FechaDevolucion { get; set; }
        public string? StatusEvidencia { get; set; }

        // Navegación
        [ForeignKey("idSello")]
        public virtual TblSellos? Sello { get; set; }

        [ForeignKey("idUsuario")]
        public virtual Usuario? Usuario { get; set; }

        [ForeignKey("idOperador")]
        public virtual Empleado? Operador { get; set; }

        [ForeignKey("idOperador2")]
        public virtual Empleado? Operador2 { get; set; }

        [ForeignKey("idUnidad")]
        public virtual TblUnidades? Unidad { get; set; }

        [ForeignKey("idSeAsigno")]
        public virtual Usuario? Coordinador { get; set; }
    }
}