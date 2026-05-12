using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblMarcasCel")]
    public class TblMarcasCel
    {
        [Key]
        public int id { get; set; }
        public string MarcaCel { get; set; } = string.Empty;
    }
}