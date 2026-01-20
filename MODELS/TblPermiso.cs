using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ProyectoRH2025.Models
{
    [Table("tblPermiso")]
    public class TblPermiso
    {
        [Key]
        public int idPermiso { get; set; }

        public int idRolUsua { get; set; }

        public int idOpcion { get; set; }

        public bool Permiso { get; set; }

        public int? idSeleccion { get; set; }

        public int? idModulo { get; set; }
    }
}