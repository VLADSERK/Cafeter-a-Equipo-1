using System;
using System.Collections.Generic;
using CapaEntidades;
using MySqlConnector;

namespace CapaDatos
{
    public class AutorizacionMermaDatos
    {
        /// <summary>
        /// Lista las mermas que exceden el límite económico y esperan autorización del Administrador.
        /// </summary>
        public List<Merma> ObtenerMermasPendientes()
        {
            const string sql = @"
                SELECT m.id_merma, m.id_insumo, m.id_lote, m.id_usuario_registra,
                       m.cantidad, m.motivo, m.descripcion, m.fecha_merma,
                       m.costo_estimado, m.estado,
                       i.nombre       AS nombre_insumo,
                       um.abreviatura AS unidad_abrev,
                       l.codigo_lote,
                       u.nombre       AS nombre_usuario_registra
                FROM Merma m
                INNER JOIN Insumo       i  ON i.id_insumo  = m.id_insumo
                INNER JOIN UnidadMedida um ON um.id_unidad = i.id_unidad
                INNER JOIN Usuario      u  ON u.id_usuario = m.id_usuario_registra
                LEFT  JOIN Lote         l  ON l.id_lote    = m.id_lote
                WHERE m.estado = 'Pendiente'
                ORDER BY m.fecha_merma ASC;";

            var lista = new List<Merma>();

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            int ordLote = reader.GetOrdinal("id_lote");
            int ordDescripcion = reader.GetOrdinal("descripcion");
            int ordCodigoLote = reader.GetOrdinal("codigo_lote");

            while (reader.Read())
            {
                lista.Add(new Merma
                {
                    IdMerma = reader.GetInt32("id_merma"),
                    IdInsumo = reader.GetInt32("id_insumo"),
                    IdLote = reader.IsDBNull(ordLote) ? null : reader.GetInt32(ordLote),
                    IdUsuarioRegistra = reader.GetInt32("id_usuario_registra"),
                    Cantidad = reader.GetDecimal("cantidad"),
                    Motivo = reader.GetString("motivo"),
                    Descripcion = reader.IsDBNull(ordDescripcion) ? null : reader.GetString(ordDescripcion),
                    FechaMerma = reader.GetDateTime("fecha_merma"),
                    CostoEstimado = reader.GetDecimal("costo_estimado"),
                    Estado = reader.GetString("estado"),
                    NombreInsumo = reader.GetString("nombre_insumo"),
                    UnidadAbreviatura = reader.GetString("unidad_abrev"),
                    CodigoLote = reader.IsDBNull(ordCodigoLote) ? "Varios (PEPS)" : reader.GetString(ordCodigoLote),
                    NombreUsuarioRegistra = reader.GetString("nombre_usuario_registra")
                });
            }

            return lista;
        }

        /// <summary>
        /// Aprueba o rechaza una merma pendiente.
        /// - Aprobar: vuelve a validar stock (FOR UPDATE), descuenta del lote (o de varios en orden PEPS
        ///   si la merma no trae lote) y registra el movimiento MERMA en el kardex.
        /// - Rechazar: solo cambia el estado; el inventario no se toca.
        /// Todo ocurre en una sola transacción.
        /// </summary>
        public (bool Exito, string Mensaje) ResolverMerma(int idMerma, int idAdmin, bool aprobar, string? observaciones)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Validar que quien resuelve sea un Administrador activo
                const string sqlAdmin = @"
                    SELECT COUNT(*)
                    FROM Usuario u
                    INNER JOIN Rol r ON r.id_rol = u.id_rol
                    WHERE u.id_usuario = @id_admin
                      AND u.estado = 'Activo'
                      AND r.nombre = 'Administrador';";

                using (var cmdAdmin = new MySqlCommand(sqlAdmin, conn, tx))
                {
                    cmdAdmin.Parameters.AddWithValue("@id_admin", idAdmin);
                    if (Convert.ToInt32(cmdAdmin.ExecuteScalar()) == 0)
                    {
                        tx.Rollback();
                        return (false, "Solo un Administrador activo puede autorizar o rechazar mermas.");
                    }
                }

                // 2. Bloquear la merma y leer sus datos
                const string sqlMerma = @"
                    SELECT id_insumo, id_lote, cantidad, motivo, estado
                    FROM Merma
                    WHERE id_merma = @id_merma
                    FOR UPDATE;";

                int idInsumo;
                int? idLote;
                decimal cantidad;
                string motivo;
                string estadoActual;

                using (var cmdMerma = new MySqlCommand(sqlMerma, conn, tx))
                {
                    cmdMerma.Parameters.AddWithValue("@id_merma", idMerma);
                    using var reader = cmdMerma.ExecuteReader();

                    if (!reader.Read())
                    {
                        reader.Close();
                        tx.Rollback();
                        return (false, "La merma indicada no existe.");
                    }

                    int ordLote = reader.GetOrdinal("id_lote");
                    idInsumo = reader.GetInt32("id_insumo");
                    idLote = reader.IsDBNull(ordLote) ? null : reader.GetInt32(ordLote);
                    cantidad = reader.GetDecimal("cantidad");
                    motivo = reader.GetString("motivo");
                    estadoActual = reader.GetString("estado");
                }

