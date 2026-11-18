using System.Globalization;
using System.Linq;
using System.Text;

namespace CapaPresentacion.Estilos
{
    /// <summary>
    /// Búsqueda de texto de las barras de búsqueda (RIU-10): sin distinguir mayúsculas ni acentos,
    /// para que "cafe" encuentre "Café en Grano" y "sofia" encuentre "Sofía Ramírez".
    /// </summary>
    public static class Busqueda
    {
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            var sb = new StringBuilder(texto.Length);
            foreach (char c in texto.Trim().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>true si la consulta (ya normalizada) está vacía o aparece en alguno de los campos.</summary>
        public static bool Coincide(string consultaNormalizada, params string?[] campos)
            => consultaNormalizada.Length == 0 || campos.Any(c => Normalizar(c).Contains(consultaNormalizada));
    }
}
