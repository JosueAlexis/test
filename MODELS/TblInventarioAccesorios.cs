using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblInventarioAccesorios")]
    public class TblInventarioAccesorios
    {
        [Key] public int id { get; set; }
        public int idInventario { get; set; }
        public bool FundaUsoRudo { get; set; }
        public bool CargadorPared { get; set; }
        public bool CargadorCenicero { get; set; }
        public bool VidrioTemplado { get; set; }
        public bool SoporteCamion { get; set; }
    }
}