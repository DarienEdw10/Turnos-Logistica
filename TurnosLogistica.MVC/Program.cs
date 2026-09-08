using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using Magna.Cosma.Autotek.Autentificacion.Library;
using Magna.Cosma.Autotek.Log;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Repositories;
using TurnosLogistica.MVC.Filters;
using TurnosLogistica.MVC.Middlewares;
using TurnosLogistica.MVC.Services;
using Logger = Magna.Cosma.Autotek.Log.Logger;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// =============================================================
// 1. CONFIGURACIÓN DEL LOGGER CORPORATIVO (Magna Autotek)
// =============================================================
Magna.Cosma.Autotek.Log.Settings logSettings = new();
builder.Configuration.GetSection("LogSettings").Bind(logSettings);

var directorioLogs = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
if (!Directory.Exists(directorioLogs))
{
    Directory.CreateDirectory(directorioLogs);
}

string nombreArchivoLog = string.IsNullOrWhiteSpace(logSettings.ArchivoDeLog)
    ? "TurnosLogistica-00.log"
    : logSettings.ArchivoDeLog;

logSettings.ArchivoDeLog = Path.Combine(directorioLogs, Path.GetFileName(nombreArchivoLog));
Logger logger = new(logSettings);

builder.Services.AddSingleton(logger);

// =============================================================
// 2. REPOSITORIOS Y ACCESO A DATOS
// =============================================================
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();

// =============================================================
// 3. CONFIGURACIÓN DE AUTENTICACIÓN CORPORATIVA (Magna Autotek)
// =============================================================
SettingsAutentificacion settingsAutentificacion = new();
builder.Configuration.GetSection("SettingsAutentificacion").Bind(settingsAutentificacion);

// Inyección como Singleton para mantener el caché de colaboradores en memoria
builder.Services.AddSingleton<RepositorioEmpleados>(sp =>
    new RepositorioEmpleados(settingsAutentificacion, logger));

// =============================================================
// 4. INYECCIÓN DE DEPENDENCIAS MVC, SERVICIOS Y FILTROS
// =============================================================
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("TurnosQAS"), sqlOptions =>
    {
        sqlOptions.CommandTimeout(10);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(2),
            errorNumbersToAdd: null);
    });
});

builder.Services.AddScoped<IPlanificacionService, PlanificacionService>();
builder.Services.AddScoped<UsuarioAuthService>();
builder.Services.AddScoped<ValidarOperacionPlantaAttribute>(); // <-- Filtro de protección multiplanta
builder.Services.AddHttpContextAccessor();

// Autenticación integrada de Windows Corporativa
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = NegotiateDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    })
    .AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

// =============================================================
// 5. PIPELINE HTTP
// =============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Autentica el token de Windows
app.UseAuthentication();

// Middleware silencioso: extrae CWID, resuelve Planta Asignada y Nivel
app.UseMiddleware<IdentificacionUsuarioMiddleware>();

// Autoriza según los claims resueltos
app.UseAuthorization();

// =============================================================
// 6. AUDITORÍA DE INICIO DE SESIÓN Y ACCESOS WEB
// =============================================================
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    bool esRutaVista = path == "" || path == "/" || path.StartsWith("/calendario") || path.StartsWith("/usuarios") || path.StartsWith("/auditoria");
    bool esLlamadaEstatica = path.Contains(".") || path.Contains("obtener");

    if (esRutaVista && !esLlamadaEstatica && context.User?.Identity?.IsAuthenticated == true)
    {
        string cwid = context.User.FindFirst("CWID")?.Value ?? context.User.Identity.Name ?? "Desconocido";
        var log = context.RequestServices.GetService<Logger>();

        log?.Registrar(
            nivel: Logger.NivelesLog.Detallado,
            tipo: Logger.TiposLog.Informativo,
            origen: "Seguridad.Acceso",
            texto: $"El usuario [{cwid}] ingresó a la vista [{path}].");
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Calendario}/{action=Index}/{id?}");

// =============================================================
// 7. PRECARGA ASÍNCRONA DEL PADRÓN AL ARRANCAR LA APLICACIÓN
// =============================================================
using (var scope = app.Services.CreateScope())
{
    var repoEmpleados = scope.ServiceProvider.GetRequiredService<RepositorioEmpleados>();
    _ = repoEmpleados.PrecargarPadronAsync();
}

app.Run();