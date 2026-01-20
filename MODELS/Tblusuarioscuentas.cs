using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    /// <summary>
    /// Tabla de relación muchos a muchos entre Usuarios y Cuentas
    /// Define qué cuentas puede ver/administrar cada supervisor/coordinador
    /// </summary>
    [Table("tblUsuariosCuentas")]
    public class TblUsuariosCuentas
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del usuario (supervisor/coordinador)
        /// </summary>
        [Required]
        public int IdUsuario { get; set; }

        /// <summary>
        /// ID de la cuenta asignada
        /// </summary>
        [Required]
        public int IdCuenta { get; set; }

        /// <summary>
        /// Fecha en que se asignó la cuenta al usuario
        /// </summary>
        public DateTime FechaAsignacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Usuario de IT que realizó la asignación
        /// </summary>
        public int? AsignadoPor { get; set; }

        /// <summary>
        /// Indica si la asignación está activa
        /// </summary>
        public bool EsActivo { get; set; } = true;

        /// <summary>
        /// Fecha de desactivación (si aplica)
        /// </summary>
        public DateTime? FechaDesactivacion { get; set; }

        /// <summary>
        /// Usuario que desactivó la asignación
        /// </summary>
        public int? DesactivadoPor { get; set; }

        // Navegación
        [ForeignKey("IdUsuario")]
        public virtual Usuario? Usuario { get; set; }

        [ForeignKey("IdCuenta")]
        public virtual TblCuentas? Cuenta { get; set; }
    }
}