using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblSucursal")]
    public class TblSucursal
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(200)]
        public string Sucursal { get; set; } = string.Empty;

        // Navegación inversa
        public virtual ICollection<TblUnidades>? Unidades { get; set; }
    }
}