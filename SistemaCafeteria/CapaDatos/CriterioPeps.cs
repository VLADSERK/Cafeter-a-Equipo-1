namespace CapaDatos
{
    /// <summary>
    /// Único lugar donde se define el orden PEPS (RF-02):
    /// primero el lote con la fecha y hora de recepción más antigua.
    /// id_lote desempata lotes recibidos en el mismo instante (orden de captura).
    /// La fecha de caducidad NO interviene en el orden de consumo.
    /// </summary>
    internal static class CriterioPeps
    {
        /// <summary>
        /// Columnas del ORDER BY PEPS. Usa el alias de la tabla Lote si la consulta lo tiene
        /// (por ejemplo "l"); sin alias si la consulta es sobre Lote directamente.
        /// </summary>
        public static string Orden(string? alias = null)
        {
            string p = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim() + ".";
            return $"{p}fecha_recepcion ASC, {p}id_lote ASC";
        }
    }
}
