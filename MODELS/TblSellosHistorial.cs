using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("TblSellosHistorial")]
    public class TblSellosHistorial
    {
        [Key]
        public int Id { get; set; }

        // Información del Sello
        [Required]
        public int SelloId { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroSello { get; set; }

        // Tipo de Movimiento
        [Required]
        [StringLength(50)]
        public string TipoMovimiento { get; set; }

        // Estados
        [Required]
        public int StatusAnterior { get; set; }

        [Required]
        public int StatusNuevo { get; set; }

        // Supervisor
        public int? SupervisorIdAnterior { get; set; }
        public int? SupervisorIdNuevo { get; set; }

        [StringLength(200)]
        public string? SupervisorNombreAnterior { get; set; }

        [StringLength(200)]
        public string? SupervisorNombreNuevo { get; set; }

        // Fechas
        [Required]
        public DateTime FechaMovimiento { get; set; } = DateTime.Now;

        public DateTime? FechaAsignacionAnterior { get; set; }
        public DateTime? FechaAsignacionNueva { get; set; }

        // Auditoría del Usuario
        public int? UsuarioId { get; set; }

        [StringLength(200)]
        public string? UsuarioNombre { get; set; }

        // Detalles
        [StringLength(500)]
        public string? Comentario { get; set; }

        [StringLength(50)]
        public string? IP { get; set; }

        // Navegación
        [ForeignKey("SelloId")]
        public virtual TblSellos? Sello { get; set; }
    }
}