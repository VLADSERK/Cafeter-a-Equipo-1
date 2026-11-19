using System;

namespace CapaEntidades
{
    /// <summary>
    /// Única regla de conversión de unidades (RNF-01): 1 kg = 1 000 g y 1 L = 1 000 ml.
    /// Solo convierte entre unidades del mismo tipo (masa con masa, volumen con volumen);
    /// entre tipos distintos no hay equivalencia y el factor es 1.
    /// La usan el registro de ventas, la configuración de recetas y el cálculo de disponibilidad.
    /// </summary>
    public static class ConversionUnidades
    {
        /// <summary>Equivalencia de la unidad respecto a su unidad base (kg o L).</summary>
        public static decimal FactorBase(string? abreviatura) => (abreviatura ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "g" => 0.001m,
            "kg" => 1m,
            "ml" => 0.001m,
            "l" => 1m,
            _ => 1m
        };

        /// <summary>
        /// Factor para convertir una cantidad expresada en la unidad "desde" a la unidad "hacia"
        /// (por ejemplo g → kg = 0.001). Si los tipos son distintos devuelve 1.
        /// </summary>
        public static decimal Factor(string abreviaturaDesde, string tipoDesde, string abreviaturaHacia, string tipoHacia)
        {
            if (!string.Equals(tipoDesde?.Trim(), tipoHacia?.Trim(), StringComparison.OrdinalIgnoreCase))
                return 1m;

            return FactorBase(abreviaturaDesde) / FactorBase(abreviaturaHacia);
        }
    }
}
