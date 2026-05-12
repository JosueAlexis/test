using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProyectoRH2025.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoRH2025.Pages.IT
{
    public class InventarioCelularesModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public InventarioCelularesModel(ApplicationDbContext context) { _context = context; }

        public List<EquipoAsignadoVista> Equipos { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Unimos las tablas usando LINQ para traer la información legible
            // Unimos las tablas usando LINQ
            var query = from inv in _context.TblInventarioCel
                        join emp in _context.Empleados on inv.idempleado equals emp.Id
                        join marca in _context.TblMarcasCel on inv.idMarca equals marca.id
                        join modelo in _context.TblModelosCel on inv.idModelo equals modelo.id
                        join est in _context.TblEstatusCelular on inv.idEstatus equals est.idEstatus 
                        join suc in _context.TblSucursal on inv.idSucursal equals suc.id into sucGroup
                        from sucursal in sucGroup.DefaultIfEmpty()
                        orderby inv.Fentrega descending
                        select new EquipoAsignadoVista
                        {
                            Id = inv.id,
                            Reloj = emp.Reloj,
                            Empleado = emp.Names + " " + emp.Apellido,
                            Telefono = inv.NoTelefono,
                            MarcaModelo = marca.MarcaCel + " " + modelo.Modelo,
                            IMEI = inv.IMEI,
                            FechaAsignacion = inv.Fentrega,
                            Sucursal = sucursal != null ? sucursal.Sucursal : "N/A",
                            Estatus = est.Descripcion
                        };

            Equipos = await query.ToListAsync();
        }

        public class EquipoAsignadoVista
        {
            public int Id { get; set; }

            // 👇 CORRECCIÓN: Cambiamos de string a int? para que coincida con la BD
            public int? Reloj { get; set; }

            public string Empleado { get; set; }
            public string Telefono { get; set; }
            public string MarcaModelo { get; set; }
            public string IMEI { get; set; }
            public DateTime FechaAsignacion { get; set; }
            public string Sucursal { get; set; }
            public string Estatus { get; set; }
        }
    }
}