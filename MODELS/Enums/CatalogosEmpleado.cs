namespace ProyectoRH2025.Models.Enums
{
    public enum EstadoCivil : byte
    {
        Soltero = 1,
        Casado = 2,
        Divorciado = 3,
        Viudo = 4,
        Otro = 5
    }

    public enum Escolaridad : byte
    {
        Primaria = 1,
        Secundaria = 2,
        Preparatoria = 3,
        TecnicoBachillerato = 4,
        Licenciatura = 5,
        Postgrado = 6
    }

    public enum NivelIngles : byte
    {
        Ninguno = 0,
        Basico = 1,
        Intermedio = 2,
        Avanzado = 3,
        Nativo = 4
    }

    public enum FuenteReclutamiento : byte
    {
        RecomendacionEmpleado = 1,
        InternetRedesSociales = 2,
        FeriaEmpleo = 3,
        AgenciaReclutamiento = 4,
        Otro = 5
    }

    public enum TipoVivienda : byte
    {
        Propia = 1,
        Infonavit = 2,
        Renta = 3,
        Familiar = 4
    }

    public enum TipoDomicilio : byte
    {
        Actual = 1,
        Anterior1 = 2,
        Anterior2 = 3
    }
}