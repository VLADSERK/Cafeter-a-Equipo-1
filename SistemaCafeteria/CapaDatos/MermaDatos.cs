using System;
using System.Collections.Generic;
using CapaEntidades;
using MySqlConnector;

namespace CapaDatos
{
    public class MermaDatos
    {
        public (int IdMerma, string EstadoFinal) RegistrarMermaOperativa(Merma merma, decimal limiteEconomico)
        {
            bool requiereAutorizacion = merma.CostoEstimado > limiteEconomico;
            string estadoFinal = requiereAutorizacion ? "Pendiente" : "Autorizada";

            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Validaciones básicas
                if (!merma.IdLote.HasValue)
                    throw new InvalidOperationException("Debe seleccionar el lote del que se registrará la merma.");

                if (merma.Cantidad <= 0)
                    throw new InvalidOperationException("La cantidad de la merma debe ser mayor a cero.");

                // 2. Verificar saldo real del lote en BD (bloqueo pesimista FOR UPDATE)
                const string sqlCheckLote = @"
                    SELECT cantidad_disponible, costo_unitario, id_insumo
                    FROM Lote
                    WHERE id_lote = @id_lote
                    FOR UPDATE;";

                decimal disponibleActual;
                using (var cmdCheck = new MySqlCommand(sqlCheckLote, conn, tx))
                {
                    cmdCheck.Parameters.AddWithValue("@id_lote", merma.IdLote.Value);
                    using var reader = cmdCheck.ExecuteReader();
                    if (!reader.Read())
                        throw new InvalidOperationException("El lote seleccionado ya no existe en la base de datos.");

                    if (reader.GetInt32("id_insumo") != merma.IdInsumo)
                        throw new InvalidOperationException("El lote seleccionado no corresponde al insumo de la merma.");

                    disponibleActual = reader.GetDecimal("cantidad_disponible");
                }

                if (merma.Cantidad > disponibleActual)
                    throw new InvalidOperationException($"Stock insuficiente en el lote. Disponible actual: {disponibleActual:N2}.");

                // 3. Insertar registro en Merma
                const string sqlInsertMerma = @"
                    INSERT INTO Merma (
                        id_insumo, id_lote, id_usuario_registra, id_usuario_autoriza,
                        cantidad, motivo, descripcion, fecha_merma, costo_estimado, estado
                    ) VALUES (
                        @id_insumo, @id_lote, @id_usuario_registra, NULL,
                        @cantidad, @motivo, @descripcion, @fecha_merma, @costo_estimado, @estado
                    );
                    SELECT LAST_INSERT_ID();";

                int idMerma;
                using (var cmdMerma = new MySqlCommand(sqlInsertMerma, conn, tx))
                {
                    cmdMerma.Parameters.AddWithValue("@id_insumo", merma.IdInsumo);
                    cmdMerma.Parameters.AddWithValue("@id_lote", merma.IdLote.Value);
                    cmdMerma.Parameters.AddWithValue("@id_usuario_registra", merma.IdUsuarioRegistra);
                    cmdMerma.Parameters.AddWithValue("@cantidad", merma.Cantidad);
                    cmdMerma.Parameters.AddWithValue("@motivo", merma.Motivo);
                    cmdMerma.Parameters.AddWithValue("@descripcion", (object?)merma.Descripcion ?? DBNull.Value);
                    cmdMerma.Parameters.AddWithValue("@fecha_merma", merma.FechaMerma);
                    cmdMerma.Parameters.AddWithValue("@costo_estimado", merma.CostoEstimado);
                    cmdMerma.Parameters.AddWithValue("@estado", estadoFinal);
                    idMerma = Convert.ToInt32(cmdMerma.ExecuteScalar());
                }

                // 4. Si NO excede el límite económico, descontar inmediatamente del lote y registrar en Kardex
                if (!requiereAutorizacion)
                {
                    const string sqlDescontarLote = @"
                        UPDATE Lote
                        SET cantidad_disponible = cantidad_disponible - @cantidad
                        WHERE id_lote = @id_lote;";

                    using (var cmdLote = new MySqlCommand(sqlDescontarLote, conn, tx))
                    {
                        cmdLote.Parameters.AddWithValue("@cantidad", merma.Cantidad);
                        cmdLote.Parameters.AddWithValue("@id_lote", merma.IdLote.Value);
                        cmdLote.ExecuteNonQuery();
                    }

                    const string sqlMov = @"
                        INSERT INTO MovimientoInventario (
                            id_lote, id_usuario, id_merma, tipo, cantidad, fecha_movimiento, observaciones
                        ) VALUES (
                            @id_lote, @id_usuario, @id_merma, 'MERMA', @cantidad, @fecha_movimiento, @observaciones
                        );";

                    using (var cmdMov = new MySqlCommand(sqlMov, conn, tx))
                    {
                        cmdMov.Parameters.AddWithValue("@id_lote", merma.IdLote.Value);
                        cmdMov.Parameters.AddWithValue("@id_usuario", merma.IdUsuarioRegistra);
                        cmdMov.Parameters.AddWithValue("@id_merma", idMerma);
                        cmdMov.Parameters.AddWithValue("@cantidad", merma.Cantidad);
                        cmdMov.Parameters.AddWithValue("@fecha_movimiento", merma.FechaMerma);
                        cmdMov.Parameters.AddWithValue("@observaciones", $"Merma autorizada automáticamente ({merma.Motivo})");
                        cmdMov.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return (idMerma, estadoFinal);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public int ContarMermasHoyPorUsuario(int idUsuario)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM Merma
                WHERE id_usuario_registra = @id_usuario
                  AND DATE(fecha_merma) = CURDATE();";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_usuario", idUsuario);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}