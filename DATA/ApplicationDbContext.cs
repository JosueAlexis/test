using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Models; // Para tus modelos existentes si están en este namespace (ej. Empleado, Usuario)
using ProyectoRH2025.MODELS;   // Para los nuevos modelos PodRecord, PodEvidenciaImagen
// Asegúrate que el namespace sea exactamente "ProyectoRH2025.Data"
namespace ProyectoRH2025.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<ImagenEmpleado> ImagenesEmpleados { get; set; }
        public DbSet<PuestoEmpleado> PuestoEmpleados { get; set; } = null!;
        public DbSet<Usuario> TblUsuarios { get; set; }
        public DbSet<TblHistoricoPass> TblHistoricoPass { get; set; }
        public DbSet<TblRolusuario> TblRolusuario { get; set; }
        public DbSet<TblSellos> TblSellos { get; set; }
        public DbSet<TblUnidades> TblUnidades { get; set; }
        public DbSet<TblAsigSellos> TblAsigSellos { get; set; }
        public DbSet<TblTipoAsignacion> TblTipoAsignacion { get; set; }
        public DbSet<ViviendaEmpleado> tblViviendaEmple { get; set; }
        public DbSet<TblHuellasEmpleados> TblHuellasEmpleados { get; set; }
        // public DbSet<ImagenAsignacion> ImagenAsignacion { get; set; } // Verifica si esta entidad es parte de tu contexto

        // --- DBSETS PARA LIQUIDACIONES ---
        // Estas entidades (PodRecord, PodEvidenciaImagen) deben estar definidas en el namespace ProyectoRH2025.MODELS
        public DbSet<PodRecord> PodRecords { get; set; }
        public DbSet<PodEvidenciaImagen> PodEvidenciasImagenes { get; set; }

        // --- NUEVOS DBSETS PARA CARTELERA DIGITAL ---
        public DbSet<CarteleraItem> CarteleraItems { get; set; }
        public DbSet<CarteleraConfig> CarteleraConfigs { get; set; }
        // ---------------------------------------------

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tu configuración existente
            modelBuilder.Entity<PuestoEmpleado>()
                .ToTable("tblPuestoEmpleados");

            // Configuraciones para nuevos modelos (si son necesarias en el futuro)
            // modelBuilder.Entity<PodRecord>()
            // .HasMany(p => p.PodEvidenciasImagenes)
            // .WithOne(e => e.PodRecord)
            // .HasForeignKey(e => e.POD_ID_FK);
            // modelBuilder.Entity<PodEvidenciaImagen>()
            // .HasOne(e => e.PodRecord)
            // .WithMany(p => p.PodEvidenciasImagenes)
            // .HasForeignKey(e => e.POD_ID_FK);

            // --- CONFIGURACIONES PARA CARTELERA DIGITAL ---

            // Configuración para CarteleraConfig - ConfigKey debe ser único
            modelBuilder.Entity<CarteleraConfig>()
                .HasIndex(c => c.ConfigKey)
                .IsUnique();

            // Configuración para CarteleraItem - Índices para mejorar rendimiento
            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.DisplayOrder);

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.IsActive);

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.UploadDate);

            // -----------------------------------------------
        }
    }
}