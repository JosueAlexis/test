using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblUnidades")]
    public class TblUnidades
    {
        [Key]
        public int id { get; set; }

        [Required]
        [Display(Name = "Número de Unidad")]
        public int NumUnidad { get; set; }

        [StringLength(15)]
        [Display(Name = "Placas")]
        public string? Placas { get; set; }

        [Display(Name = "Pool")]
        public int? Pool { get; set; }

        [Required]
        [Display(Name = "Código Cliente")]
        public int CodCliente { get; set; }

        [Required]
        [Display(Name = "Año")]
        public int AnoUnidad { get; set; }

        [Display(Name = "Sucursal")]
        public int? idSucursal { get; set; }

        // Relación con Cuentas
        [Required(ErrorMessage = "Debe seleccionar una cuenta")]
        [Column("IdCuenta")]
        [Display(Name = "Cuenta")]
        public int IdCuenta { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdCuenta")]
        public virtual TblCuentas? Cuenta { get; set; }

        [ForeignKey("Pool")]
        public virtual TblPool? PoolNavigation { get; set; }

        [ForeignKey("CodCliente")]
        public virtual TblClientes? Cliente { get; set; }

        [ForeignKey("idSucursal")]
        public virtual TblSucursal? Sucursal { get; set; }
    }
}