using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoRH2025.Models
{
    [Table("tblCarteleraConfig")]
    public class CarteleraConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ConfigKey { get; set; }

        [Required]
        [StringLength(500)]
        public string ConfigValue { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}