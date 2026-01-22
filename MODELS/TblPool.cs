using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblPool")]
    public class TblPool
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(100)]
        public string Pool { get; set; } = string.Empty;

        // Navegación inversa
        public virtual ICollection<TblUnidades>? Unidades { get; set; }
    }
}