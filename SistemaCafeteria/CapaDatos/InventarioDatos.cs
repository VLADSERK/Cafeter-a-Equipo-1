using System;
using System.Collections.Generic;
using CapaEntidades;
using MySqlConnector;

namespace CapaDatos
{
    public class InventarioDatos
    {
        public List<UnidadMedida> ObtenerUnidadesMedida()
        {
            var lista = new List<UnidadMedida>();
            const string sql = "SELECT id_unidad, nombre, abreviatura, tipo FROM UnidadMedida ORDER BY id_unidad ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new UnidadMedida
                {
                    IdUnidad = reader.GetInt32("id_unidad"),
                    Nombre = reader.GetString("nombre"),
                    Abreviatura = reader.GetString("abreviatura"),
                    Tipo = reader.GetString("tipo")
                });
            }
            return lista;
        }

        public List<Proveedor> ObtenerProveedoresActivos()
        {
            var lista = new List<Proveedor>();
            const string sql = @"
                SELECT id_proveedor, nombre, telefono, correo, direccion, estado
                FROM Proveedor
                WHERE estado = TRUE
                ORDER BY nombre ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new Proveedor
                {
                    IdProveedor = reader.GetInt32("id_proveedor"),
                    Nombre = reader.GetString("nombre"),
                    Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString("telefono"),
                    Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString("correo"),
                    Direccion = reader.IsDBNull(reader.GetOrdinal("direccion")) ? null : reader.GetString("direccion"),
                    Estado = reader.GetBoolean("estado")
                });
            }
            return lista;
        }

        public int InsertarProveedor(Proveedor proveedor)
        {
            const string sql = @"
                INSERT INTO Proveedor (nombre, telefono, correo, direccion, estado)
                VALUES (@nombre, @telefono, @correo, @direccion, TRUE);
                SELECT LAST_INSERT_ID();";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@nombre", proveedor.Nombre.Trim());
            cmd.Parameters.AddWithValue("@telefono", (object?)proveedor.Telefono ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@correo", (object?)proveedor.Correo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@direccion", (object?)proveedor.Direccion ?? DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<Insumo> ObtenerInsumosConStock()
        {
            var lista = new List<Insumo>();
            const string sql = @"
        SELECT i.id_insumo,
               i.id_unidad,
               i.categoria,
               i.nombre,
               i.stock_minimo,
               i.stock_maximo,
               i.estado,
               u.abreviatura AS unidad_abrev,
               COALESCE(SUM(l.cantidad_disponible), 0) AS stock_actual
        FROM Insumo i
        INNER JOIN UnidadMedida u ON i.id_unidad = u.id_unidad
        LEFT JOIN Lote l ON i.id_insumo = l.id_insumo AND l.cantidad_disponible > 0
        WHERE i.estado = TRUE
        GROUP BY i.id_insumo, i.id_unidad, i.categoria, i.nombre, i.stock_minimo, i.stock_maximo, i.estado, u.abreviatura
        ORDER BY i.nombre ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new Insumo
                {
                    IdInsumo = reader.GetInt32("id_insumo"),
                    IdUnidad = reader.GetInt32("id_unidad"),
                    Categoria = reader.IsDBNull(reader.GetOrdinal("categoria")) ? "General" : reader.GetString("categoria"),
                    Nombre = reader.GetString("nombre"),
                    StockMinimo = reader.GetDecimal("stock_minimo"),
                    StockMaximo = reader.GetDecimal("stock_maximo"),
                    Estado = reader.GetBoolean("estado"),
                    UnidadAbreviatura = reader.GetString("unidad_abrev"),
                    StockActualTotal = reader.GetDecimal("stock_actual")
                });
            }
            return lista;
        }

        public int InsertarInsumo(Insumo insumo)
        {
            const string sql = @"
        INSERT INTO Insumo (id_unidad, categoria, nombre, stock_minimo, stock_maximo, estado)
        VALUES (@id_unidad, @categoria, @nombre, @stock_minimo, @stock_maximo, TRUE);
        SELECT LAST_INSERT_ID();";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_unidad", insumo.IdUnidad);
            cmd.Parameters.AddWithValue("@categoria", string.IsNullOrWhiteSpace(insumo.Categoria) ? "General" : insumo.Categoria.Trim());
            cmd.Parameters.AddWithValue("@nombre", insumo.Nombre.Trim());
            cmd.Parameters.AddWithValue("@stock_minimo", insumo.StockMinimo > 0 ? insumo.StockMinimo : 1.000m);
            cmd.Parameters.AddWithValue("@stock_maximo", insumo.StockMaximo);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// Devuelve todos los lotes ordenados bajo criterio PEPS (caducidad más próxima primero, luego fecha de recepción).
        /// </summary>
        public List<Lote> ObtenerLotesPeps(bool soloConDisponibilidad = true)
        {
            var lista = new List<Lote>();
            string filtroDisp = soloConDisponibilidad ? "WHERE l.cantidad_disponible > 0" : "";

            string sql = $@"
                SELECT l.id_lote,
                       l.id_recepcion,
                       l.id_insumo,
                       l.codigo_lote,
                       l.codigo_barras,
                       l.fecha_recepcion,
                       l.fecha_caducidad,
                       l.cantidad_inicial,
                       l.cantidad_disponible,
                       l.costo_unitario,
                       i.nombre AS nombre_insumo,
                       i.stock_maximo,
                       u.abreviatura AS unidad_abrev,
                       p.nombre AS nombre_proveedor
                FROM Lote l
                INNER JOIN Insumo i ON l.id_insumo = i.id_insumo
                INNER JOIN UnidadMedida u ON i.id_unidad = u.id_unidad
                INNER JOIN Recepcion r ON l.id_recepcion = r.id_recepcion
                INNER JOIN Proveedor p ON r.id_proveedor = p.id_proveedor
                {filtroDisp}
                ORDER BY l.id_insumo ASC,
                         CASE WHEN l.fecha_caducidad IS NULL THEN 1 ELSE 0 END ASC,
                         l.fecha_caducidad ASC,
                         l.fecha_recepcion ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new Lote
                {
                    IdLote = reader.GetInt32("id_lote"),
                    IdRecepcion = reader.GetInt32("id_recepcion"),
                    IdInsumo = reader.GetInt32("id_insumo"),
                    CodigoLote = reader.GetString("codigo_lote"),
                    CodigoBarras = reader.IsDBNull(reader.GetOrdinal("codigo_barras")) ? string.Empty : reader.GetString("codigo_barras"),
                    FechaRecepcion = reader.GetDateTime("fecha_recepcion"),
                    FechaCaducidad = reader.IsDBNull(reader.GetOrdinal("fecha_caducidad"))
                        ? null
                        : reader.GetDateTime("fecha_caducidad"),
                    CantidadInicial = reader.GetDecimal("cantidad_inicial"),
                    CantidadDisponible = reader.GetDecimal("cantidad_disponible"),
                    CostoUnitario = reader.GetDecimal("costo_unitario"),
                    NombreInsumo = reader.GetString("nombre_insumo"),
                    StockMaximoInsumo = reader.GetDecimal("stock_maximo"),
                    UnidadAbreviatura = reader.GetString("unidad_abrev"),
                    NombreProveedor = reader.GetString("nombre_proveedor")
                });
            }
            return lista;
        }

        /// <summary>
        /// Transacción atómica: registra Recepcion, crea Lote y anota MovimientoInventario (ENTREGA).
        /// </summary>
        public int RegistrarRecepcionConLote(Recepcion recepcion, Lote nuevoLote)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Insertar cabecera de Recepción
                const string sqlRecepcion = @"
                    INSERT INTO Recepcion (id_proveedor, id_usuario, fecha_recepcion, observaciones)
                    VALUES (@id_proveedor, @id_usuario, @fecha_recepcion, @observaciones);
                    SELECT LAST_INSERT_ID();";

                int idRecepcion;
                using (var cmdRec = new MySqlCommand(sqlRecepcion, conn, tx))
                {
                    cmdRec.Parameters.AddWithValue("@id_proveedor", recepcion.IdProveedor);
                    cmdRec.Parameters.AddWithValue("@id_usuario", recepcion.IdUsuario);
                    cmdRec.Parameters.AddWithValue("@fecha_recepcion", recepcion.FechaRecepcion);
                    cmdRec.Parameters.AddWithValue("@observaciones", (object?)recepcion.Observaciones ?? DBNull.Value);
                    idRecepcion = Convert.ToInt32(cmdRec.ExecuteScalar());
                }

                // 2. Insertar Lote asociado
                const string sqlLote = @"
                    INSERT INTO Lote (
                        id_recepcion, id_insumo, codigo_lote, codigo_barras,
                        fecha_recepcion, fecha_caducidad, cantidad_inicial, cantidad_disponible, costo_unitario
                    )
                    VALUES (
                        @id_recepcion, @id_insumo, @codigo_lote, @codigo_barras,
                        @fecha_recepcion, @fecha_caducidad, @cantidad_inicial, @cantidad_disponible, @costo_unitario
                    );
                    SELECT LAST_INSERT_ID();";

                int idLote;
                using (var cmdLote = new MySqlCommand(sqlLote, conn, tx))
                {
                    cmdLote.Parameters.AddWithValue("@id_recepcion", idRecepcion);
                    cmdLote.Parameters.AddWithValue("@id_insumo", nuevoLote.IdInsumo);
                    cmdLote.Parameters.AddWithValue("@codigo_lote", nuevoLote.CodigoLote);
                    cmdLote.Parameters.AddWithValue("@codigo_barras", nuevoLote.CodigoBarras);
                    cmdLote.Parameters.AddWithValue("@fecha_recepcion", recepcion.FechaRecepcion);
                    cmdLote.Parameters.AddWithValue("@fecha_caducidad", (object?)nuevoLote.FechaCaducidad?.Date ?? DBNull.Value);
                    cmdLote.Parameters.AddWithValue("@cantidad_inicial", nuevoLote.CantidadInicial);
                    cmdLote.Parameters.AddWithValue("@cantidad_disponible", nuevoLote.CantidadInicial);
                    cmdLote.Parameters.AddWithValue("@costo_unitario", nuevoLote.CostoUnitario);
                    idLote = Convert.ToInt32(cmdLote.ExecuteScalar());
                }

                // 3. Registrar MovimientoInventario (ENTREGA)
                const string sqlMov = @"
                    INSERT INTO MovimientoInventario (
                        id_lote, id_usuario, tipo, cantidad, fecha_movimiento, observaciones
                    )
                    VALUES (
                        @id_lote, @id_usuario, 'ENTREGA', @cantidad, @fecha_movimiento, @observaciones
                    );";

                using (var cmdMov = new MySqlCommand(sqlMov, conn, tx))
                {
                    cmdMov.Parameters.AddWithValue("@id_lote", idLote);
                    cmdMov.Parameters.AddWithValue("@id_usuario", recepcion.IdUsuario);
                    cmdMov.Parameters.AddWithValue("@cantidad", nuevoLote.CantidadInicial);
                    cmdMov.Parameters.AddWithValue("@fecha_movimiento", recepcion.FechaRecepcion);
                    cmdMov.Parameters.AddWithValue("@observaciones", $"Entrada de lote {nuevoLote.CodigoLote}");
                    cmdMov.ExecuteNonQuery();
                }

                tx.Commit();
                return idLote;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        public int ContarLotesHoyPorInsumo(int idInsumo)
        {
            const string sql = @"
        SELECT COUNT(*)
        FROM Lote
        WHERE id_insumo = @id_insumo
          AND DATE(fecha_recepcion) = CURDATE();";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_insumo", idInsumo);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

    }
    
    }