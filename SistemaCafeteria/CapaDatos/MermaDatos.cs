using System;
using System.Collections.Generic;
using CapaEntidades;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    public class MermaDatos
    {
        /// <summary>
        /// Registra una merma en una sola transacción (RF-09, RIU-05):
        /// - Costo ≤ límite: se autoriza automáticamente y se descuenta.
        /// - Costo &gt; límite con <paramref name="idAdminAutoriza"/> (NIP validado en el momento):
        ///   se autoriza a nombre de ese Administrador y se descuenta.
        /// - Costo &gt; límite sin autorización: queda Pendiente y el inventario no se toca.
        /// El descuento es del lote indicado o, si la merma no trae lote, de los lotes del insumo en orden PEPS.
        /// </summary>
        public (int IdMerma, string EstadoFinal) RegistrarMermaOperativa(Merma merma, decimal limiteEconomico, int? idAdminAutoriza = null)
        {
            bool requiereAutorizacion = merma.CostoEstimado > limiteEconomico;
            bool autorizadaConNip = requiereAutorizacion && idAdminAutoriza.HasValue;
            string estadoFinal = requiereAutorizacion && !autorizadaConNip ? "Pendiente" : "Autorizada";

            if (merma.Cantidad <= 0)
                throw new InvalidOperationException("La cantidad de la merma debe ser mayor a cero.");

            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Si la autoriza un Administrador, confirmar que siga activo y con ese rol
                if (autorizadaConNip)
                {
                    const string sqlAdmin = @"
                        SELECT COUNT(*)
                        FROM Usuario u
                        INNER JOIN Rol r ON r.id_rol = u.id_rol
                        WHERE u.id_usuario = @id_admin
                          AND u.estado = 'Activo'
                          AND r.nombre = 'Administrador';";

                    using var cmdAdmin = new MySqlCommand(sqlAdmin, conn, tx);
                    cmdAdmin.Parameters.AddWithValue("@id_admin", idAdminAutoriza!.Value);
                    if (Convert.ToInt32(cmdAdmin.ExecuteScalar()) == 0)
                        throw new InvalidOperationException("Solo un Administrador activo puede autorizar la merma.");
                }

                // 2. Una merma pendiente no descuenta, pero no puede pedir más de lo que existe
                if (estadoFinal == "Pendiente")
                    ValidarExistencia(conn, tx, merma);

                // 3. Insertar registro en Merma (id_lote NULL si no se conoce el lote)
                const string sqlInsertMerma = @"
                    INSERT INTO Merma (
                        id_insumo, id_lote, id_usuario_registra, id_usuario_autoriza,
                        cantidad, motivo, descripcion, fecha_merma, costo_estimado, estado
                    ) VALUES (
                        @id_insumo, @id_lote, @id_usuario_registra, @id_usuario_autoriza,
                        @cantidad, @motivo, @descripcion, @fecha_merma, @costo_estimado, @estado
                    );
                    SELECT LAST_INSERT_ID();";

                int idMerma;
                using (var cmdMerma = new MySqlCommand(sqlInsertMerma, conn, tx))
                {
                    cmdMerma.Parameters.AddWithValue("@id_insumo", merma.IdInsumo);
                    cmdMerma.Parameters.AddWithValue("@id_lote", merma.IdLote.HasValue ? merma.IdLote.Value : DBNull.Value);
                    cmdMerma.Parameters.AddWithValue("@id_usuario_registra", merma.IdUsuarioRegistra);
                    // Automática (≤ límite) o pendiente: sin autorizador. Con NIP: el Administrador que lo capturó.
                    cmdMerma.Parameters.AddWithValue("@id_usuario_autoriza", autorizadaConNip ? idAdminAutoriza!.Value : DBNull.Value);
                    cmdMerma.Parameters.AddWithValue("@cantidad", merma.Cantidad);
                    cmdMerma.Parameters.AddWithValue("@motivo", merma.Motivo);
                    cmdMerma.Parameters.AddWithValue("@descripcion", (object?)merma.Descripcion ?? DBNull.Value);
                    cmdMerma.Parameters.AddWithValue("@fecha_merma", merma.FechaMerma);
                    cmdMerma.Parameters.AddWithValue("@costo_estimado", merma.CostoEstimado);
                    cmdMerma.Parameters.AddWithValue("@estado", estadoFinal);
                    idMerma = Convert.ToInt32(cmdMerma.ExecuteScalar());
                }

                // 4. Si quedó autorizada (automática o con NIP), descontar y registrar en el kardex.
                //    El movimiento queda a nombre de quien autoriza, igual que en la bandeja.
                if (estadoFinal == "Autorizada")
                {
                    string origen = merma.IdLote.HasValue ? "" : " · lote no identificado, PEPS";
                    string texto = autorizadaConNip
                        ? $"Merma autorizada con NIP de administrador ({merma.Motivo}){origen}"
                        : $"Merma autorizada automáticamente ({merma.Motivo}){origen}";

                    DescuentoMerma.Aplicar(conn, tx, idMerma, merma.IdInsumo, merma.IdLote, merma.Cantidad,
                        autorizadaConNip ? idAdminAutoriza!.Value : merma.IdUsuarioRegistra, texto, merma.FechaMerma);
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

        private static void ValidarExistencia(MySqlConnection conn, MySqlTransaction tx, Merma merma)
        {
            string sql = merma.IdLote.HasValue
                ? "SELECT id_insumo, cantidad_disponible FROM Lote WHERE id_lote = @id"
                : "SELECT @id_insumo AS id_insumo, COALESCE(SUM(cantidad_disponible), 0) AS cantidad_disponible FROM Lote WHERE id_insumo = @id";

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", merma.IdLote ?? merma.IdInsumo);
            cmd.Parameters.AddWithValue("@id_insumo", merma.IdInsumo);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException("El lote seleccionado ya no existe en la base de datos.");
            if (Convert.ToInt32(reader["id_insumo"]) != merma.IdInsumo)
                throw new InvalidOperationException("El lote seleccionado no corresponde al insumo de la merma.");

            decimal disponible = Convert.ToDecimal(reader["cantidad_disponible"]);
            if (merma.Cantidad > disponible)
                throw new InvalidOperationException(
                    $"Stock insuficiente {(merma.IdLote.HasValue ? "en el lote" : "del insumo")}. Disponible actual: {disponible:0.###}.");
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
