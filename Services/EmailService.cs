using System.Net;
using System.Net.Mail;

namespace ProyectoRH2025.Services
{
    // 1. Definimos la interfaz para poder inyectarla en cualquier parte del proyecto
    public interface IEmailService
    {
        Task EnviarCorreoAsync(string destinatario, string asunto, string mensajeHtml);
    }

    // 2. Implementamos el servicio
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarCorreoAsync(string destinatario, string asunto, string mensajeHtml)
        {
            try
            {
                // ASP.NET Core leerá esto inteligentemente de tu appsettings, secretos, variables de entorno o web.config
                string? host = _configuration["EmailSettings:Host"];
                string? user = _configuration["EmailSettings:User"];
                string? pass = _configuration["EmailSettings:Password"];

                // FORMA SEGURA: Evita el FormatException si el puerto está vacío ("") en la configuración
                if (!int.TryParse(_configuration["EmailSettings:Port"], out int port))
                {
                    port = 587; // Puerto por defecto
                }

                // FORMA SEGURA: Evita el FormatException si el SSL está vacío o mal escrito
                if (!bool.TryParse(_configuration["EmailSettings:EnableSsl"], out bool enableSsl))
                {
                    enableSsl = true; // Por defecto true
                }

                // Validación rápida de seguridad
                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                {
                    _logger.LogError("Faltan credenciales o configuración del servidor SMTP para enviar correos.");
                    throw new InvalidOperationException("Configuración de correo incompleta.");
                }

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(user, pass),
                    EnableSsl = enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(user, "Sistema de Liquidaciones"),
                    Subject = asunto,
                    Body = mensajeHtml,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(destinatario);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Correo enviado exitosamente a {Destinatario}", destinatario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico al intentar enviar correo a {Destinatario}", destinatario);
                // Es importante lanzar la excepción para que, si falla, Hangfire lo marque como "Fallido" 
                // y lo intente de nuevo automáticamente más tarde.
                throw;
            }
        }
    }
}