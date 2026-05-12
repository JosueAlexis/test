using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblInventarioCel")]
    public class TblInventarioCel
    {
        [Key]
        public int id { get; set; }

        public int idempleado { get; set; }
        public int? idSucursal { get; set; }
        public string NoTelefono { get; set; } = string.Empty;
        public DateTime Fentrega { get; set; }
        public DateTime? Frenovacion { get; set; }
        public int idMarca { get; set; }
        public int idModelo { get; set; }
        public string IMEI { get; set; } = string.Empty;
        public int idPuesto { get; set; }
        public int idUsuario { get; set; }
        public int? idHuella { get; set; }
        public int? idPrecio { get; set; }
        public string? Comentarios { get; set; }
        public int idEstatus { get; set; }
    }
}