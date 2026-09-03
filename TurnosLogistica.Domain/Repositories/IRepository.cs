using System.Linq.Expressions;

namespace TurnosLogistica.Domain.Repositories;

public interface IRepository<T> where T : class
{
    // =========================================================
    // LECTURAS BÁSICAS
    // =========================================================
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(object id);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    // =========================================================
    // CONSULTAS OPTIMIZADAS (AUDITORÍA / REPORTES GRANDES)
    // =========================================================
    /// <summary>
    /// Expone el IQueryable con AsNoTracking() activo para filtrar, ordenar y proyectar directo en SQL.
    /// </summary>
    IQueryable<T> QueryNoTracking();

    /// <summary>
    /// Consulta paginada en SQL Server (evita traer miles de registros de golpe a memoria).
    /// </summary>
    Task<IEnumerable<T>> GetPagedAsync(
        Expression<Func<T, bool>>? predicate,
        int pagina,
        int registrosPorPagina,
        Expression<Func<T, object>>? orderBy = null,
        bool ordenDescendente = true);

    // =========================================================
    // ESCRITURA Y PERSISTENCIA
    // =========================================================
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    Task<int> SaveChangesAsync();
}