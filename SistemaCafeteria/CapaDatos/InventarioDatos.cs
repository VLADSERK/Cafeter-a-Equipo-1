using System;
using System.Collections.Generic;
using CapaEntidades;
using MySql.Data.MySqlClient;

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
        /// Devuelve los lotes agrupados por insumo y, dentro de cada insumo, en orden PEPS
        /// (fecha y hora de recepción más antigua primero, RF-02).
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
                ORDER BY l.id_insumo ASC, {CriterioPeps.Orden("l")};";

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
        /// Código de barras interno EAN-13 (RF-01): prefijo 20 (uso interno del comercio),
        /// id del lote a 10 dígitos y dígito verificador. Es único porque el id del lote lo es.
        /// </summary>
        public static string GenerarCodigoBarrasInterno(int idLote, int prefijo = 20)
        {
            string cuerpo = $"{prefijo:00}{idLote:0000000000}";
            int suma = 0;
            for (int i = 0; i < 12; i++)
                suma += (cuerpo[i] - '0') * (i % 2 == 0 ? 1 : 3);
            int verificador = (10 - suma % 10) % 10;
            return cuerpo + verificador;
        }

        public bool ExisteCodigoLote(string codigoLote)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM Lote WHERE codigo_lote = @c;", conn);
            cmd.Parameters.AddWithValue("@c", codigoLote.Trim());
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool ExisteCodigoBarras(string codigoBarras)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM Lote WHERE codigo_barras = @c;", conn);
            cmd.Parameters.AddWithValue("@c", codigoBarras.Trim());
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        /// <summary>Cuántos lotes ya usan el prefijo de código (p. ej. "LOT-LEC-20261117-").</summary>
        public int ContarLotesConPrefijo(string prefijoCodigo)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM Lote WHERE codigo_lote LIKE @p;", conn);
            cmd.Parameters.AddWithValue("@p", prefijoCodigo.Replace("%", "").Replace("_", "\\_") + "%");
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// Transacción atómica: registra Recepcion, crea Lote y anota MovimientoInventario (ENTREGA).
        /// Si el lote no trae código de barras, genera uno único con <see cref="GenerarCodigoBarrasInterno"/>.
        /// Devuelve el id del lote y el código de barras guardado.
        /// </summary>
        public (int IdLote, string CodigoBarras) RegistrarRecepcionConLote(Recepcion recepcion, Lote nuevoLote)
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
                    // NULL en vez de '' para no violar el índice UNIQUE de codigo_barras
                    cmdLote.Parameters.AddWithValue("@codigo_barras",
                        string.IsNullOrWhiteSpace(nuevoLote.CodigoBarras) ? DBNull.Value : (object)nuevoLote.CodigoBarras.Trim());
                    cmdLote.Parameters.AddWithValue("@fecha_recepcion", recepcion.FechaRecepcion);
                    cmdLote.Parameters.AddWithValue("@fecha_caducidad", (object?)nuevoLote.FechaCaducidad?.Date ?? DBNull.Value);
                    cmdLote.Parameters.AddWithValue("@cantidad_inicial", nuevoLote.CantidadInicial);
                    cmdLote.Parameters.AddWithValue("@cantidad_disponible", nuevoLote.CantidadInicial);
                    cmdLote.Parameters.AddWithValue("@costo_unitario", nuevoLote.CostoUnitario);
                    idLote = Convert.ToInt32(cmdLote.ExecuteScalar());
                }

                // 2b. Identificador único (RF-01) si el usuario no capturó un código de barras
                string codigoBarras = nuevoLote.CodigoBarras?.Trim() ?? string.Empty;
                if (codigoBarras.Length == 0)
                {
                    // Prefijos 20–29 = uso interno; se cambia de prefijo solo si alguien capturó a mano ese mismo número
                    for (int prefijo = 20; prefijo <= 29; prefijo++)
                    {
                        codigoBarras = GenerarCodigoBarrasInterno(idLote, prefijo);
                        using var cmdExiste = new MySqlCommand("SELECT COUNT(*) FROM Lote WHERE codigo_barras = @c;", conn, tx);
                        cmdExiste.Parameters.AddWithValue("@c", codigoBarras);
                        if (Convert.ToInt32(cmdExiste.ExecuteScalar()) == 0) break;
                    }

                    using var cmdBarras = new MySqlCommand("UPDATE Lote SET codigo_barras = @c WHERE id_lote = @id;", conn, tx);
                    cmdBarras.Parameters.AddWithValue("@c", codigoBarras);
                    cmdBarras.Parameters.AddWithValue("@id", idLote);
                    cmdBarras.ExecuteNonQuery();
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
                return (idLote, codigoBarras);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Kardex de un lote (RF-03): todos sus movimientos en orden cronológico, con el
        /// documento que los originó (recepción/proveedor, folio de venta y producto, o merma).
        /// </summary>
        public List<MovimientoKardex> ObtenerKardexLote(int idLote)
        {
            const string sql = @"
                SELECT m.id_movimiento, m.tipo, m.cantidad, m.fecha_movimiento, m.observaciones,
                       u.nombre       AS usuario,
                       l.id_recepcion,
                       pr.nombre      AS proveedor,
                       v.folio,
                       p.nombre       AS producto,
                       m.id_merma,
                       mm.motivo      AS motivo_merma
                FROM MovimientoInventario m
                INNER JOIN Usuario u     ON u.id_usuario    = m.id_usuario
                INNER JOIN Lote l        ON l.id_lote       = m.id_lote
                LEFT  JOIN Recepcion r   ON r.id_recepcion  = l.id_recepcion
                LEFT  JOIN Proveedor pr  ON pr.id_proveedor = r.id_proveedor
                LEFT  JOIN DetalleVenta d ON d.id_detalle_venta = m.id_detalle_venta
                LEFT  JOIN Venta v       ON v.id_venta      = d.id_venta
                LEFT  JOIN Producto p    ON p.id_producto   = d.id_producto
                LEFT  JOIN Merma mm      ON mm.id_merma     = m.id_merma
                WHERE m.id_lote = @id_lote
                ORDER BY m.fecha_movimiento ASC, m.id_movimiento ASC;";

            var lista = new List<MovimientoKardex>();

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_lote", idLote);
            using var reader = cmd.ExecuteReader();

            string? Texto(string col) => reader.IsDBNull(reader.GetOrdinal(col)) ? null : reader.GetString(col);
            int? Entero(string col) => reader.IsDBNull(reader.GetOrdinal(col)) ? null : reader.GetInt32(col);

            while (reader.Read())
            {
                lista.Add(new MovimientoKardex
                {
                    IdMovimiento = reader.GetInt32("id_movimiento"),
                    Tipo = reader.GetString("tipo"),
                    Cantidad = reader.GetDecimal("cantidad"),
                    FechaMovimiento = reader.GetDateTime("fecha_movimiento"),
                    Observaciones = Texto("observaciones"),
                    Usuario = reader.GetString("usuario"),
                    IdRecepcion = Entero("id_recepcion"),
                    Proveedor = Texto("proveedor"),
                    FolioVenta = Texto("folio"),
                    Producto = Texto("producto"),
                    IdMerma = Entero("id_merma"),
                    MotivoMerma = Texto("motivo_merma")
                });
            }

            return lista;
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