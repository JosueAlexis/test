using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Models;
using ProyectoRH2025.Models.Enums;
using ProyectoRH2025.MODELS;

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
        public DbSet<ViviendaEmple> tblViviendaEmple { get; set; }
        public DbSet<TblHuellasEmpleados> TblHuellasEmpleados { get; set; }
        public DbSet<TblImagenAsigSellos> TblImagenAsigSellos { get; set; }
        public DbSet<PodRecord> PodRecords { get; set; }
        public DbSet<PodEvidenciaImagen> PodEvidenciasImagenes { get; set; }
        public DbSet<CarteleraItem> CarteleraItems { get; set; }
        public DbSet<CarteleraConfig> CarteleraConfigs { get; set; }
        public DbSet<TblSellosHistorial> TblSellosHistorial { get; set; }
        public DbSet<TblTipoEmpleado> TblTipoEmpleado { get; set; }
        public DbSet<TblCuentas> TblCuentas { get; set; }
        public DbSet<TblUsuariosCuentas> TblUsuariosCuentas { get; set; }
        public DbSet<TblPermiso> TblPermiso { get; set; }
        public DbSet<TblModulo> TblModulo { get; set; }
        public DbSet<TblOpcion> TblOpcion { get; set; }
        public DbSet<TblPool> TblPool { get; set; }
        public DbSet<TblClientes> TblClientes { get; set; }
        public DbSet<TblSucursal> TblSucursal { get; set; }
        public DbSet<ReferenciaPersEmpleado> ReferenciasPersonalesEmpleados { get; set; }
        public DbSet<tblDocumentosEmpleado> tblDocumentosEmpleado { get; set; }
        public DbSet<Beneficiario> Beneficiarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Empleado>()
                .ToTable("tblEmpleados");

            modelBuilder.Entity<ImagenEmpleado>()
                .ToTable("tblimagenes");

            modelBuilder.Entity<TblAsigSellos>(entity =>
            {
                entity.ToTable(tb => tb.HasTrigger("Trigger_Generico"));
            });

            modelBuilder.Entity<TblTipoEmpleado>(entity =>
            {
                entity.ToTable("tblTipoEmpleado");
                entity.HasKey(e => e.id);
                entity.Property(e => e.TipEmpleado)
                      .IsRequired()
                      .HasMaxLength(50);
            });

            modelBuilder.Entity<PuestoEmpleado>(entity =>
            {
                entity.ToTable("tblPuestoEmpleados");
                entity.HasKey(e => e.id);
                entity.Property(e => e.id).HasColumnName("id");
                entity.Property(e => e.Puesto)
                      .IsRequired()
                      .HasMaxLength(200)
                      .HasColumnName("Puesto");
                entity.Property(e => e.idtipempleado)
                      .IsRequired()
                      .HasColumnName("idtipempleado");
                entity.HasOne(p => p.TipoEmpleado)
                      .WithMany(t => t.Puestos)
                      .HasForeignKey(p => p.idtipempleado)
                      .HasConstraintName("FK_tblPuestoEmpleados_tblTipoEmpleado")
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CarteleraConfig>()
                .HasIndex(c => c.ConfigKey)
                .IsUnique();

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.DisplayOrder);

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.IsActive);

            modelBuilder.Entity<CarteleraItem>()
                .HasIndex(c => c.UploadDate);

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

            modelBuilder.Entity<TblSellosHistorial>(entity =>
            {
                entity.ToTable("TblSellosHistorial");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.NumeroSello).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TipoMovimiento).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SupervisorNombreAnterior).HasMaxLength(200);
                entity.Property(e => e.SupervisorNombreNuevo).HasMaxLength(200);
                entity.Property(e => e.UsuarioNombre).HasMaxLength(200);
                entity.Property(e => e.Comentario).HasMaxLength(500);
                entity.Property(e => e.IP).HasMaxLength(50);
                entity.Property(e => e.FechaMovimiento).HasDefaultValueSql("GETDATE()");
                entity.HasOne(e => e.Sello)
                      .WithMany()
                      .HasForeignKey(e => e.SelloId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.SelloId).HasDatabaseName("IX_TblSellosHistorial_SelloId");
                entity.HasIndex(e => e.FechaMovimiento).HasDatabaseName("IX_TblSellosHistorial_FechaMovimiento");
                entity.HasIndex(e => e.SupervisorIdNuevo).HasDatabaseName("IX_TblSellosHistorial_SupervisorIdNuevo");
                entity.HasIndex(e => e.TipoMovimiento).HasDatabaseName("IX_TblSellosHistorial_TipoMovimiento");
                entity.HasIndex(e => e.UsuarioId).HasDatabaseName("IX_TblSellosHistorial_UsuarioId");
            });

            modelBuilder.Entity<TblPool>(entity =>
            {
                entity.ToTable("tblPool");
                entity.HasKey(e => e.id);
                entity.Property(e => e.Pool).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Pool).HasDatabaseName("IX_Pool_Nombre");
            });

            modelBuilder.Entity<TblClientes>(entity =>
            {
                entity.ToTable("tblClientes");
                entity.HasKey(e => e.codCliente);
                entity.Property(e => e.Cliente).IsRequired().HasMaxLength(200);
                entity.HasIndex(e => e.Cliente).HasDatabaseName("IX_Clientes_Nombre");
            });

            modelBuilder.Entity<TblSucursal>(entity =>
            {
                entity.ToTable("tblSucursal");
                entity.HasKey(e => e.id);
                entity.Property(e => e.Sucursal).IsRequired().HasMaxLength(200);
                entity.HasIndex(e => e.Sucursal).HasDatabaseName("IX_Sucursales_Nombre");
            });

            modelBuilder.Entity<TblCuentas>(entity =>
            {
                entity.ToTable("tblCuentas");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CodigoCuenta).IsUnique().HasDatabaseName("IX_Cuentas_CodigoCuenta");
                entity.HasIndex(e => e.EsActiva).HasDatabaseName("IX_Cuentas_EsActiva");
                entity.HasIndex(e => e.OrdenVisualizacion).HasDatabaseName("IX_Cuentas_Orden");
            });

            modelBuilder.Entity<TblUsuariosCuentas>(entity =>
            {
                entity.ToTable("tblUsuariosCuentas");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.IdUsuario).HasDatabaseName("IX_UsuariosCuentas_Usuario");
                entity.HasIndex(e => e.IdCuenta).HasDatabaseName("IX_UsuariosCuentas_Cuenta");
                entity.HasIndex(e => new { e.IdUsuario, e.IdCuenta, e.EsActivo })
                      .IsUnique()
                      .HasDatabaseName("IX_UsuariosCuentas_Unique")
                      .HasFilter("EsActivo = 1");
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.IdUsuario).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(e => e.Cuenta).WithMany(c => c.UsuariosAsignados).HasForeignKey(e => e.IdCuenta).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<TblUnidades>(entity =>
            {
                entity.ToTable("tblUnidades");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IdCuenta).HasColumnName("IdCuenta");
                entity.HasIndex(e => e.NumUnidad).IsUnique().HasDatabaseName("IX_Unidades_NumUnidad");
                entity.HasIndex(e => e.IdCuenta).HasDatabaseName("IX_Unidades_IdCuenta");
                entity.HasIndex(e => e.Pool).HasDatabaseName("IX_Unidades_Pool");
                entity.HasIndex(e => e.CodCliente).HasDatabaseName("IX_Unidades_CodCliente");
                entity.HasIndex(e => e.IdSucursal).HasDatabaseName("IX_Unidades_Sucursal");
                entity.HasOne(u => u.Cuenta).WithMany().HasForeignKey(u => u.IdCuenta).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(u => u.PoolNavigation).WithMany(p => p.Unidades).HasForeignKey(u => u.Pool).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
                entity.HasOne(u => u.Cliente).WithMany(c => c.Unidades).HasForeignKey(u => u.CodCliente).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(u => u.Sucursal).WithMany(s => s.Unidades).HasForeignKey(u => u.IdSucursal).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            });

            modelBuilder.Entity<ReferenciaPersEmpleado>()
                .ToTable("tblReferenciasPersonales");

            modelBuilder.Entity<ViviendaEmple>(entity =>
            {
                entity.ToTable("tblViviendaEmple");
                entity.HasKey(v => new { v.idEmpleado, v.TipoDomicilio });
                entity.Property(v => v.TipoDomicilio).HasConversion<byte>();
                entity.Property(v => v.TipoVivienda).HasConversion<byte?>();
            });

            modelBuilder.Entity<Empleado>(entity =>
            {
                entity.HasMany(e => e.Viviendas)
                      .WithOne(v => v.Empleado)
                      .HasForeignKey(v => v.idEmpleado)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ReferenciasPersonales)
                      .WithOne(r => r.Empleado)
                      .HasForeignKey(r => r.IdEmpleado)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.EstadoCivil).HasConversion<byte?>();
                entity.Property(e => e.Escolaridad).HasConversion<byte?>();
                entity.Property(e => e.NivelIngles).HasConversion<byte?>();
                entity.Property(e => e.FuenteReclutamiento).HasConversion<byte?>();
            });

            modelBuilder.Entity<tblDocumentosEmpleado>(entity =>
            {
                entity.ToTable(tb => tb.UseSqlOutputClause(false));

                entity.HasKey(e => e.id);

                // ✅ CRÍTICO: Definir explícitamente la relación con la columna correcta
                entity.HasOne(d => d.Empleado)
                      .WithMany(e => e.Documentos)  // Asegúrate que Empleado tenga esta propiedad
                      .HasForeignKey(d => d.idEmpleado)  // ← Nombre EXACTO de la columna
                      .HasConstraintName("FK_tblDocumentosEmpleado_tblEmpleados")
                      .OnDelete(DeleteBehavior.Cascade);

                // Configuración de columna computada
                entity.Property(e => e.Status)
                    .ValueGeneratedOnAddOrUpdate();

                // Índices para mejorar rendimiento
                entity.HasIndex(e => e.idEmpleado)
                    .HasDatabaseName("IX_DocumentosEmpleado_idEmpleado");

                entity.HasIndex(e => e.idTipDocumento)
                    .HasDatabaseName("IX_DocumentosEmpleado_idTipDocumento");

                // Índice compuesto para consultas frecuentes
                entity.HasIndex(e => new { e.idEmpleado, e.idTipDocumento })
                    .HasDatabaseName("IX_DocumentosEmpleado_Empleado_Tipo");
            });
        }
    }
}