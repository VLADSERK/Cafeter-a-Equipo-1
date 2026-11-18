using System.Globalization;
using System.Text.RegularExpressions;
using CapaEntidades;

namespace CapaPresentacion.Estilos
{
    /// <summary>
    /// Lectura única de números capturados por el usuario (RNF-01 / RNF-08 / RIU-08).
    /// - Acepta punto o coma como separador decimal: "2.5" y "2,5" valen 2.5.
    /// - Acepta coma de miles junto con punto decimal: "1,250.50" vale 1250.50.
    /// - Rechaza formatos ambiguos como "1,000" (¿mil o uno?) en lugar de adivinar.
    /// - Rechaza negativos y más decimales de los permitidos (3 en cantidades, 2 en importes).
    /// </summary>
    public static class EntradaNumerica
    {
        private static readonly Regex MilesSoloConComa = new(@"^\d{1,3}(,\d{3})+$", RegexOptions.Compiled);

        /// <summary>Cantidades de inventario y recetas: hasta 3 decimales.</summary>
        public static bool TryLeerCantidad(string? texto, out decimal valor, out string error)
            => TryLeer(texto, Redondeo.DecimalesCantidad, "La cantidad", out valor, out error);

        /// <summary>Importes en MXN (precios, costos, montos): hasta 2 decimales.</summary>
        public static bool TryLeerImporte(string? texto, out decimal valor, out string error)
            => TryLeer(texto, Redondeo.DecimalesImporte, "El importe", out valor, out error);

        /// <summary>Versión silenciosa para vistas previas: devuelve 0 si el texto no es válido.</summary>
        public static decimal LeerCantidadOCero(string? texto) => TryLeerCantidad(texto, out decimal v, out _) ? v : 0m;

        /// <summary>Versión silenciosa para vistas previas: devuelve 0 si el texto no es válido.</summary>
        public static decimal LeerImporteOCero(string? texto) => TryLeerImporte(texto, out decimal v, out _) ? v : 0m;

        private static bool TryLeer(string? texto, int maxDecimales, string sujeto, out decimal valor, out string error)
        {
            valor = 0m;
            error = string.Empty;

            string limpio = (texto ?? string.Empty)
                .Replace("$", string.Empty)
                .Replace("MXN", string.Empty, System.StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .Trim();

            if (limpio.Length == 0)
            {
                error = $"{sujeto} es obligatorio.";
                return false;
            }

            bool tienePunto = limpio.Contains('.');
            bool tieneComa = limpio.Contains(',');

            if (tienePunto && tieneComa)
            {
                // Formato "1,250.50": la coma solo puede ir antes del punto decimal (separador de miles)
                if (limpio.LastIndexOf(',') > limpio.IndexOf('.'))
                {
                    error = $"{sujeto} tiene un formato no válido. Usa punto para los decimales (ej. 1250.50).";
                    return false;
                }
                limpio = limpio.Replace(",", string.Empty);
            }
            else if (tieneComa)
            {
                if (MilesSoloConComa.IsMatch(limpio))
                {
                    error = $"{sujeto} \"{texto?.Trim()}\" es ambiguo. Escribe 1000 para mil o 1.000 para uno con decimales.";
                    return false;
                }
                if (limpio.IndexOf(',') != limpio.LastIndexOf(','))
                {
                    error = $"{sujeto} tiene un formato no válido.";
                    return false;
                }
                limpio = limpio.Replace(',', '.');
            }

            if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor))
            {
                error = $"{sujeto} debe ser un número positivo (ej. 2.5).";
                return false;
            }

            if (Redondeo.ExcedeDecimales(valor, maxDecimales))
            {
                error = $"{sujeto} admite como máximo {maxDecimales} decimales.";
                valor = 0m;
                return false;
            }

            return true;
        }
    }
}
