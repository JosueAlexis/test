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

        // ✅ NUEVAS TABLAS PARA GESTIÓN DE CUENTAS Y RELACIONES
        public DbSet<TblCuentas> TblCuentas { get; set; }
        public DbSet<TblUsuariosCuentas> TblUsuariosCuentas { get; set; }
        public DbSet<TblPermiso> TblPermiso { get; set; }
        public DbSet<TblModulo> TblModulo { get; set; }
        public DbSet<TblOpcion> TblOpcion { get; set; }
        public DbSet<TblPool> TblPool { get; set; }
        public DbSet<TblClientes> TblClientes { get; set; }
        public DbSet<TblSucursal> TblSucursal { get; set; }

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

            // ================================================
            // ✅ CONFIGURACIÓN DE POOL
            // ================================================

            modelBuilder.Entity<TblPool>(entity =>
            {
                entity.ToTable("tblPool");
                entity.HasKey(e => e.id);

                entity.Property(e => e.Pool)
                      .IsRequired()
                      .HasMaxLength(100);

                // Índice para búsquedas
                entity.HasIndex(e => e.Pool)
                      .HasDatabaseName("IX_Pool_Nombre");
            });

            // ================================================
            // ✅ CONFIGURACIÓN DE CLIENTES
            // ================================================

            modelBuilder.Entity<TblClientes>(entity =>
            {
                entity.ToTable("tblClientes");
                entity.HasKey(e => e.codCliente);

                entity.Property(e => e.Cliente)
                      .IsRequired()
                      .HasMaxLength(200);

                // Índice para búsquedas
                entity.HasIndex(e => e.Cliente)
                      .HasDatabaseName("IX_Clientes_Nombre");
            });

            // ================================================
            // ✅ CONFIGURACIÓN DE SUCURSALES
            // ================================================

            modelBuilder.Entity<TblSucursal>(entity =>
            {
                entity.ToTable("tblSucursal");
                entity.HasKey(e => e.id);

                entity.Property(e => e.Sucursal)
                      .IsRequired()
                      .HasMaxLength(200);

                // Índice para búsquedas
                entity.HasIndex(e => e.Sucursal)
                      .HasDatabaseName("IX_Sucursales_Nombre");
            });

            // ================================================
            // ✅ CONFIGURACIÓN DE CUENTAS
            // ================================================

            modelBuilder.Entity<TblCuentas>(entity =>
            {
                entity.ToTable("tblCuentas");
                entity.HasKey(e => e.Id);

                // Código de cuenta único
                entity.HasIndex(e => e.CodigoCuenta)
                      .IsUnique()
                      .HasDatabaseName("IX_Cuentas_CodigoCuenta");

                // Índice para cuenta activa
                entity.HasIndex(e => e.EsActiva)
                      .HasDatabaseName("IX_Cuentas_EsActiva");

                // Índice para orden de visualización
                entity.HasIndex(e => e.OrdenVisualizacion)
                      .HasDatabaseName("IX_Cuentas_Orden");
            });

            // ================================================
            // ✅ CONFIGURACIÓN DE USUARIOS-CUENTAS
            // ================================================

            modelBuilder.Entity<TblUsuariosCuentas>(entity =>
            {
                entity.ToTable("tblUsuariosCuentas");
                entity.HasKey(e => e.Id);

                // Índice para búsquedas por usuario
                entity.HasIndex(e => e.IdUsuario)
                      .HasDatabaseName("IX_UsuariosCuentas_Usuario");

                // Índice para búsquedas por cuenta
                entity.HasIndex(e => e.IdCuenta)
                      .HasDatabaseName("IX_UsuariosCuentas_Cuenta");

                // Índice compuesto para evitar duplicados activos
                entity.HasIndex(e => new { e.IdUsuario, e.IdCuenta, e.EsActivo })
                      .IsUnique()
                      .HasDatabaseName("IX_UsuariosCuentas_Unique")
                      .HasFilter("EsActivo = 1");

                // Configurar relaciones
                entity.HasOne(e => e.Usuario)
                      .WithMany()
                      .HasForeignKey(e => e.IdUsuario)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Cuenta)
                      .WithMany(c => c.UsuariosAsignados)
                      .HasForeignKey(e => e.IdCuenta)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ================================================
            // ✅ CONFIGURACIÓN DE UNIDADES (CON TODAS LAS RELACIONES)
            // ================================================

            modelBuilder.Entity<TblUnidades>(entity =>
            {
                entity.ToTable("tblUnidades");
                entity.HasKey(e => e.id);

                // Mapear la propiedad IdCuenta a la columna de la BD
                entity.Property(e => e.IdCuenta)
                      .HasColumnName("IdCuenta");

                // ===== ÍNDICES =====

                // Índice único para número de unidad
                entity.HasIndex(e => e.NumUnidad)
                      .IsUnique()
                      .HasDatabaseName("IX_Unidades_NumUnidad");

                // Índice para búsquedas por cuenta
                entity.HasIndex(e => e.IdCuenta)
                      .HasDatabaseName("IX_Unidades_IdCuenta");

                // Índice para búsquedas por pool
                entity.HasIndex(e => e.Pool)
                      .HasDatabaseName("IX_Unidades_Pool");

                // Índice para búsquedas por cliente
                entity.HasIndex(e => e.CodCliente)
                      .HasDatabaseName("IX_Unidades_CodCliente");

                // Índice para búsquedas por sucursal
                entity.HasIndex(e => e.idSucursal)
                      .HasDatabaseName("IX_Unidades_Sucursal");

                // ===== RELACIONES =====

                // Relación con TblCuentas (REQUERIDA)
                entity.HasOne(u => u.Cuenta)
                      .WithMany()
                      .HasForeignKey(u => u.IdCuenta)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relación con TblPool (OPCIONAL)
                entity.HasOne(u => u.PoolNavigation)
                      .WithMany(p => p.Unidades)
                      .HasForeignKey(u => u.Pool)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);

                // Relación con TblClientes (REQUERIDA)
                entity.HasOne(u => u.Cliente)
                      .WithMany(c => c.Unidades)
                      .HasForeignKey(u => u.CodCliente)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relación con TblSucursal (OPCIONAL)
                entity.HasOne(u => u.Sucursal)
                      .WithMany(s => s.Unidades)
                      .HasForeignKey(u => u.idSucursal)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);
            });
        }
    }
}