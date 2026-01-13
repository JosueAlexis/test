using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.MODELS
{
    [Table("tblCarteleraItems")]
    public class CarteleraItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } // "image", "pdf", "video"

        [Required]
        [StringLength(500)]
        public string SharePointPath { get; set; }

        [Required]
        [StringLength(500)]
        public string SharePointUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        public int DurationSeconds { get; set; } = 10;

        public DateTime UploadDate { get; set; } = DateTime.Now;

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [StringLength(100)]
        public string? UploaderUserId { get; set; }  // ← AGREGADO ? para permitir null

        [StringLength(500)]
        public string? Description { get; set; }  // ← AGREGADO ? para permitir null

        public long FileSize { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }  // ← AGREGADO ? para permitir null

        [StringLength(50)]
        public string? MimeType { get; set; }  // ← AGREGADO ? para permitir null
    }
}