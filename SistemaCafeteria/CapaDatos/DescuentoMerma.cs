using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    /// <summary>
    /// Descuento de una merma autorizada (RF-09, RNF-02, RNF-05). Se usa dentro de la transacción
    /// de quien llama: registro con autorización automática o con NIP, y autorización desde la bandeja.
    /// - Con lote: descuenta de ese lote.
    /// - Sin lote ("cuando se conozca", RIU-05): descuenta de los lotes del insumo en orden PEPS (RF-02).
    /// Bloquea los lotes con FOR UPDATE y registra un movimiento MERMA por cada lote afectado.
    /// </summary>
    internal static class DescuentoMerma
    {
        /// <exception cref="InvalidOperationException">Si el lote no corresponde o no alcanza la existencia.</exception>
        internal static List<(int IdLote, decimal Cantidad)> Aplicar(
            MySqlConnection conn,
            MySqlTransaction tx,
            int idMerma,
            int idInsumo,
            int? idLote,
            decimal cantidad,
            int idUsuario,
            string observaciones,
            DateTime fecha)
        {
            var lotes = new List<(int IdLote, int IdInsumo, decimal Disponible)>();

            string sqlLotes = idLote.HasValue
                ? @"SELECT id_lote, id_insumo, cantidad_disponible
                    FROM Lote
                    WHERE id_lote = @id_lote
                    FOR UPDATE;"
                : $@"SELECT id_lote, id_insumo, cantidad_disponible
                    FROM Lote
                    WHERE id_insumo = @id_insumo
                      AND cantidad_disponible > 0
                    ORDER BY {CriterioPeps.Orden()}
                    FOR UPDATE;";

            using (var cmd = new MySqlCommand(sqlLotes, conn, tx))
            {
                if (idLote.HasValue)
                    cmd.Parameters.AddWithValue("@id_lote", idLote.Value);
                else
                    cmd.Parameters.AddWithValue("@id_insumo", idInsumo);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lotes.Add((reader.GetInt32("id_lote"), reader.GetInt32("id_insumo"), reader.GetDecimal("cantidad_disponible")));
            }

            if (idLote.HasValue && lotes.Count == 0)
                throw new InvalidOperationException("El lote seleccionado ya no existe en la base de datos.");

            if (lotes.Any(l => l.IdInsumo != idInsumo))
                throw new InvalidOperationException("El lote seleccionado no corresponde al insumo de la merma.");

            decimal totalDisponible = lotes.Sum(l => l.Disponible);
            if (totalDisponible < cantidad)
            {
                string donde = idLote.HasValue ? "en el lote" : "del insumo";
                throw new InvalidOperationException(
                    $"Stock insuficiente {donde}. Disponible actual: {totalDisponible:0.###}, requerido: {cantidad:0.###}.");
            }

            const string sqlDescontar = @"
                UPDATE Lote
                SET cantidad_disponible = cantidad_disponible - @cantidad
                WHERE id_lote = @id_lote;";

            const string sqlMov = @"
                INSERT INTO MovimientoInventario (
                    id_lote, id_usuario, id_merma, tipo, cantidad, fecha_movimiento, observaciones
                ) VALUES (
                    @id_lote, @id_usuario, @id_merma, 'MERMA', @cantidad, @fecha_movimiento, @observaciones
                );";

            string texto = observaciones.Length > 255 ? observaciones.Substring(0, 255) : observaciones;
            var aplicados = new List<(int IdLote, decimal Cantidad)>();
            decimal restante = cantidad;

            foreach (var lote in lotes)
            {
                if (restante <= 0) break;

                decimal tomar = Math.Min(restante, lote.Disponible);
                if (tomar <= 0) continue;

                using (var cmdLote = new MySqlCommand(sqlDescontar, conn, tx))
                {
                    cmdLote.Parameters.AddWithValue("@cantidad", tomar);
                    cmdLote.Parameters.AddWithValue("@id_lote", lote.IdLote);
                    cmdLote.ExecuteNonQuery();
                }

                using (var cmdMov = new MySqlCommand(sqlMov, conn, tx))
                {
                    cmdMov.Parameters.AddWithValue("@id_lote", lote.IdLote);
                    cmdMov.Parameters.AddWithValue("@id_usuario", idUsuario);
                    cmdMov.Parameters.AddWithValue("@id_merma", idMerma);
                    cmdMov.Parameters.AddWithValue("@cantidad", tomar);
                    cmdMov.Parameters.AddWithValue("@fecha_movimiento", fecha);
                    cmdMov.Parameters.AddWithValue("@observaciones", texto);
                    cmdMov.ExecuteNonQuery();
                }

                aplicados.Add((lote.IdLote, tomar));
                restante -= tomar;
            }

            return aplicados;
        }
    }
}
