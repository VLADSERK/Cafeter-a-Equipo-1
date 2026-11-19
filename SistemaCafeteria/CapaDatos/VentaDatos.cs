using System;
using System.Collections.Generic;
using CapaEntidades;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    public class VentaDatos
    {
        // =====================================================================
        // REGISTRO DE VENTA (con descuento PEPS de insumos)
        // =====================================================================

        /// <summary>
        /// Transacción atómica:
        /// 1. Crea la venta y le asigna folio (V-000001).
        /// 2. Por cada producto: toma el precio y la receta vigente desde MySQL, inserta el DetalleVenta,
        ///    descuenta cada insumo de los lotes en orden PEPS (fecha y hora de recepción más antigua primero),
        ///    guarda la trazabilidad en HistorialRecetaVenta y el movimiento CONSUMO_VENTA en el kardex.
        /// 3. Calcula total y cambio.
        /// Si falta stock de cualquier insumo, se revierte todo y se lanza InvalidOperationException.
        /// </summary>
        public Venta RegistrarVenta(Venta venta)
        {
            if (venta.Detalles == null || venta.Detalles.Count == 0)
                throw new InvalidOperationException("La venta no tiene productos.");

            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                DateTime fecha = venta.FechaVenta == default ? DateTime.Now : venta.FechaVenta;

                // 1. Cabecera con folio temporal (se reemplaza al conocer el id)
                const string sqlVenta = @"
                    INSERT INTO Venta (id_usuario, folio, fecha_venta, total, estado, metodo_pago, monto_recibido, cambio)
                    VALUES (@id_usuario, @folio_tmp, @fecha_venta, 0, 'Confirmada', @metodo_pago, @monto_recibido, NULL);
                    SELECT LAST_INSERT_ID();";

                int idVenta;
                using (var cmd = new MySqlCommand(sqlVenta, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id_usuario", venta.IdUsuario);
                    cmd.Parameters.AddWithValue("@folio_tmp", "TMP-" + Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@fecha_venta", fecha);
                    cmd.Parameters.AddWithValue("@metodo_pago", string.IsNullOrWhiteSpace(venta.MetodoPago) ? DBNull.Value : (object)venta.MetodoPago.Trim());
                    cmd.Parameters.AddWithValue("@monto_recibido", (object?)venta.MontoRecibido ?? DBNull.Value);
                    idVenta = Convert.ToInt32(cmd.ExecuteScalar());
                }

                decimal total = 0m;

                // 2. Detalles + descuento de insumos
                foreach (var detalle in venta.Detalles)
                {
                    if (detalle.Cantidad <= 0)
                        throw new InvalidOperationException("La cantidad de cada producto debe ser mayor a cero.");

                    var producto = ObtenerProductoVendible(conn, tx, detalle.IdProducto);

                    decimal subtotal = Redondeo.Importe(producto.Precio * detalle.Cantidad);
                    total += subtotal;

                    const string sqlDetalle = @"
                        INSERT INTO DetalleVenta (id_venta, id_producto, id_version_receta, cantidad, precio_unitario, subtotal)
                        VALUES (@id_venta, @id_producto, @id_version_receta, @cantidad, @precio_unitario, @subtotal);
                        SELECT LAST_INSERT_ID();";

                    int idDetalle;
                    using (var cmd = new MySqlCommand(sqlDetalle, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id_venta", idVenta);
                        cmd.Parameters.AddWithValue("@id_producto", detalle.IdProducto);
                        cmd.Parameters.AddWithValue("@id_version_receta", producto.IdVersionReceta);
                        cmd.Parameters.AddWithValue("@cantidad", detalle.Cantidad);
                        cmd.Parameters.AddWithValue("@precio_unitario", producto.Precio);
                        cmd.Parameters.AddWithValue("@subtotal", subtotal);
                        idDetalle = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Actualizar el objeto con los datos reales usados
                    detalle.IdDetalleVenta = idDetalle;
                    detalle.IdVenta = idVenta;
                    detalle.IdVersionReceta = producto.IdVersionReceta;
                    detalle.PrecioUnitario = producto.Precio;
                    detalle.Subtotal = subtotal;
                    detalle.NombreProducto = producto.Nombre;

                    // Explosión de la receta
                    var ingredientes = ObtenerIngredientes(conn, tx, producto.IdVersionReceta);
                    foreach (var ing in ingredientes)
                    {
                        decimal requerido = Redondeo.Cantidad(ing.Cantidad * ing.Factor * detalle.Cantidad);
                        if (requerido <= 0)
                            continue;

                        ConsumirInsumoPeps(conn, tx, ing.IdInsumo, ing.NombreInsumo, ing.UnidadInsumo,
                            requerido, idDetalle, venta.IdUsuario, fecha, producto.Nombre);
                    }
                }

                // 3. Total, cambio y folio definitivo
                total = Redondeo.Importe(total);
                decimal? cambio = null;
                if (venta.MontoRecibido.HasValue)
                {
                    decimal recibido = Redondeo.Importe(venta.MontoRecibido.Value);
                    if (recibido < total)
                        throw new InvalidOperationException(
                            $"El monto recibido ({recibido:C2}) es menor al total de la venta ({total:C2}).");
                    cambio = Redondeo.Importe(recibido - total);
                }

                string folio = $"V-{idVenta:D6}";

                const string sqlCerrar = @"
                    UPDATE Venta
                    SET total = @total, cambio = @cambio, folio = @folio
                    WHERE id_venta = @id_venta;";
                using (var cmd = new MySqlCommand(sqlCerrar, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@total", total);
                    cmd.Parameters.AddWithValue("@cambio", (object?)cambio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@folio", folio);
                    cmd.Parameters.AddWithValue("@id_venta", idVenta);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();

                venta.IdVenta = idVenta;
                venta.Folio = folio;
                venta.FechaVenta = fecha;
                venta.Total = total;
                venta.Cambio = cambio;
                venta.Estado = "Confirmada";
                return venta;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =====================================================================
        // CONSULTAS PARA HISTORIAL
        // =====================================================================

        /// <summary>
        /// Ventas entre dos fechas (ambas incluidas). estado: null = todas, "Confirmada" o "Cancelada".
        /// </summary>
        public List<Venta> ObtenerVentas(DateTime desde, DateTime hasta, string? estado = null)
        {
            var lista = new List<Venta>();
            bool filtrarEstado = !string.IsNullOrWhiteSpace(estado);

            // El filtro de estado se agrega solo si se pide. El patrón "(@estado IS NULL OR v.estado = @estado)"
            // con un parámetro nulo provocaba "Illegal mix of collations" en conexiones reutilizadas del pool.
            string sql = $@"
                SELECT v.id_venta, v.id_usuario, v.folio, v.fecha_venta, v.total, v.estado,
                       v.metodo_pago, v.monto_recibido, v.cambio, v.motivo_cancelacion,
                       u.nombre AS nombre_cajero
                FROM Venta v
                INNER JOIN Usuario u ON u.id_usuario = v.id_usuario
                WHERE v.fecha_venta >= @desde
                  AND v.fecha_venta < @hasta
                  {(filtrarEstado ? "AND v.estado = @estado" : "")}
                ORDER BY v.fecha_venta DESC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@desde", desde.Date);
            cmd.Parameters.AddWithValue("@hasta", hasta.Date.AddDays(1));
            if (filtrarEstado)
                cmd.Parameters.AddWithValue("@estado", estado!.Trim());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(MapearVenta(reader));

            return lista;
        }

        /// <summary>Venta con sus productos (Detalles) cargados.</summary>
        public Venta? ObtenerVentaConDetalle(int idVenta)
        {
            const string sqlVenta = @"
                SELECT v.id_venta, v.id_usuario, v.folio, v.fecha_venta, v.total, v.estado,
                       v.metodo_pago, v.monto_recibido, v.cambio, v.motivo_cancelacion,
                       u.nombre AS nombre_cajero
                FROM Venta v
                INNER JOIN Usuario u ON u.id_usuario = v.id_usuario
                WHERE v.id_venta = @id_venta;";

            const string sqlDetalle = @"
                SELECT d.id_detalle_venta, d.id_venta, d.id_producto, d.id_version_receta,
                       d.cantidad, d.precio_unitario, d.subtotal,
                       p.nombre AS nombre_producto
                FROM DetalleVenta d
                INNER JOIN Producto p ON p.id_producto = d.id_producto
                WHERE d.id_venta = @id_venta
                ORDER BY d.id_detalle_venta ASC;";

            using var conn = ConexionMySql.ObtenerConexion();

            Venta? venta;
            using (var cmd = new MySqlCommand(sqlVenta, conn))
            {
                cmd.Parameters.AddWithValue("@id_venta", idVenta);
                using var reader = cmd.ExecuteReader();
                venta = reader.Read() ? MapearVenta(reader) : null;
            }

            if (venta == null)
                return null;

            using (var cmd = new MySqlCommand(sqlDetalle, conn))
            {
                cmd.Parameters.AddWithValue("@id_venta", idVenta);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    venta.Detalles.Add(new DetalleVenta
                    {
                        IdDetalleVenta = reader.GetInt32("id_detalle_venta"),
                        IdVenta = reader.GetInt32("id_venta"),
                        IdProducto = reader.GetInt32("id_producto"),
                        IdVersionReceta = reader.GetInt32("id_version_receta"),
                        Cantidad = reader.GetInt32("cantidad"),
                        PrecioUnitario = reader.GetDecimal("precio_unitario"),
                        Subtotal = reader.GetDecimal("subtotal"),
                        NombreProducto = reader.GetString("nombre_producto")
                    });
                }
            }

            return venta;
        }

        public int ContarVentasHoyPorUsuario(int idUsuario)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM Venta
                WHERE id_usuario = @id_usuario
                  AND estado = 'Confirmada'
                  AND DATE(fecha_venta) = CURDATE();";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_usuario", idUsuario);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // =====================================================================
        // CANCELACIÓN (regresa los insumos a sus lotes)
        // =====================================================================

        /// <summary>
        /// Transacción atómica: marca la venta como Cancelada y, por cada movimiento CONSUMO_VENTA
        /// de esa venta, regresa la cantidad a su lote y registra un movimiento REVERSION
        /// enlazado al movimiento original (id_movimiento_origen).
        /// </summary>
        public (bool Exito, string Mensaje) CancelarVenta(int idVenta, int idUsuarioAutoriza, string motivo)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Bloquear la venta y validar su estado
                string? estadoActual;
                string folio;
                const string sqlVenta = "SELECT estado, folio FROM Venta WHERE id_venta = @id_venta FOR UPDATE;";
                using (var cmd = new MySqlCommand(sqlVenta, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id_venta", idVenta);
                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        reader.Close();
                        tx.Rollback();
                        return (false, "La venta indicada no existe.");
                    }
                    estadoActual = reader.GetString("estado");
                    folio = reader.GetString("folio");
                }

                if (estadoActual != "Confirmada")
                {
                    tx.Rollback();
                    return (false, $"La venta {folio} ya estaba cancelada.");
                }

                // 2. Movimientos de consumo de esa venta
                var consumos = new List<(int IdMovimiento, int IdLote, decimal Cantidad, int IdDetalleVenta)>();
                const string sqlConsumos = @"
                    SELECT m.id_movimiento, m.id_lote, m.cantidad, m.id_detalle_venta
                    FROM MovimientoInventario m
                    INNER JOIN DetalleVenta d ON d.id_detalle_venta = m.id_detalle_venta
                    WHERE d.id_venta = @id_venta
                      AND m.tipo = 'CONSUMO_VENTA';";
                using (var cmd = new MySqlCommand(sqlConsumos, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id_venta", idVenta);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        consumos.Add((
                            reader.GetInt32("id_movimiento"),
                            reader.GetInt32("id_lote"),
                            reader.GetDecimal("cantidad"),
                            reader.GetInt32("id_detalle_venta")));
                    }
                }

                // 3. Regresar cada cantidad a su lote y registrar la reversión
                const string sqlLote = @"
                    UPDATE Lote
                    SET cantidad_disponible = cantidad_disponible + @cantidad
                    WHERE id_lote = @id_lote;";

                const string sqlReversion = @"
                    INSERT INTO MovimientoInventario (
                        id_lote, id_usuario, id_detalle_venta, id_movimiento_origen,
                        tipo, cantidad, fecha_movimiento, observaciones
                    ) VALUES (
                        @id_lote, @id_usuario, @id_detalle_venta, @id_origen,
                        'REVERSION', @cantidad, @fecha, @observaciones
                    );";

                DateTime ahora = DateTime.Now;
                foreach (var c in consumos)
                {
                    using (var cmd = new MySqlCommand(sqlLote, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@cantidad", c.Cantidad);
                        cmd.Parameters.AddWithValue("@id_lote", c.IdLote);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new MySqlCommand(sqlReversion, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id_lote", c.IdLote);
                        cmd.Parameters.AddWithValue("@id_usuario", idUsuarioAutoriza);
                        cmd.Parameters.AddWithValue("@id_detalle_venta", c.IdDetalleVenta);
                        cmd.Parameters.AddWithValue("@id_origen", c.IdMovimiento);
                        cmd.Parameters.AddWithValue("@cantidad", c.Cantidad);
                        cmd.Parameters.AddWithValue("@fecha", ahora);
                        cmd.Parameters.AddWithValue("@observaciones", $"Cancelación de venta {folio}");
                        cmd.ExecuteNonQuery();
                    }
                }

                // 4. Marcar la venta como cancelada
                const string sqlCancelar = @"
                    UPDATE Venta
                    SET estado = 'Cancelada',
                        motivo_cancelacion = @motivo
                    WHERE id_venta = @id_venta;";
                using (var cmd = new MySqlCommand(sqlCancelar, conn, tx))
                {
                    string motivoFinal = motivo.Trim();
                    if (motivoFinal.Length > 255)
                        motivoFinal = motivoFinal.Substring(0, 255);

                    cmd.Parameters.AddWithValue("@motivo", motivoFinal);
                    cmd.Parameters.AddWithValue("@id_venta", idVenta);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return (true, $"Venta {folio} cancelada. Los insumos regresaron al inventario.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =====================================================================
        // AUXILIARES PRIVADOS
        // =====================================================================

        private sealed class ProductoVendible
        {
            public string Nombre { get; set; } = string.Empty;
            public decimal Precio { get; set; }
            public int IdVersionReceta { get; set; }
        }

        private sealed class IngredienteVenta
        {
            public int IdInsumo { get; set; }
            public string NombreInsumo { get; set; } = string.Empty;
            public string UnidadInsumo { get; set; } = string.Empty;
            public decimal Cantidad { get; set; }
            public decimal Factor { get; set; } = 1m;
        }

        /// <summary>Precio y versión de receta vigente tomados de MySQL (no del formulario).</summary>
        private static ProductoVendible ObtenerProductoVendible(MySqlConnection conn, MySqlTransaction tx, int idProducto)
        {
            const string sql = @"
                SELECT p.nombre, p.precio, p.estado,
                       (SELECT vr.id_version_receta
                        FROM VersionReceta vr
                        WHERE vr.id_producto = p.id_producto AND vr.estado = TRUE
                        ORDER BY vr.numero_version DESC
                        LIMIT 1) AS id_version_activa
                FROM Producto p
                WHERE p.id_producto = @id_producto;";

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id_producto", idProducto);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException("Uno de los productos ya no existe en el catálogo.");

            string nombre = reader.GetString("nombre");

            if (reader.GetString("estado") != "Activo")
                throw new InvalidOperationException($"El producto \"{nombre}\" está inactivo y no se puede vender.");

            int ordVersion = reader.GetOrdinal("id_version_activa");
            if (reader.IsDBNull(ordVersion))
                throw new InvalidOperationException($"El producto \"{nombre}\" no tiene receta registrada.");

            return new ProductoVendible
            {
                Nombre = nombre,
                Precio = reader.GetDecimal("precio"),
                IdVersionReceta = reader.GetInt32(ordVersion)
            };
        }

        /// <summary>
        /// Ingredientes de una versión de receta, con el factor para convertir la unidad
        /// de la receta a la unidad en que se maneja el insumo (ej. g → kg = 0.001).
        /// </summary>
        private static List<IngredienteVenta> ObtenerIngredientes(MySqlConnection conn, MySqlTransaction tx, int idVersionReceta)
        {
            var lista = new List<IngredienteVenta>();
            const string sql = @"
                SELECT dr.id_insumo, dr.cantidad,
                       i.nombre        AS nombre_insumo,
                       ur.abreviatura  AS abrev_receta,
                       ur.tipo         AS tipo_receta,
                       ui.abreviatura  AS abrev_insumo,
                       ui.tipo         AS tipo_insumo
                FROM DetalleReceta dr
                INNER JOIN Insumo       i  ON i.id_insumo  = dr.id_insumo
                INNER JOIN UnidadMedida ur ON ur.id_unidad = dr.id_unidad
                INNER JOIN UnidadMedida ui ON ui.id_unidad = i.id_unidad
                WHERE dr.id_version_receta = @id_version;";

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id_version", idVersionReceta);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string abrevInsumo = reader.GetString("abrev_insumo");

                lista.Add(new IngredienteVenta
                {
                    IdInsumo = reader.GetInt32("id_insumo"),
                    NombreInsumo = reader.GetString("nombre_insumo"),
                    UnidadInsumo = abrevInsumo,
                    Cantidad = reader.GetDecimal("cantidad"),
                    Factor = ConversionUnidades.Factor(
                        reader.GetString("abrev_receta"), reader.GetString("tipo_receta"),
                        abrevInsumo, reader.GetString("tipo_insumo"))
                });
            }

            return lista;
        }

        /// <summary>
        /// Descuenta una cantidad de un insumo recorriendo sus lotes en orden PEPS.
        /// Bloquea los lotes (FOR UPDATE) y lanza InvalidOperationException si no alcanza.
        /// </summary>
        private static void ConsumirInsumoPeps(
            MySqlConnection conn, MySqlTransaction tx,
            int idInsumo, string nombreInsumo, string unidad,
            decimal requerido, int idDetalleVenta, int idUsuario, DateTime fecha, string nombreProducto)
        {
            var lotes = new List<(int IdLote, decimal Disponible, decimal Costo)>();
            string sqlLotes = $@"
                SELECT id_lote, cantidad_disponible, costo_unitario
                FROM Lote
                WHERE id_insumo = @id_insumo
                  AND cantidad_disponible > 0
                ORDER BY {CriterioPeps.Orden()}
                FOR UPDATE;";

            using (var cmd = new MySqlCommand(sqlLotes, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id_insumo", idInsumo);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lotes.Add((
                        reader.GetInt32("id_lote"),
                        reader.GetDecimal("cantidad_disponible"),
                        reader.GetDecimal("costo_unitario")));
                }
            }

            decimal disponibleTotal = 0m;
            foreach (var l in lotes)
                disponibleTotal += l.Disponible;

            if (disponibleTotal < requerido)
            {
                throw new InvalidOperationException(
                    $"Stock insuficiente de \"{nombreInsumo}\" para preparar \"{nombreProducto}\". " +
                    $"Se necesitan {requerido:0.###} {unidad} y hay {disponibleTotal:0.###} {unidad}.");
            }

            const string sqlDescontar = @"
                UPDATE Lote
                SET cantidad_disponible = cantidad_disponible - @cantidad
                WHERE id_lote = @id_lote;";

            const string sqlHistorial = @"
                INSERT INTO HistorialRecetaVenta (id_detalle_venta, id_insumo, id_lote, cantidad_consumida, costo_unitario)
                VALUES (@id_detalle_venta, @id_insumo, @id_lote, @cantidad, @costo_unitario);";

            const string sqlMovimiento = @"
                INSERT INTO MovimientoInventario (
                    id_lote, id_usuario, id_detalle_venta, tipo, cantidad, fecha_movimiento, observaciones
                ) VALUES (
                    @id_lote, @id_usuario, @id_detalle_venta, 'CONSUMO_VENTA', @cantidad, @fecha, @observaciones
                );";

            decimal restante = requerido;
            foreach (var lote in lotes)
            {
                if (restante <= 0) break;

                decimal tomar = Math.Min(restante, lote.Disponible);
                if (tomar <= 0) continue;

                using (var cmd = new MySqlCommand(sqlDescontar, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cantidad", tomar);
                    cmd.Parameters.AddWithValue("@id_lote", lote.IdLote);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(sqlHistorial, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id_detalle_venta", idDetalleVenta);
                    cmd.Parameters.AddWithValue("@id_insumo", idInsumo);
                    cmd.Parameters.AddWithValue("@id_lote", lote.IdLote);
                    cmd.Parameters.AddWithValue("@cantidad", tomar);
                    cmd.Parameters.AddWithValue("@costo_unitario", lote.Costo);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(sqlMovimiento, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id_lote", lote.IdLote);
                    cmd.Parameters.AddWithValue("@id_usuario", idUsuario);
                    cmd.Parameters.AddWithValue("@id_detalle_venta", idDetalleVenta);
                    cmd.Parameters.AddWithValue("@cantidad", tomar);
                    cmd.Parameters.AddWithValue("@fecha", fecha);
                    cmd.Parameters.AddWithValue("@observaciones", $"Venta de {nombreProducto}");
                    cmd.ExecuteNonQuery();
                }

                restante -= tomar;
            }
        }

        private static Venta MapearVenta(MySqlDataReader reader)
        {
            int ordMetodo = reader.GetOrdinal("metodo_pago");
            int ordMonto = reader.GetOrdinal("monto_recibido");
            int ordCambio = reader.GetOrdinal("cambio");
            int ordMotivo = reader.GetOrdinal("motivo_cancelacion");

            return new Venta
            {
                IdVenta = reader.GetInt32("id_venta"),
                IdUsuario = reader.GetInt32("id_usuario"),
                Folio = reader.GetString("folio"),
                FechaVenta = reader.GetDateTime("fecha_venta"),
                Total = reader.GetDecimal("total"),
                Estado = reader.GetString("estado"),
                MetodoPago = reader.IsDBNull(ordMetodo) ? null : reader.GetString(ordMetodo),
                MontoRecibido = reader.IsDBNull(ordMonto) ? null : reader.GetDecimal(ordMonto),
                Cambio = reader.IsDBNull(ordCambio) ? null : reader.GetDecimal(ordCambio),
                MotivoCancelacion = reader.IsDBNull(ordMotivo) ? null : reader.GetString(ordMotivo),
                NombreCajero = reader.GetString("nombre_cajero")
            };
        }
    }
}