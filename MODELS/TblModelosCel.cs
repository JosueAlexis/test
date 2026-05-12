using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblModelosCel")]
    public class TblModelosCel
    {
        [Key]
        public int id { get; set; }
        public string Modelo { get; set; } = string.Empty;
        public int Marca { get; set; }
    }
}