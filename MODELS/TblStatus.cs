using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblStatus")]
    public class TblStatus
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Column("Status")]
        public string Status { get; set; } = string.Empty;

        // Relación inversa
        public virtual ICollection<TblUnidades>? Unidades { get; set; }
    }
}