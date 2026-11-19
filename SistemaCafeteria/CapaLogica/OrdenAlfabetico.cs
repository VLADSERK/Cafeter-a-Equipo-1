using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapaLogica
{
    /// <summary>
    /// Orden alfabético único para los listados que ve el usuario (productos, insumos, categorías).
    /// Usa la cultura es-MX sin distinguir mayúsculas ni acentos, para que "Azúcar" quede junto a
    /// "Azul" y la "Ñ" vaya después de la "N", igual en todas las pantallas.
    /// </summary>
    public static class OrdenAlfabetico
    {
        private static readonly CompareInfo Comparacion = CultureInfo.GetCultureInfo("es-MX").CompareInfo;
        private const CompareOptions Opciones = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        /// <summary>Comparador de nombres (es-MX, sin mayúsculas ni acentos).</summary>
        public static readonly IComparer<string?> Nombres =
            Comparer<string?>.Create((a, b) => Comparacion.Compare(a?.Trim() ?? string.Empty, b?.Trim() ?? string.Empty, Opciones));

        /// <summary>Ordena una lista por el nombre que devuelva <paramref name="nombre"/>.</summary>
        public static List<T> Por<T>(IEnumerable<T> elementos, Func<T, string?> nombre)
            => elementos.OrderBy(nombre, Nombres).ToList();
    }
}
