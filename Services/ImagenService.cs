using System.Drawing;
using System.Drawing.Imaging;

namespace ProyectoRH2025.Services
{
    public class ImagenService
    {
        public (string imagenBase64, string thumbnailBase64, int tamanoOriginal, int tamanoComprimido)
            ProcesarImagen(IFormFile archivo)
        {
            using (var stream = archivo.OpenReadStream())
            using (var imagen = Image.FromStream(stream))
            {
                int tamanoOriginal = (int)(archivo.Length / 1024);
                var imagenComprimida = RedimensionarImagen(imagen, 1920);
                string imagenBase64 = ConvertirABase64(imagenComprimida, 80);
                var thumbnail = GenerarThumbnail(imagen, 200);
                string thumbnailBase64 = ConvertirABase64(thumbnail, 70);
                int tamanoComprimido = (imagenBase64.Length * 3 / 4) / 1024;
                return (imagenBase64, thumbnailBase64, tamanoOriginal, tamanoComprimido);
            }
        }

        private Image RedimensionarImagen(Image imagenOriginal, int maxAncho)
        {
            int nuevoAncho = imagenOriginal.Width;
            int nuevoAlto = imagenOriginal.Height;

            if (nuevoAncho > maxAncho)
            {
                double ratio = (double)maxAncho / nuevoAncho;
                nuevoAncho = maxAncho;
                nuevoAlto = (int)(nuevoAlto * ratio);
            }

            var bitmap = new Bitmap(nuevoAncho, nuevoAlto);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(imagenOriginal, 0, 0, nuevoAncho, nuevoAlto);
            }

            return bitmap;
        }

        private Image GenerarThumbnail(Image imagenOriginal, int tamaño)
        {
            int dimensionMenor = Math.Min(imagenOriginal.Width, imagenOriginal.Height);
            int x = (imagenOriginal.Width - dimensionMenor) / 2;
            int y = (imagenOriginal.Height - dimensionMenor) / 2;
            var rectanguloRecorte = new Rectangle(x, y, dimensionMenor, dimensionMenor);

            var thumbnail = new Bitmap(tamaño, tamaño);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(imagenOriginal, new Rectangle(0, 0, tamaño, tamaño), rectanguloRecorte, GraphicsUnit.Pixel);
            }

            return thumbnail;
        }

        private string ConvertirABase64(Image imagen, long calidad)
        {
            using (var ms = new MemoryStream())
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, calidad);
                var jpegCodec = GetEncoderInfo("image/jpeg");
                imagen.Save(ms, jpegCodec, encoderParams);
                byte[] imageBytes = ms.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
        }

        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                    return codec;
            }
            return null;
        }

        public (string pdfBase64, int tamanoKB) ProcesarPDF(IFormFile archivo)
        {
            using (var ms = new MemoryStream())
            {
                archivo.CopyTo(ms);
                byte[] pdfBytes = ms.ToArray();
                string pdfBase64 = Convert.ToBase64String(pdfBytes);
                int tamanoKB = (int)(archivo.Length / 1024);
                return (pdfBase64, tamanoKB);
            }
        }

        // ✅ MÉTODO QUE FALTABA
        public bool EsImagenValida(IFormFile archivo)
        {
            try
            {
                var extension = Path.GetExtension(archivo.FileName).ToLower();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                    return false;

                using (var stream = archivo.OpenReadStream())
                using (var imagen = Image.FromStream(stream))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}