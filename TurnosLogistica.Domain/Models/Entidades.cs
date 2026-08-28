using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnosLogistica.Domain.Models;

[Table("Plantas", Schema = "MPS")]
public class Planta
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nombre")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("codigo")]
    [MaxLength(20)]
    public string Codigo { get; set; } = string.Empty;

    [Column("timezone")]
    [MaxLength(50)]
    public string Timezone { get; set; } = "America/Mexico_City";

    [Column("activa")]
    public bool Activa { get; set; } = true;

    [Column("creado_at")]
    public DateTime CreadoAt { get; set; } = DateTime.UtcNow;
}

[Table("Usuarios", Schema = "MPS")]
public class Usuario
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("planta_id")]
    public int PlantaId { get; set; }

    [Column("cwid")]
    [MaxLength(50)]
    public string? CWID { get; set; }

    [Column("nombre")]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [Column("no_empleado")]
    [MaxLength(30)]
    public string NoEmpleado { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Column("rol")]
    [MaxLength(30)]
    public string Rol { get; set; } = "operador"; // 'operador','jefe_log','admin','sistemas'

    [Column("nivel")]
    public int Nivel { get; set; } = 10;

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("creado_at")]
    public DateTime CreadoAt { get; set; } = DateTime.UtcNow;
}

[Table("Proyectos", Schema = "MPS")]
public class Proyecto
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("planta_id")]
    public int PlantaId { get; set; }

    [Column("nombre")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("codigo")]
    [MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("activo")]
    public bool Activo { get; set; } = true;
}

[Table("Lineas", Schema = "MPS")]
public class Linea
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("planta_id")]
    public int PlantaId { get; set; }

    [Column("nombre")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("codigo")]
    [MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("activa")]
    public bool Activa { get; set; } = true;

    [Column("proyecto_id")]
    public int? ProyectoId { get; set; }
}

[Table("Celdas", Schema = "MPS")]
public class Celda
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("linea_id")]
    public int LineaId { get; set; }

    [Column("nombre")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("codigo")]
    [MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("activa")]
    public bool Activa { get; set; } = true;
}

[Table("Estaciones", Schema = "MPS")]
public class Estacion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("celda_id")]
    public int CeldaId { get; set; }

    [Column("nombre")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("codigo")]
    [MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("activa")]
    public bool Activa { get; set; } = true;
}

[Table("Turnos", Schema = "MPS")]
public class Turno
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("planta_id")]
    public int PlantaId { get; set; }

    [Column("nombre")]
    [MaxLength(50)]
    public string Nombre { get; set; } = string.Empty;

    [Column("hora_inicio")]
    public TimeSpan HoraInicio { get; set; }

    [Column("hora_fin")]
    public TimeSpan HoraFin { get; set; }

    [Column("activo")]
    public bool Activo { get; set; } = true;

    // Propiedad calculada: Duración en horas manejando cruces de medianoche
    [NotMapped]
    public double DuracionHoras
    {
        get
        {
            if (HoraFin <= HoraInicio)
            {
                return (HoraFin.Add(TimeSpan.FromDays(1)) - HoraInicio).TotalHours;
            }
            return (HoraFin - HoraInicio).TotalHours;
        }
    }

    // Propiedad calculada: Clase CSS según el turno
    [NotMapped]
    public string ClaseColor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Nombre)) return "t1";
            var upper = Nombre.ToUpperInvariant();
            if (upper.StartsWith("1") || upper.Contains("MATUTINO") || upper.Contains("12H-D")) return "t1";
            if (upper.StartsWith("2") || upper.Contains("VESPERTINO")) return "t2";
            if (upper.StartsWith("3") || upper.Contains("NOCTURNO") || upper.Contains("12H-N")) return "t3";
            return "t1";
        }
    }

    // Método estático auxiliar para llamadas directas
    public static double CalcularDuracionHoras(TimeSpan inicio, TimeSpan fin)
    {
        if (fin <= inicio)
        {
            return (fin.Add(TimeSpan.FromDays(1)) - inicio).TotalHours;
        }
        return (fin - inicio).TotalHours;
    }
}

