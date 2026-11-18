using System.Globalization;

namespace CapaPresentacion.Estilos
{
    /// <summary>
    /// Formato único de cantidades de inventario para todas las pantallas (inventario del cajero,
    /// del Administrador, mermas, autorizaciones, kardex y reportes): hasta 3 decimales (RNF-01),
    /// sin redondear a 2 y con separador de miles es-MX. Así un mismo lote muestra el mismo texto en todas.
    /// </summary>
    public static class FormatoInventario
    {
        private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

        public static string Numero(decimal valor) => valor.ToString("#,##0.###", Mx);

        public static string Cantidad(decimal valor, string? unidad)
            => string.IsNullOrWhiteSpace(unidad) ? Numero(valor) : $"{Numero(valor)} {unidad}";
    }
}
