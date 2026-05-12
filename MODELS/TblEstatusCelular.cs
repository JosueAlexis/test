using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblEstatusCelular")]
    public class TblEstatusCelular
    {
        [Key]
        public int idEstatus { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }
}