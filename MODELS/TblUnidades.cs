using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblUnidades")]
    public class TblUnidades
    {
        [Key]
        public int id { get; set; }

        public int NumUnidad { get; set; }
        public string? Placas { get; set; }
        public int? Pool { get; set; }
        public int CodCliente { get; set; }
        public int AnoUnidad { get; set; }
        public int? idSucursal { get; set; }
    }
}
