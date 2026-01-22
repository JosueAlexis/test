using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblClientes")]
    public class TblClientes
    {
        [Key]
        public int codCliente { get; set; }

        [Required]
        [StringLength(200)]
        public string Cliente { get; set; } = string.Empty;

        // Navegación inversa
        public virtual ICollection<TblUnidades>? Unidades { get; set; }
    }
}