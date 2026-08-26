using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// TEST DE CONEXIÓN A BASE DE DATOS (QAS / PRD)
var connectionString = builder.Configuration.GetConnectionString("TurnosQAS");

try
{
    using (var connection = new SqlConnection(connectionString))
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[BD] Probando conexión a SQL Server...");
        Console.ResetColor();

        connection.Open();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BD] => CONEXIÓN CON BD EXITOSA");
        Console.WriteLine($"[BD] Servidor: {connection.DataSource} | BD: {connection.Database}\n");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[BD] => ERROR AL CONECTAR CON BD: {ex.Message}\n");
    Console.ResetColor();
}

// Pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Calendario}/{action=Index}/{id?}");

app.Run();