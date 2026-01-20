using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    /// <summary>
    /// Catálogo de cuentas/clientes del sistema
    /// Ejemplos: TOYOTA TMMTX, TOYOTA MTMUS, DALTILE, SCD, etc.
    /// </summary>
    [Table("tblCuentas")]
    public class TblCuentas
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Código único de la cuenta (Ej: TOYOTA1, DALTILE, SCD)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string CodigoCuenta { get; set; }

        /// <summary>
        /// Nombre descriptivo de la cuenta
        /// </summary>
        [Required]
        [StringLength(200)]
        public string NombreCuenta { get; set; }

        /// <summary>
        /// Descripción adicional o notas
        /// </summary>
        [StringLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Color en hexadecimal para UI (Opcional)
        /// Ej: #FF5733 para identificar visualmente
        /// </summary>
        [StringLength(7)]
        public string? ColorHex { get; set; }

        /// <summary>
        /// Indica si la cuenta está activa
        /// </summary>
        public bool EsActiva { get; set; } = true;

        /// <summary>
        /// Fecha de creación de la cuenta
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Usuario que creó la cuenta
        /// </summary>
        public int? CreadoPor { get; set; }

        /// <summary>
        /// Orden para mostrar en listas
        /// </summary>
        public int OrdenVisualizacion { get; set; } = 0;

        // Navegación
        public virtual ICollection<TblUsuariosCuentas> UsuariosAsignados { get; set; } = new List<TblUsuariosCuentas>();
    }
}