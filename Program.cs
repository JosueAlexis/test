using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== DIAGNÓSTICO DE CONFIGURACIÓN =====
Console.WriteLine("=== DIAGNÓSTICO DE CONFIGURACIÓN ===");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// Verificar variables de entorno directamente
var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
Console.WriteLine($"Variable de entorno ConnectionStrings__DefaultConnection: {(!string.IsNullOrEmpty(envConnectionString) ? "? ENCONTRADA" : "? NO ENCONTRADA")}");

// Verificar en IConfiguration
var configConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"IConfiguration ConnectionString: {(!string.IsNullOrEmpty(configConnectionString) ? "? ENCONTRADA" : "? NO ENCONTRADA")}");

// Verificar SharePoint config
var sharePointSiteUrl = builder.Configuration["SharePoint:SiteUrl"];
Console.WriteLine($"SharePoint SiteUrl: {(!string.IsNullOrEmpty(sharePointSiteUrl) ? "? ENCONTRADA" : "? NO ENCONTRADA")}");

// ===== CONFIGURACIÓN DE SERVICIOS =====

// Add services to the container.
builder.Services.AddRazorPages();

// CONFIGURACIÓN DE BASE DE DATOS CON VALIDACIÓN Y FALLBACK
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("? No se encontró cadena de conexión en variables de entorno");

    // SOLO EN DESARROLLO: Usar appsettings.json como fallback
    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine("?? Entorno de desarrollo: Intentando usar appsettings.json...");

        // Recargar configuración para asegurar que se lean los appsettings
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

        connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("? Usando cadena de conexión desde appsettings.json");
        }
        else
        {
            throw new InvalidOperationException("? No se encontró cadena de conexión ni en variables de entorno ni en appsettings.json");
        }
    }
    else
    {
        throw new InvalidOperationException("? ERROR: No se encontró la cadena de conexión 'DefaultConnection'. Verifica las variables de entorno en IIS.");
    }
}
else
{
    Console.WriteLine("? Usando cadena de conexión desde variables de entorno");
}

Console.WriteLine($"? Cadena de conexión configurada. Longitud: {connectionString.Length} caracteres");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    // Logging adicional en desarrollo
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// SERVICIOS SHAREPOINT
builder.Services.Configure<SharePointConfig>(
    builder.Configuration.GetSection("SharePoint"));
builder.Services.AddScoped<ISharePointTestService, SharePointTestService>();

// HEALTH CHECKS
builder.Services.AddHealthChecks()
    .AddCheck<SharePointHealthCheck>("sharepoint");

// Habilitar soporte para sesiones
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Tiempo de inactividad
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ===== CONFIGURACIÓN DEL PIPELINE =====

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Habilitar middleware de sesiones
app.UseSession();
app.UseAuthorization();

app.MapRazorPages();

// HEALTH CHECK ENDPOINT
app.MapHealthChecks("/health");

// Redirección inicial
app.MapGet("/", context =>
{
    context.Response.Redirect("/Index");
    return Task.CompletedTask;
});

Console.WriteLine("? Aplicación iniciada correctamente");
app.Run();