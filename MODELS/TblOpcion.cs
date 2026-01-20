using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblOpcion")]
    public class TblOpcion
    {
        [Key]
        public int idOpcion { get; set; }

        public string OpcNombre { get; set; } = string.Empty;

        public int ModID { get; set; }
    }
}