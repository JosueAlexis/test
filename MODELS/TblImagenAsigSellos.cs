using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblImagenAsigSellos")]
    public class TblImagenAsigSellos
    {
        [Key]
        public int id { get; set; }

        public int idTabla { get; set; }

        // ✅ NUEVO: Guardar Base64 en lugar de ruta
        [Required]
        public string Imagen { get; set; }  // Base64 de imagen comprimida

        public string? ImagenThumbnail { get; set; }  // Base64 de miniatura

        public int? TamanoOriginal { get; set; }  // KB

        public int? TamanoComprimido { get; set; }  // KB

        public string? TipoArchivo { get; set; }  // "imagen" o "pdf"

        public DateTime FSubidaEvidencia { get; set; }

        public int? Editor { get; set; }

        // Navegación
        [ForeignKey("idTabla")]
        public virtual TblAsigSellos? Asignacion { get; set; }
    }
}