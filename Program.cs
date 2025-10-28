using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== DIAGN�STICO DE CONFIGURACI�N =====
Console.WriteLine("=== DIAGN�STICO DE CONFIGURACI�N ===");
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

// ===== CONFIGURACI�N DE SERVICIOS =====

// Add services to the container.
builder.Services.AddRazorPages();

// CONFIGURACI�N DE BASE DE DATOS CON VALIDACI�N Y FALLBACK
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("? No se encontr� cadena de conexi�n en variables de entorno");

    // SOLO EN DESARROLLO: Usar appsettings.json como fallback
    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine("?? Entorno de desarrollo: Intentando usar appsettings.json...");

        // Recargar configuraci�n para asegurar que se lean los appsettings
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

        connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("? Usando cadena de conexi�n desde appsettings.json");
        }
        else
        {
            throw new InvalidOperationException("? No se encontr� cadena de conexi�n ni en variables de entorno ni en appsettings.json");
        }
    }
    else
    {
        throw new InvalidOperationException("? ERROR: No se encontr� la cadena de conexi�n 'DefaultConnection'. Verifica las variables de entorno en IIS.");
    }
}
else
{
    Console.WriteLine("? Usando cadena de conexi�n desde variables de entorno");
}

Console.WriteLine($"? Cadena de conexi�n configurada. Longitud: {connectionString.Length} caracteres");

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
builder.Services.AddScoped<ISharePointBatchService, SharePointBatchService>();

// HTTP CLIENT FACTORY para SharePoint (con timeout y pooling)
builder.Services.AddHttpClient("SharePointClient", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Timeout de 5 minutos para archivos grandes
    client.DefaultRequestHeaders.Add("User-Agent", "ProyectoRH2025-SharePoint-Client/1.0");
})
.ConfigureHttpMessageHandlerBuilder(builder =>
{
    // Configurar pool de conexiones
    builder.PrimaryHandler = new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10), // Reciclar conexiones cada 10 min
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 20 // Máximo 20 conexiones por servidor
    };
});

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

// ===== CONFIGURACI�N DEL PIPELINE =====

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

// Redirecci�n inicial
app.MapGet("/", context =>
{
    context.Response.Redirect("/Index");
    return Task.CompletedTask;
});

Console.WriteLine("? Aplicaci�n iniciada correctamente");
app.Run();