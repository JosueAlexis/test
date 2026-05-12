using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblPreciosCel")]
    public class TblPreciosCel
    {
        [Key]
        public int id { get; set; }
        public string Precio { get; set; } = string.Empty;

        public int idModelo { get; set; }
        public int idMarca { get; set; }
    }
}