                if (estadoActual != "Pendiente")
                {
                    tx.Rollback();
                    return (false, $"La merma ya fue {estadoActual.ToLower()} anteriormente.");
                }

                bool hayObservaciones = !string.IsNullOrWhiteSpace(observaciones);

                // 3. RECHAZAR: solo se cambia el estado (y se anota el motivo en la descripción)
                if (!aprobar)
                {
                    const string sqlRechazar = @"
                        UPDATE Merma
                        SET estado = 'Rechazada',
                            id_usuario_autoriza = @id_admin,
                            descripcion = CASE
                                WHEN @obs IS NULL THEN descripcion
                                ELSE LEFT(CONCAT_WS(' | ', descripcion, CONCAT('Rechazo: ', @obs)), 255)
                            END
                        WHERE id_merma = @id_merma;";

                    using (var cmdRechazar = new MySqlCommand(sqlRechazar, conn, tx))
                    {
                        cmdRechazar.Parameters.AddWithValue("@id_admin", idAdmin);
                        cmdRechazar.Parameters.AddWithValue("@obs", hayObservaciones ? (object)observaciones!.Trim() : DBNull.Value);
                        cmdRechazar.Parameters.AddWithValue("@id_merma", idMerma);
                        cmdRechazar.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return (true, "Merma rechazada. El inventario no se modificó.");
                }

                // 4. APROBAR: bloquear los lotes de los que se va a descontar
                var lotes = new List<(int IdLote, decimal Disponible)>();

                string sqlLotes = idLote.HasValue
                    ? @"SELECT id_lote, cantidad_disponible
                        FROM Lote
                        WHERE id_lote = @id_lote
                        FOR UPDATE;"
                    : @"SELECT id_lote, cantidad_disponible
                        FROM Lote
                        WHERE id_insumo = @id_insumo
                          AND cantidad_disponible > 0
                        ORDER BY fecha_caducidad IS NULL, fecha_caducidad, fecha_recepcion, id_lote
                        FOR UPDATE;";

                using (var cmdLotes = new MySqlCommand(sqlLotes, conn, tx))
                {
                    if (idLote.HasValue)
                        cmdLotes.Parameters.AddWithValue("@id_lote", idLote.Value);
                    else
                        cmdLotes.Parameters.AddWithValue("@id_insumo", idInsumo);

                    using var reader = cmdLotes.ExecuteReader();
                    while (reader.Read())
                        lotes.Add((reader.GetInt32("id_lote"), reader.GetDecimal("cantidad_disponible")));
                }

                decimal totalDisponible = 0;
                foreach (var l in lotes)
                    totalDisponible += l.Disponible;

                if (totalDisponible < cantidad)
                {
                    tx.Rollback();
                    return (false, $"Stock insuficiente para autorizar la merma. Disponible actual: {totalDisponible:N2}, requerido: {cantidad:N2}.");
                }

                // 5. Descontar de cada lote y registrar el movimiento MERMA en el kardex
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

                string textoMov = hayObservaciones
                    ? $"Merma autorizada por administrador ({motivo}): {observaciones!.Trim()}"
                    : $"Merma autorizada por administrador ({motivo})";
                if (textoMov.Length > 255)
                    textoMov = textoMov.Substring(0, 255);

                DateTime ahora = DateTime.Now;
                decimal restante = cantidad;

                foreach (var lote in lotes)
                {
                    if (restante <= 0) break;

                    decimal aDescontar = Math.Min(restante, lote.Disponible);
                    if (aDescontar <= 0) continue;

                    using (var cmdLote = new MySqlCommand(sqlDescontar, conn, tx))
                    {
                        cmdLote.Parameters.AddWithValue("@cantidad", aDescontar);
                        cmdLote.Parameters.AddWithValue("@id_lote", lote.IdLote);
                        cmdLote.ExecuteNonQuery();
                    }

                    using (var cmdMov = new MySqlCommand(sqlMov, conn, tx))
                    {
                        cmdMov.Parameters.AddWithValue("@id_lote", lote.IdLote);
                        cmdMov.Parameters.AddWithValue("@id_usuario", idAdmin);
                        cmdMov.Parameters.AddWithValue("@id_merma", idMerma);
                        cmdMov.Parameters.AddWithValue("@cantidad", aDescontar);
                        cmdMov.Parameters.AddWithValue("@fecha_movimiento", ahora);
                        cmdMov.Parameters.AddWithValue("@observaciones", textoMov);
                        cmdMov.ExecuteNonQuery();
                    }

                    restante -= aDescontar;
                }

                // 6. Marcar la merma como autorizada
                const string sqlAutorizar = @"
                    UPDATE Merma
                    SET estado = 'Autorizada',
                        id_usuario_autoriza = @id_admin
                    WHERE id_merma = @id_merma;";

                using (var cmdAut = new MySqlCommand(sqlAutorizar, conn, tx))
                {
                    cmdAut.Parameters.AddWithValue("@id_admin", idAdmin);
                    cmdAut.Parameters.AddWithValue("@id_merma", idMerma);
                    cmdAut.ExecuteNonQuery();
                }

                tx.Commit();
                return (true, "Merma autorizada y descontada del inventario.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}