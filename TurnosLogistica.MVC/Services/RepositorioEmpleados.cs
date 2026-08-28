using System.Collections.Concurrent;
using Magna.Cosma.Autotek.Autentificacion.Library;
using Magna.Cosma.Autotek.Log;

namespace TurnosLogistica.MVC.Services;

public class RepositorioEmpleados
{
    private readonly RepositorioDB? _repositorioDb;
    private readonly Logger _logger;

    private static readonly ConcurrentDictionary<string, Empleado> _indicePorCwid = new(StringComparer.OrdinalIgnoreCase);
    private static List<Empleado> _listaEmpleados = new();
    private static DateTime _ultimaCarga = DateTime.MinValue;
    private static readonly SemaphoreSlim _semaforoCarga = new(1, 1);

    public RepositorioEmpleados(SettingsAutentificacion settingsAutentificacion, Logger logger)
    {
        _logger = logger;
        try
        {
            _repositorioDb = new RepositorioDB(settingsAutentificacion);
        }
        catch (Exception ex)
        {
            _logger.Registrar(
                nivel: Logger.NivelesLog.Basico,
                tipo: Logger.TiposLog.Errores,
                origen: nameof(RepositorioEmpleados),
                texto: $"Error al inicializar RepositorioDB: {ex.Message}");
            _repositorioDb = null;
        }
    }

    public List<Empleado> ObtenerEmpleados(bool soloActivos = true)
    {
        if (_repositorioDb == null) return new List<Empleado>();

        if (_listaEmpleados.Count > 0 && (DateTime.Now - _ultimaCarga).TotalMinutes <= 60)
        {
            return soloActivos ? _listaEmpleados.Where(e => e.Activo).ToList() : _listaEmpleados;
        }

        try
        {
            var empleados = _repositorioDb.ObtenerEmpleados(soloActivos);
            if (empleados != null && empleados.Count > 0)
            {
                _listaEmpleados = empleados;
                IndexarEmpleados(empleados);
                _ultimaCarga = DateTime.Now;
            }
            return empleados ?? new List<Empleado>();
        }
        catch (Exception ex)
        {
            _logger.Registrar(
                nivel: Logger.NivelesLog.Basico,
                tipo: Logger.TiposLog.Errores,
                origen: nameof(RepositorioEmpleados),
                texto: $"Error al consultar empleados: {ex.Message}");
            return new List<Empleado>();
        }
    }

    public async Task PrecargarPadronAsync()
    {
        if (_repositorioDb == null) return;

        await _semaforoCarga.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var empleados = _repositorioDb.ObtenerEmpleados(soloActivos: true);
                if (empleados == null) return;

                _listaEmpleados = empleados;
                IndexarEmpleados(empleados);
                _ultimaCarga = DateTime.Now;
            });
        }
        catch (Exception ex)
        {
            _logger.Registrar(
                nivel: Logger.NivelesLog.Basico,
                tipo: Logger.TiposLog.Errores,
                origen: nameof(RepositorioEmpleados),
                texto: $"Error durante precarga de padrón: {ex.Message}");
        }
        finally
        {
            _semaforoCarga.Release();
        }
    }

    private static void IndexarEmpleados(List<Empleado> empleados)
    {
        foreach (var emp in empleados)
        {
            if (emp == null) continue;

            if (!string.IsNullOrWhiteSpace(emp.Codigo))
                _indicePorCwid[emp.Codigo.Trim()] = emp;

            if (emp.CWIDs != null)
            {
                foreach (var c in emp.CWIDs)
                {
                    string cwidStr = c?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(cwidStr))
                        _indicePorCwid[cwidStr] = emp;
                }
            }

            if (emp.Correos != null)
            {
                foreach (var corr in emp.Correos)
                {
                    if (corr != null && !string.IsNullOrWhiteSpace(corr.Direccion))
                    {
                        string prefijo = corr.Direccion.Split('@')[0].Trim();
                        _indicePorCwid.TryAdd(prefijo, emp);
                    }
                }
            }
        }
    }

    public Empleado? ObtenerEmpleadoPorCWID(string cwid)
    {
        if (string.IsNullOrWhiteSpace(cwid)) return null;

        string cwidLimpio = cwid.Contains('\\') ? cwid.Split('\\')[1].Trim() : cwid.Trim();

        if (_indicePorCwid.TryGetValue(cwidLimpio, out var empleado))
        {
            return empleado;
        }

        if (_indicePorCwid.IsEmpty)
        {
            ObtenerEmpleados(soloActivos: true);
            if (_indicePorCwid.TryGetValue(cwidLimpio, out empleado))
            {
                return empleado;
            }
        }

        if ((DateTime.Now - _ultimaCarga).TotalMinutes > 60)
        {
            _ = Task.Run(PrecargarPadronAsync);
        }

        return null;
    }

    public List<Empleado> BuscarEmpleados(string termino, int limite = 50)
    {
        var todos = ObtenerEmpleados(soloActivos: true);
        if (string.IsNullOrWhiteSpace(termino))
        {
            return todos.Take(limite).ToList();
        }

        string clean = termino.Trim().ToLowerInvariant();

        return todos.Where(e =>
            (!string.IsNullOrEmpty(e.Codigo) && e.Codigo.ToLowerInvariant().Contains(clean)) ||
            (!string.IsNullOrEmpty(e.Nombre) && e.Nombre.ToLowerInvariant().Contains(clean)) ||
            (!string.IsNullOrEmpty(e.ApellidoPaterno) && e.ApellidoPaterno.ToLowerInvariant().Contains(clean)) ||
            (!string.IsNullOrEmpty(e.ApellidoMaterno) && e.ApellidoMaterno.ToLowerInvariant().Contains(clean)) ||
            (e.CWIDs != null && e.CWIDs.Any(c => c.ToString()?.ToLowerInvariant().Contains(clean) == true)) ||
            (e.Correos != null && e.Correos.Any(c => c.Direccion?.ToLowerInvariant().Contains(clean) == true))
        ).Take(limite).ToList();
    }
}