namespace ProyectoRH2025.Models
{
    public class ReferenciaPersEmpleado
    {
        public int Id { get; set; }
        public int IdEmpleado { get; set; }
        public string NombreReferencia { get; set; } = string.Empty;
        public string? RelacionReferencia { get; set; }
        public string? TelefonoReferencia { get; set; }
        public bool Status { get; set; } = true;  // soft delete

        // Navigación (opcional, para Include)
        public Empleado? Empleado { get; set; }
    }
}