[Table("Numeros_de_parte", Schema = "MPS")]
public class NumeroDeParte
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("planta_id")]
    public int PlantaId { get; set; }

    [Column("linea_id")]
    public int LineaId { get; set; }

    [Column("celda_id")]
    public int? CeldaId { get; set; }

    [Column("sap_part_number")]
    [MaxLength(50)]
    public string SapPartNumber { get; set; } = string.Empty;

    [Column("no_de_parte")]
    [MaxLength(50)]
    public string NoDeParte { get; set; } = string.Empty;

    [Column("descripcion")]
    [MaxLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    [Column("final_assembly")]
    [MaxLength(100)]
    public string? FinalAssembly { get; set; }

    [Column("familia")]
    [MaxLength(50)]
    public string? Familia { get; set; }

    [Column("oa")]
    public decimal OA { get; set; }

    [Column("jph")]
    public int JPH { get; set; }

    [Column("imagen_ayuda_visual")]
    [MaxLength(500)]
    public string? ImagenAyudaVisual { get; set; }

    [Column("umbral_critico")]
    public int UmbralCritico { get; set; } = 10;

    [Column("umbral_bajo")]
    public int UmbralBajo { get; set; } = 25;

    [Column("umbral_aceptable")]
    public int UmbralAceptable { get; set; } = 50;

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("creado_at")]
    public DateTime CreadoAt { get; set; } = DateTime.UtcNow;
}

[Table("Programacion_produccion", Schema = "MPS")]
public class ProgramacionProduccion
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("numero_parte_id")]
    public int NumeroParteId { get; set; }

    [Column("fecha")]
    public DateTime Fecha { get; set; }

    [Column("turno_id")]
    public int TurnoId { get; set; }

    [Column("cantidad_programada")]
    public int CantidadProgramada { get; set; } = 0;

    [Column("orden_producir")]
    public int OrdenProducir { get; set; } = 1;

    [Column("ventanas_salida")]
    public int VentanasSalida { get; set; } = 0;

    [Column("estatus")]
    [MaxLength(30)]
    public string Estatus { get; set; } = "pendiente"; // 'pendiente','aprobado','en_proceso','completado','cancelado'

    [Column("aprobado_por")]
    public int? AprobadoPor { get; set; }

    [Column("aprobado_at")]
    public DateTime? AprobadoAt { get; set; }

    [Column("razon_cambio")]
    [MaxLength(500)]
    public string? RazonCambio { get; set; }

    [Column("creado_por")]
    public int CreadoPor { get; set; }

    [Column("creado_at")]
    public DateTime CreadoAt { get; set; } = DateTime.UtcNow;
}

[Table("Historial_agenda", Schema = "MPS")]
public class HistorialAgenda
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("programacion_id")]
    public long ProgramacionId { get; set; }

    [Column("accion")]
    [MaxLength(30)]
    public string Accion { get; set; } = string.Empty;

    [Column("valor_anterior")]
    public string? ValorAnterior { get; set; }

    [Column("valor_nuevo")]
    public string ValorNuevo { get; set; } = string.Empty;

    [Column("razon")]
    [MaxLength(500)]
    public string Razon { get; set; } = string.Empty;

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("fecha_accion")]
    public DateTime FechaAccion { get; set; } = DateTime.UtcNow;
}

[Table("Inventario_diario", Schema = "MPS")]
public class InventarioDiario
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("numero_parte_id")]
    public int NumeroParteId { get; set; }

    [Column("fecha")]
    public DateTime Fecha { get; set; }

    [Column("inventario_inicio_dia")]
    public int InventarioInicioDia { get; set; } = 0;

    [Column("inventario_cierre")]
    public int InventarioCierre { get; set; } = 0;

    [Column("batch_hold")]
    public int BatchHold { get; set; } = 0;

    [Column("bloqueado")]
    public int Bloqueado { get; set; } = 0;

    [Column("creado_por")]
    public int CreadoPor { get; set; }

    [Column("creado_at")]
    public DateTime CreadoAt { get; set; } = DateTime.UtcNow;
}