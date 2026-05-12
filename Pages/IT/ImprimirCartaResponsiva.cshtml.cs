using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using ProyectoRH2025.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.IT
{
    public class ImprimirCartaResponsivaModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ImprimirCartaResponsivaModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public CartaResponsivaDTO Carta { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var equipo = await _context.TblInventarioCel.FindAsync(id);
            if (equipo == null) return NotFound();

            // Buscar información relacionada
            var empleado = await _context.Empleados.FirstOrDefaultAsync(e => e.Id == equipo.idempleado);
            var usuario = await _context.TblUsuarios.FirstOrDefaultAsync(u => u.idUsuario == equipo.idUsuario);
            var marca = await _context.TblMarcasCel.FirstOrDefaultAsync(m => m.id == equipo.idMarca);
            var modelo = await _context.TblModelosCel.FirstOrDefaultAsync(m => m.id == equipo.idModelo);
            var precioObj = await _context.TblPreciosCel.FirstOrDefaultAsync(p => p.id == equipo.idPrecio);
            var puestoObj = await _context.PuestoEmpleados.FirstOrDefaultAsync(p => p.id == equipo.idPuesto);
            var sucursalObj = await _context.TblSucursal.FirstOrDefaultAsync(s => s.id == equipo.idSucursal);

            // 👇 NUEVO: Buscamos los accesorios en la base de datos 👇
            var accesoriosObj = await _context.TblInventarioAccesorios.FirstOrDefaultAsync(a => a.idInventario == id);

            decimal precioConvertido = 0;
            if (precioObj != null) decimal.TryParse(precioObj.Precio, out precioConvertido);

            // Mapear al DTO
            Carta = new CartaResponsivaDTO
            {
                FechaEntrega = equipo.Fentrega,
                Marca = marca?.MarcaCel ?? "N/A",
                Modelo = modelo?.Modelo ?? "N/A",
                NumeroTelefono = equipo.NoTelefono,
                IMEI = equipo.IMEI,
                Precio = precioConvertido,
                NombreEmpleado = empleado != null ? $"{empleado.Names} {empleado.Apellido}".Trim() : "N/A",
                NumeroReloj = empleado?.Reloj?.ToString() ?? "N/A",
                Puesto = puestoObj?.Puesto ?? "N/A",
                EntregadoPor = usuario?.NombreCompleto ?? usuario?.UsuarioNombre ?? "SISTEMA",
                Sucursal = sucursalObj?.Sucursal ?? "Nuevo Laredo Tamps.",

                // 👇 NUEVO: Pasamos los comentarios y accesorios a la vista 👇
                Comentarios = equipo.Comentarios,
                Accesorios = accesoriosObj ?? new TblInventarioAccesorios()
            };

            return Page();
        }
    }

    public class CartaResponsivaDTO
    {
        public DateTime FechaEntrega { get; set; }
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public string NumeroTelefono { get; set; }
        public string IMEI { get; set; }
        public decimal Precio { get; set; }
        public string NombreEmpleado { get; set; }
        public string NumeroReloj { get; set; }
        public string Puesto { get; set; }
        public string EntregadoPor { get; set; }
        public string Sucursal { get; set; }

        // 👇 NUEVO: Propiedades para recibir los datos 👇
        public string Comentarios { get; set; }
        public TblInventarioAccesorios Accesorios { get; set; }
    }
}