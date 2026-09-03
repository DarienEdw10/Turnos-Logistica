using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.Domain.Repositories;

public interface IAuditoriaRepository
{
    Task<List<RegistroAuditoriaDto>> ConsultarHistorialAsync(int plantaId, DateTime fechaInicio, DateTime fechaFin);
}