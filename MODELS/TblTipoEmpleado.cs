using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblTipoEmpleado")]
    public class TblTipoEmpleado
    {
        [Key]
        public int id { get; set; }

        [Required(ErrorMessage = "El tipo de empleado es obligatorio.")]
        [StringLength(50)]
        public string TipEmpleado { get; set; } = string.Empty;

        // Un tipo de empleado puede tener muchos puestos
        public virtual ICollection<PuestoEmpleado> Puestos { get; set; } = new List<PuestoEmpleado>();
    }
}