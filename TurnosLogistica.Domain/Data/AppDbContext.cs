using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.Domain.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Planta> Plantas => Set<Planta>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<Linea> Lineas => Set<Linea>();
    public DbSet<Celda> Celdas => Set<Celda>();
    public DbSet<Estacion> Estaciones => Set<Estacion>();
    public DbSet<NumeroDeParte> NumerosDeParte => Set<NumeroDeParte>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<TurnoParo> TurnoParos => Set<TurnoParo>(); // <-- REGISTRAR AQUÍ
    public DbSet<ProgramacionProduccion> Programaciones => Set<ProgramacionProduccion>();
    public DbSet<HistorialAgenda> HistorialAgendas => Set<HistorialAgenda>();
    public DbSet<InventarioDiario> InventariosDiarios => Set<InventarioDiario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("MPS");

        modelBuilder.Entity<NumeroDeParte>()
            .Property(p => p.OA)
            .HasPrecision(5, 2);

        // Mapeo explícito de tabla para TurnoParos
        modelBuilder.Entity<TurnoParo>(entity =>
        {
            entity.ToTable("TurnoParos", "MPS");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TurnoId).HasColumnName("turno_id");
            entity.Property(e => e.ProgramacionId).HasColumnName("programacion_id");
            entity.Property(e => e.TipoParo).HasColumnName("tipo_paro").HasMaxLength(50).IsRequired();
            entity.Property(e => e.DuracionMinutos).HasColumnName("duracion_minutos").IsRequired();
            entity.Property(e => e.EsProgramado).HasColumnName("es_programado");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(250);
            entity.Property(e => e.Activo).HasColumnName("activo");
        });
    }
}