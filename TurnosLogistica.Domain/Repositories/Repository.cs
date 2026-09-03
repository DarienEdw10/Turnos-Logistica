using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;

namespace TurnosLogistica.Domain.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // =========================================================
    // LECTURAS BÁSICAS
    // =========================================================

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public async Task<T?> GetByIdAsync(object id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate != null
            ? await _dbSet.CountAsync(predicate)
            : await _dbSet.CountAsync();
    }

    // =========================================================
    // CONSULTAS OPTIMIZADAS (AUDITORÍA / REPORTES GRANDES)
    // =========================================================

    public IQueryable<T> QueryNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    public async Task<IEnumerable<T>> GetPagedAsync(
        Expression<Func<T, bool>>? predicate,
        int pagina,
        int registrosPorPagina,
        Expression<Func<T, object>>? orderBy = null,
        bool ordenDescendente = true)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        if (orderBy != null)
        {
            query = ordenDescendente
                ? query.OrderByDescending(orderBy)
                : query.OrderBy(orderBy);
        }

        int skip = Math.Max(0, (pagina - 1) * registrosPorPagina);

        return await query
            .Skip(skip)
            .Take(registrosPorPagina)
            .ToListAsync();
    }

    // =========================================================
    // ESCRITURA Y PERSISTENCIA
    // =========================================================

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}