using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Models;
using ProyectoRH2025.MODELS;

namespace ProyectoRH2025.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets existentes
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
        public DbSet<TblImagenAsigSellos> TblImagenAsigSellos { get; set; }
        public DbSet<PodRecord> PodRecords { get; set; }
        public DbSet<PodEvidenciaImagen> PodEvidenciasImagenes { get; set; }
        public DbSet<CarteleraItem> CarteleraItems { get; set; }
        public DbSet<CarteleraConfig> CarteleraConfigs { get; set; }
        public DbSet<TblSellosHistorial> TblSellosHistorial { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TblAsigSellos>(entity =>
            {
                entity.ToTable(tb => tb.HasTrigger("Trigger_Generico"));
            });

            // Configuración existente
            modelBuilder.Entity<PuestoEmpleado>()
                .ToTable("tblPuestoEmpleados");

            // Configuraciones para Cartelera Digital
            modelBuilder.Entity<CarteleraConfig>()
                .HasIndex(c => c.ConfigKey)
                .IsUnique();

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.DisplayOrder);

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.IsActive);

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.UploadDate);

            // Configuración de TblImagenAsigSellos
            modelBuilder.Entity<TblImagenAsigSellos>(entity =>
            {
                entity.ToTable("tblImagenAsigSellos");
                entity.HasKey(e => e.id);

                entity.Property(e => e.Imagen)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.FSubidaEvidencia)
                    .HasDefaultValueSql("GETDATE()");
            });

            // Configuración para TblSellosHistorial
            modelBuilder.Entity<TblSellosHistorial>(entity =>
            {
                entity.ToTable("TblSellosHistorial");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.NumeroSello)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.TipoMovimiento)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.SupervisorNombreAnterior)
                      .HasMaxLength(200);

                entity.Property(e => e.SupervisorNombreNuevo)
                      .HasMaxLength(200);

                entity.Property(e => e.UsuarioNombre)
                      .HasMaxLength(200);

                entity.Property(e => e.Comentario)
                      .HasMaxLength(500);

                entity.Property(e => e.IP)
                      .HasMaxLength(50);

                entity.Property(e => e.FechaMovimiento)
                      .HasDefaultValueSql("GETDATE()");

                entity.HasOne(e => e.Sello)
                      .WithMany()
                      .HasForeignKey(e => e.SelloId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Índices
                entity.HasIndex(e => e.SelloId)
                      .HasDatabaseName("IX_TblSellosHistorial_SelloId");

                entity.HasIndex(e => e.FechaMovimiento)
                      .HasDatabaseName("IX_TblSellosHistorial_FechaMovimiento");

                entity.HasIndex(e => e.SupervisorIdNuevo)
                      .HasDatabaseName("IX_TblSellosHistorial_SupervisorIdNuevo");

                entity.HasIndex(e => e.TipoMovimiento)
                      .HasDatabaseName("IX_TblSellosHistorial_TipoMovimiento");

                entity.HasIndex(e => e.UsuarioId)
                      .HasDatabaseName("IX_TblSellosHistorial_UsuarioId");
            });
        }
    }
}