using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace ProyectoRH2025.Services
{
    public class QRCodeService
    {
        /// <summary>
        /// Genera un código QR y lo devuelve como base64 para mostrar en HTML
        /// </summary>
        public string GenerarQRBase64(string contenido)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
                using (QRCode qrCode = new QRCode(qrCodeData))
                {
                    using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            qrCodeImage.Save(ms, ImageFormat.Png);
                            byte[] byteImage = ms.ToArray();
                            return Convert.ToBase64String(byteImage);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Genera un código único para la asignación
        /// </summary>
        public string GenerarCodigoUnico(int idAsignacion, string numeroSello, int idOperador)
        {
            // Formato: ASIG-{IdAsignacion}-SELLO-{NumeroSello}-OP-{IdOperador}-{Timestamp}
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            return $"ASIG-{idAsignacion}-SELLO-{numeroSello}-OP-{idOperador}-{timestamp}";
        }

        /// <summary>
        /// Valida si un código QR es válido y extrae la información
        /// </summary>
        public (bool esValido, int idAsignacion, string numeroSello, int idOperador) ValidarQR(string codigoQR)
        {
            try
            {
                // Formato esperado: ASIG-{IdAsignacion}-SELLO-{NumeroSello}-OP-{IdOperador}-{Timestamp}
                var partes = codigoQR.Split('-');

                if (partes.Length < 7 || partes[0] != "ASIG" || partes[2] != "SELLO" || partes[4] != "OP")
                {
                    return (false, 0, "", 0);
                }

                int idAsignacion = int.Parse(partes[1]);
                string numeroSello = partes[3];
                int idOperador = int.Parse(partes[5]);

                return (true, idAsignacion, numeroSello, idOperador);
            }
            catch
            {
                return (false, 0, "", 0);
            }
        }
    }
}