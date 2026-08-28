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
    }
}