using Hangfire;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.BackgroundJobs;
using Microsoft.AspNetCore.Http.Features;
using ProyectoRH2025.Data;
using ProyectoRH2025.Services;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// 👇 NUEVO: AUMENTAR EL LÍMITE DE SUBIDA DE ARCHIVOS A 100 MB 👇
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 104857600; // 100 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});
// 👆 FIN DE CONFIGURACIÓN DE LÍMITES 👆

// ===== DIAGNÓSTICO DE CONFIGURACIÓN =====
Console.WriteLine("=== DIAGNÓSTICO DE CONFIGURACIÓN ===");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
Console.WriteLine($"Variable de entorno ConnectionStrings__DefaultConnection: {(!string.IsNullOrEmpty(envConnectionString) ? "✅ ENCONTRADA" : "❌ NO ENCONTRADA")}");

var configConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"IConfiguration ConnectionString: {(!string.IsNullOrEmpty(configConnectionString) ? "✅ ENCONTRADA" : "❌ NO ENCONTRADA")}");

var sharePointSiteUrl = builder.Configuration["SharePoint:SiteUrl"];
Console.WriteLine($"SharePoint SiteUrl: {(!string.IsNullOrEmpty(sharePointSiteUrl) ? "✅ ENCONTRADA" : "❌ NO ENCONTRADA")}");

// ===== CONFIGURACIÓN DE SERVICIOS =====

builder.Services.AddRazorPages(options =>
{
    // Regla de Microsoft para permitir acceso anónimo
    options.Conventions.AllowAnonymousToPage("/Cartelera/Display");
});

builder.Services.AddControllers();

//AGREGAR SERVICIO DE QR
builder.Services.AddScoped<QRCodeService>();
builder.Services.AddScoped<ImagenService>();

// Servicio que maneja el progreso de migraciones
builder.Services.AddSingleton<MigracionSharePointService>();
//SERIVIO DE PERMISOS//
builder.Services.AddScoped<PermisosService>();

// SERVICIOS PARA REPORTES EN SEGUNDO PLANO Y LIMPIEZA
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IReporteMasivoJob, ReporteMasivoJob>();
builder.Services.AddScoped<ILimpiezaZipsJob, LimpiezaZipsJob>();

// CONFIGURACIÓN DE BASE DE DATOS CON VALIDACIÓN Y FALLBACK
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("⚠ No se encontró cadena de conexión en variables de entorno");

    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine("🔧 Entorno de desarrollo: Intentando usar appsettings.json...");

        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

        connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("✅ Usando cadena de conexión desde appsettings.json");
        }
        else
        {
            throw new InvalidOperationException("❌ No se encontró cadena de conexión ni en variables de entorno ni en appsettings.json");
        }
    }
    else
    {
        throw new InvalidOperationException("❌ ERROR: No se encontró la cadena de conexión 'DefaultConnection'. Verifica las variables de entorno en IIS.");
    }
}
else
{
    Console.WriteLine("✅ Usando cadena de conexión desde variables de entorno");
}

Console.WriteLine($"📊 Cadena de conexión configurada. Longitud: {connectionString.Length} caracteres");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// CONFIGURACIÓN DE HANGFIRE
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

// SERVICIOS SHAREPOINT
builder.Services.Configure<SharePointConfig>(
    builder.Configuration.GetSection("SharePoint"));
builder.Services.AddScoped<ISharePointTestService, SharePointTestService>();

// HEALTH CHECKS
builder.Services.AddHealthChecks()
    .AddCheck<SharePointHealthCheck>("sharepoint");

//SERVICIO DE SELLOS
builder.Services.AddScoped<SellosAuditoriaService>();

// Habilitar soporte para sesiones
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ===== CONFIGURACIÓN DEL PIPELINE =====

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

// CONFIGURACIÓN DE ARCHIVOS ESTÁTICOS MEJORADA
app.UseStaticFiles();

string descargasPath = Path.Combine(app.Environment.WebRootPath, "descargas_masivas");
if (!Directory.Exists(descargasPath))
{
    Directory.CreateDirectory(descargasPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(descargasPath),
    RequestPath = "/descargas_masivas",
    ServeUnknownFileTypes = true
});

app.UseRouting();

// Habilitar middleware de sesiones
app.UseSession();

// 👇 CADENERO GLOBAL DE SESIONES 👇
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // 1. Definimos las rutas públicas que NO requieren iniciar sesión
    bool esRutaPublica = path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/Login", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/RecuperarPassword", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/descargas_masivas", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/img", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                         // 🔥 NUEVAS EXCEPCIONES PARA LA CARTELERA DE TV 🔥
                         path.StartsWith("/Cartelera/Display", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/api/Cartelera/GetActive", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("/api/Cartelera/GetImage", StringComparison.OrdinalIgnoreCase);

    if (!esRutaPublica)
    {
        // 2. Revisamos si el usuario tiene su ID guardado en la sesión
        var idUsuario = context.Session.GetInt32("idUsuario");

        if (idUsuario == null || idUsuario == 0)
        {
            // 3. ¡La sesión expiró o no existe! Lo pateamos al Login inmediatamente
            context.Response.Redirect("/Login");
            return; // Cortamos la ejecución para que no cargue la página no autorizada
        }
    }

    // Si todo está en orden o es una ruta pública, lo dejamos pasar a la página que pidió
    await next();
});
// 👆 FIN DEL CADENERO 👆

app.UseAuthorization();

// PANEL DE CONTROL DE HANGFIRE
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.MapRazorPages();
app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/", context =>
{
    context.Response.Redirect("/Index");
    return Task.CompletedTask;
});

Console.WriteLine("✅ Aplicación iniciada correctamente");

// PROGRAMACIÓN DE LIMPIEZA AUTOMÁTICA
RecurringJob.AddOrUpdate<ProyectoRH2025.BackgroundJobs.ILimpiezaZipsJob>(
    "Limpieza-Diaria-Zips",
    job => job.BorrarZipsAntiguos(),
    Cron.Daily);

app.Run();

// FILTRO DE AUTORIZACIÓN PARA HANGFIRE
public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var idUsuario = httpContext.Session.GetInt32("idUsuario");
        return idUsuario != null && idUsuario > 0;
    }
}