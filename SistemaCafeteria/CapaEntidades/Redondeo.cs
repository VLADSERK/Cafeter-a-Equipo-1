using System;

namespace CapaEntidades
{
    /// <summary>
    /// Reglas de precisión compartidas por todas las capas.
    /// RNF-08: importes en pesos con 2 decimales y redondeo comercial Half-Up
    ///         (MidpointRounding.AwayFromZero). Math.Round por defecto usa el redondeo
    ///         bancario (ToEven), que NO cumple el requisito: 2.345 → 2.34 en vez de 2.35.
    /// RNF-01: cantidades de inventario y recetas con 3 decimales.
    /// </summary>
    public static class Redondeo
    {
        public const int DecimalesImporte = 2;
        public const int DecimalesCantidad = 3;

        /// <summary>Redondea un importe en MXN a 2 decimales (Half-Up).</summary>
        public static decimal Importe(decimal valor) =>
            Math.Round(valor, DecimalesImporte, MidpointRounding.AwayFromZero);

        /// <summary>Redondea una cantidad de inventario o receta a 3 decimales (Half-Up).</summary>
        public static decimal Cantidad(decimal valor) =>
            Math.Round(valor, DecimalesCantidad, MidpointRounding.AwayFromZero);

        /// <summary>Redondeo Half-Up a un número arbitrario de decimales (porcentajes, costos de referencia).</summary>
        public static decimal A(decimal valor, int decimales) =>
            Math.Round(valor, decimales, MidpointRounding.AwayFromZero);

        /// <summary>True si el valor tiene más decimales significativos de los permitidos.</summary>
        public static bool ExcedeDecimales(decimal valor, int decimales) =>
            decimal.Round(valor, decimales, MidpointRounding.AwayFromZero) != valor;
    }
}
