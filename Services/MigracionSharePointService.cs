using System.Collections.Concurrent;

namespace ProyectoRH2025.Services;

public class MigracionSharePointService
{
    private readonly ConcurrentDictionary<Guid, ProgresoMigracion> _migracionesActivas = new();

    // Propiedad para acceder al ID actual
    public Guid? MigrationIdActual { get; private set; }

    public Guid IniciarMigracion(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        int tamanoLote,
        bool soloPendientes,
        bool sobreescribir,
        string usuario,
        Func<Task> accionMigracion)
    {
        var migrationId = Guid.NewGuid();
        MigrationIdActual = migrationId;

        var progreso = new ProgresoMigracion
        {
            MigrationId = migrationId,
            TotalProcesar = 0,
            Procesadas = 0,
            Exitosas = 0,
            Fallidas = 0,
            Completado = false,
            Inicio = DateTime.Now,
            Usuario = usuario,
            Logs = new List<LogEntry>()
        };

        _migracionesActivas[migrationId] = progreso;

        _ = Task.Run(async () =>
        {
            try
            {
                await accionMigracion();
            }
            catch (Exception ex)
            {
                progreso.AgregarLog("error", $"Excepción crítica durante migración: {ex.Message}");
            }
            finally
            {
                progreso.Completado = true;
                MigrationIdActual = null;
            }
        });

        return migrationId;
    }

    public ProgresoMigracion? ObtenerProgreso(Guid migrationId)
    {
        _migracionesActivas.TryGetValue(migrationId, out var progreso);
        return progreso;
    }

    public void LimpiarMigracionFinalizada(Guid migrationId)
    {
        if (_migracionesActivas.TryGetValue(migrationId, out var p) && p.Completado)
        {
            _migracionesActivas.TryRemove(migrationId, out _);
        }
    }
}

public class ProgresoMigracion
{
    public Guid MigrationId { get; set; }
    public int TotalProcesar { get; set; }
    public int Procesadas { get; set; }
    public int Exitosas { get; set; }
    public int Fallidas { get; set; }
    public bool Completado { get; set; }
    public DateTime Inicio { get; set; }
    public string? Usuario { get; set; }
    public List<LogEntry> Logs { get; set; } = new();

    public int Porcentaje => TotalProcesar > 0 ? (int)((Procesadas / (double)TotalProcesar) * 100) : 0;

    public void AgregarLog(string tipo, string mensaje)
    {
        lock (Logs)
        {
            Logs.Add(new LogEntry
            {
                Tipo = tipo,
                Mensaje = mensaje,
                Timestamp = DateTime.Now
            });

            // Limitar para no crecer indefinidamente
            if (Logs.Count > 300)
                Logs.RemoveRange(0, Logs.Count - 300);
        }
    }
}

public class LogEntry
{
    public string Tipo { get; set; } = "info";
    public string Mensaje { get; set; } = "";
    public DateTime Timestamp { get; set; }
}