// ══════════════════════════════════════════════════════════
// Modelo: ViviendaEmple - CORREGIDO según BD real
// ══════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using ProyectoRH2025.Models.Enums;

namespace ProyectoRH2025.Models
{
    public class ViviendaEmple
    {
        // ══════════════════════════════════════════════════════
        // CLAVE PRIMARIA COMPUESTA
        // ══════════════════════════════════════════════════════
        public int? idEmpleado { get; set; }  // ✅ int nullable
        public TipoDomicilio? TipoDomicilio { get; set; }  // ✅ tinyint (byte)

        // ══════════════════════════════════════════════════════
        // CAMPOS DE DIRECCIÓN
        // ══════════════════════════════════════════════════════
        [StringLength(30)]
        public string? Calle { get; set; }

        public int? NoExterior { get; set; }  // ✅ int, no string

        public int? NoInterior { get; set; }  // ✅ int, no string

        [StringLength(30)]
        public string? Colonia { get; set; }

        [StringLength(30)]
        public string? Ciudad { get; set; }

        [StringLength(50)]
        public string? Municipio { get; set; }

        [StringLength(30)]
        public string? Estado { get; set; }

        [StringLength(30)]
        public string? Pais { get; set; }

        public int? codPostal { get; set; }  // ✅ int, no string

        // ══════════════════════════════════════════════════════
        // INFORMACIÓN DE VIVIENDA
        // ══════════════════════════════════════════════════════
        public TipoVivienda? TipoVivienda { get; set; }  // ✅ tinyint (byte)

        [StringLength(50)]
        public string? NoCredito { get; set; }

        public bool? AutoPropio { get; set; }  // ✅ bit (bool)

        // ══════════════════════════════════════════════════════
        // AUDITORÍA
        // ══════════════════════════════════════════════════════
        public DateTime? FechaRegistro { get; set; }

        public int? UsuarioRegistro { get; set; }

        // ══════════════════════════════════════════════════════
        // NAVEGACIÓN
        // ══════════════════════════════════════════════════════
        public Empleado? Empleado { get; set; }
    }
}