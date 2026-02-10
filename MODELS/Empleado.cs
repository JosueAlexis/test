// ============================================================
// Modelo: Empleado v3 CORREGIDO
// Solo campos GENERALES: TipoSangre, CuentaInfonavit
// Los campos AKNA van en tblDocumentosEmpleado
// ============================================================
using System;
using System.Collections.Generic;
using ProyectoRH2025.Models.Enums;

namespace ProyectoRH2025.Models
{
    public class Empleado
    {
        // ── Campos originales ─────────────────────────────────
        public int Id { get; set; }
        public int? Reloj { get; set; }
        public string? Names { get; set; }
        public string? Apellido { get; set; }
        public string? Apellido2 { get; set; }
        public DateTime? Fingreso { get; set; }
        public int? TipEmpleado { get; set; }
        public int? Jinmediato { get; set; }
        public DateTime? Fnacimiento { get; set; }
        public int? Puesto { get; set; }
        public DateTime? Fegreso { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? TelEmergencia { get; set; }
        public string? Rfc { get; set; }
        public string? Curp { get; set; }
        public string? NumSSocial { get; set; }
        public string? CodClientes { get; set; }
        public DateTime? FechaAlta { get; set; }
        public int? IdUsuarioAlta { get; set; }
        public int? Editor { get; set; }
        public int Status { get; set; }

        // Colección de documentos asociados al empleado
        public virtual ICollection<tblDocumentosEmpleado> Documentos { get; set; }
            = new List<tblDocumentosEmpleado>();

        // ── Campos con ENUMS ──────────────────────────────────
        public string? NombreEmergencia { get; set; }
        public string? LugarNacimiento { get; set; }
        public EstadoCivil? EstadoCivil { get; set; }
        public string? NombreConyuge { get; set; }
        public byte? NumHijos { get; set; }
        public Escolaridad? Escolaridad { get; set; }
        public NivelIngles? NivelIngles { get; set; }
        public bool? Fuma { get; set; }
        public bool? Alcohol { get; set; }
        public bool? Dopping { get; set; }
        public bool? Diabetes { get; set; }
        public bool? Hipertension { get; set; }
        public bool? EnfermedadCronica { get; set; }
        public bool? ConocePersEmple { get; set; }
        public FuenteReclutamiento? FuenteReclutamiento { get; set; }

        // ════════════════════════════════════════════════════════════════
        // ✅ NUEVOS CAMPOS GENERALES (Para TODOS: STIL y AKNA)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tipo de sangre del empleado (ej: O+, A-, AB+, etc.)
        /// </summary>
        public string? TipoSangre { get; set; }

        /// <summary>
        /// Indica si el empleado cuenta con crédito INFONAVIT
        /// </summary>
        public bool? CuentaInfonavit { get; set; }

        // ── Auditoría ─────────────────────────────────────────
        public DateTime? FechaUltimaModificacion { get; set; }
        public int? UsuarioUltimaModificacion { get; set; }

        // ── Navegación (uno a muchos) ─────────────────────────
        public ICollection<ViviendaEmple> Viviendas { get; set; }
            = new List<ViviendaEmple>();

        public ICollection<ReferenciaPersEmpleado> ReferenciasPersonales { get; set; }
            = new List<ReferenciaPersEmpleado>();

        // ════════════════════════════════════════════════════════════════
        // MÉTODOS HELPER
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Determina si este empleado es de AKNA
        /// codClientes = 2 (Akna)
        /// </summary>
        public bool EsEmpleadoAkna()
        {
            if (string.IsNullOrWhiteSpace(CodClientes))
                return false;

            // Solo codClientes = 2 (Akna)
            // NO incluir 3 (AknaNoDedi) según tu especificación
            return CodClientes == "2";
        }

        /// <summary>
        /// Determina si debe mostrar campos AKNA adicionales
        /// codClientes=2 AND TipEmpleado=1 AND Puesto=2
        /// </summary>
        public bool MostrarCamposAknaDocumentos()
        {
            return CodClientes == "2" && TipEmpleado == 1 && Puesto == 2;
        }
    }
}