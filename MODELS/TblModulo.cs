using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblModulo")]
    public class TblModulo
    {
        [Key]
        public int idModulo { get; set; }

        public string ModuloNombre { get; set; } = string.Empty;
    }
}