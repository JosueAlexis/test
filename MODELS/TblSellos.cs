using ProyectoRH2025.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

public class TblSellos
{
    public int Id { get; set; }

    public string Sello { get; set; }

    public DateTime? Fentrega { get; set; }

    public string? Recibio { get; set; }

    public int Status { get; set; }

    public int? SupervisorId { get; set; }

    // ✅ NUEVO CAMPO: ID de la cuenta a la que pertenece el sello
    public int? IdCuenta { get; set; }

    public DateTime? FechaAsignacion { get; set; }

    public int? Alta { get; set; }

    [ForeignKey("SupervisorId")]
    public virtual Usuario? Supervisor { get; set; }

    // ✅ NUEVA RELACIÓN: Enlace directo al modelo TblCuentas
    [ForeignKey("IdCuenta")]
    public virtual TblCuentas? Cuenta { get; set; }
}