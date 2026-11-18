using System;
using System.Collections.Generic;
using CapaEntidades;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    /// <summary>
    /// Consultas de solo lectura para los reportes 5.1–5.4 (RF-10).
    /// Todos los métodos reciben el rango como [desde, hastaExclusivo): desde a las 00:00
    /// y el día siguiente a la fecha final a las 00:00.
    /// </summary>
    public class ReporteDatos
    {
        // =====================================================================
        // 5.1 VENTAS
        // =====================================================================

        public List<ReporteVentaFila> ObtenerVentas(DateTime desde, DateTime hastaExclusivo)
        {
            const string sql = @"
                SELECT v.id_venta, v.folio, v.fecha_venta, v.total, v.estado, v.metodo_pago,
                       u.nombre AS cajero,
                       GROUP_CONCAT(CONCAT(d.cantidad, 'x ', p.nombre)
                                    ORDER BY d.id_detalle_venta SEPARATOR ', ') AS productos,
                       COALESCE(SUM(d.cantidad), 0) AS articulos
                FROM Venta v
                INNER JOIN Usuario u      ON u.id_usuario = v.id_usuario
                LEFT  JOIN DetalleVenta d ON d.id_venta = v.id_venta
                LEFT  JOIN Producto p     ON p.id_producto = d.id_producto
                WHERE v.fecha_venta >= @desde AND v.fecha_venta < @hasta
                GROUP BY v.id_venta, v.folio, v.fecha_venta, v.total, v.estado, v.metodo_pago, u.nombre
                ORDER BY v.fecha_venta ASC, v.id_venta ASC;";

            var lista = new List<ReporteVentaFila>();
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = CrearComando(sql, conn, desde, hastaExclusivo);
            using var r = cmd.ExecuteReader();

            int ordMetodo = r.GetOrdinal("metodo_pago");
            int ordProductos = r.GetOrdinal("productos");
            while (r.Read())
            {
                lista.Add(new ReporteVentaFila
                {
                    IdVenta = r.GetInt32("id_venta"),
                    Folio = r.GetString("folio"),
                    FechaVenta = r.GetDateTime("fecha_venta"),
                    Total = r.GetDecimal("total"),
                    Estado = r.GetString("estado"),
                    MetodoPago = r.IsDBNull(ordMetodo) ? null : r.GetString(ordMetodo),
                    Cajero = r.GetString("cajero"),
                    Productos = r.IsDBNull(ordProductos) ? string.Empty : r.GetString(ordProductos),
                    Articulos = Convert.ToInt32(r.GetValue(r.GetOrdinal("articulos")))
                });
            }
            return lista;
        }

        // =====================================================================
        // 5.2 CONSUMO DE INSUMOS
        // =====================================================================

        /// <summary>
        /// Consumo real tomado de los movimientos CONSUMO_VENTA del kardex, solo de ventas
        /// confirmadas del periodo (las canceladas ya regresaron sus insumos con REVERSION).
        /// Se agrupa por insumo y lote para conservar el costo PEPS de cada lote.
        /// </summary>
        public List<ReporteConsumoFila> ObtenerConsumo(DateTime desde, DateTime hastaExclusivo)
        {
            const string sql = @"
                SELECT i.id_insumo, i.nombre AS insumo, um.abreviatura AS unidad,
                       l.codigo_lote, l.costo_unitario,
                       SUM(m.cantidad) AS cantidad
                FROM MovimientoInventario m
                INNER JOIN DetalleVenta d  ON d.id_detalle_venta = m.id_detalle_venta
                INNER JOIN Venta v         ON v.id_venta = d.id_venta
                INNER JOIN Lote l          ON l.id_lote = m.id_lote
                INNER JOIN Insumo i        ON i.id_insumo = l.id_insumo
                INNER JOIN UnidadMedida um ON um.id_unidad = i.id_unidad
                WHERE m.tipo = 'CONSUMO_VENTA'
                  AND v.estado = 'Confirmada'
                  AND v.fecha_venta >= @desde AND v.fecha_venta < @hasta
                GROUP BY i.id_insumo, i.nombre, um.abreviatura, l.id_lote, l.codigo_lote, l.costo_unitario, l.fecha_recepcion
                ORDER BY i.nombre ASC, l.fecha_recepcion ASC, l.id_lote ASC;";

            var lista = new List<ReporteConsumoFila>();
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = CrearComando(sql, conn, desde, hastaExclusivo);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new ReporteConsumoFila
                {
                    IdInsumo = r.GetInt32("id_insumo"),
                    Insumo = r.GetString("insumo"),
                    Unidad = r.GetString("unidad"),
                    CodigoLote = r.GetString("codigo_lote"),
                    CostoUnitario = r.GetDecimal("costo_unitario"),
                    Cantidad = r.GetDecimal("cantidad")
                });
            }
            return lista;
        }

        // =====================================================================
        // 5.3 EXISTENCIAS
        // =====================================================================

        /// <summary>
        /// Lotes con existencia al cierre del periodo. La cantidad al cierre se reconstruye
        /// desde el kardex: disponible hoy menos el efecto de los movimientos posteriores
        /// al cierre (ENTREGA y REVERSION suman; CONSUMO_VENTA y MERMA restan).
        /// Lote.CantidadDisponible trae la cantidad AL CIERRE, no la actual.
        /// </summary>
        public List<Lote> ObtenerLotesAlCierre(DateTime hastaExclusivo)
        {
            const string sql = @"
                SELECT x.id_lote, x.id_insumo, x.codigo_lote, x.costo_unitario, x.cantidad_al_cierre
                FROM (
                    SELECT l.id_lote, l.id_insumo, l.codigo_lote, l.costo_unitario, l.fecha_recepcion,
                           l.cantidad_disponible - COALESCE((
                               SELECT SUM(CASE WHEN m.tipo IN ('ENTREGA', 'REVERSION') THEN m.cantidad
                                               ELSE -m.cantidad END)
                               FROM MovimientoInventario m
                               WHERE m.id_lote = l.id_lote
                                 AND m.fecha_movimiento >= @hasta
                           ), 0) AS cantidad_al_cierre
                    FROM Lote l
                    WHERE l.fecha_recepcion < @hasta
                ) x
                WHERE x.cantidad_al_cierre > 0
                ORDER BY x.id_insumo ASC, x.fecha_recepcion ASC, x.id_lote ASC;";

            var lista = new List<Lote>();
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@hasta", hastaExclusivo);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Lote
                {
                    IdLote = r.GetInt32("id_lote"),
                    IdInsumo = r.GetInt32("id_insumo"),
                    CodigoLote = r.GetString("codigo_lote"),
                    CostoUnitario = r.GetDecimal("costo_unitario"),
                    CantidadDisponible = r.GetDecimal("cantidad_al_cierre")
                });
            }
            return lista;
        }

        /// <summary>Entradas (ENTREGA + REVERSION) y salidas (CONSUMO_VENTA + MERMA) de cada insumo en el periodo.</summary>
        public Dictionary<int, (decimal Entradas, decimal Salidas)> ObtenerMovimientosPorInsumo(DateTime desde, DateTime hastaExclusivo)
        {
            const string sql = @"
                SELECT l.id_insumo,
                       COALESCE(SUM(CASE WHEN m.tipo IN ('ENTREGA', 'REVERSION') THEN m.cantidad ELSE 0 END), 0) AS entradas,
                       COALESCE(SUM(CASE WHEN m.tipo IN ('CONSUMO_VENTA', 'MERMA') THEN m.cantidad ELSE 0 END), 0) AS salidas
                FROM MovimientoInventario m
                INNER JOIN Lote l ON l.id_lote = m.id_lote
                WHERE m.fecha_movimiento >= @desde AND m.fecha_movimiento < @hasta
                GROUP BY l.id_insumo;";

            var resultado = new Dictionary<int, (decimal, decimal)>();
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = CrearComando(sql, conn, desde, hastaExclusivo);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                resultado[r.GetInt32("id_insumo")] = (r.GetDecimal("entradas"), r.GetDecimal("salidas"));
            return resultado;
        }

        // =====================================================================
        // 5.4 MERMAS
        // =====================================================================

        public List<ReporteMermaFila> ObtenerMermas(DateTime desde, DateTime hastaExclusivo)
        {
            const string sql = @"
                SELECT m.id_merma, m.fecha_merma, m.cantidad, m.motivo, m.costo_estimado, m.estado,
                       i.nombre AS insumo, um.abreviatura AS unidad,
                       -- Merma sin lote identificado: se muestran los lotes que se descontaron por PEPS
                       COALESCE(l.codigo_lote, (
                           SELECT CONCAT('PEPS: ', GROUP_CONCAT(DISTINCT lx.codigo_lote ORDER BY mi.id_movimiento SEPARATOR ', '))
                           FROM MovimientoInventario mi
                           INNER JOIN Lote lx ON lx.id_lote = mi.id_lote
                           WHERE mi.id_merma = m.id_merma AND mi.tipo = 'MERMA'
                       )) AS codigo_lote,
                       ur.nombre AS registra,
                       ua.nombre AS autoriza
                FROM Merma m
                INNER JOIN Insumo i        ON i.id_insumo = m.id_insumo
                INNER JOIN UnidadMedida um ON um.id_unidad = i.id_unidad
                INNER JOIN Usuario ur      ON ur.id_usuario = m.id_usuario_registra
                LEFT  JOIN Usuario ua      ON ua.id_usuario = m.id_usuario_autoriza
                LEFT  JOIN Lote l          ON l.id_lote = m.id_lote
                WHERE m.fecha_merma >= @desde AND m.fecha_merma < @hasta
                ORDER BY m.fecha_merma ASC, m.id_merma ASC;";

            var lista = new List<ReporteMermaFila>();
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = CrearComando(sql, conn, desde, hastaExclusivo);
            using var r = cmd.ExecuteReader();

            int ordLote = r.GetOrdinal("codigo_lote");
            int ordAutoriza = r.GetOrdinal("autoriza");
            while (r.Read())
            {
                lista.Add(new ReporteMermaFila
                {
                    IdMerma = r.GetInt32("id_merma"),
                    FechaMerma = r.GetDateTime("fecha_merma"),
                    Cantidad = r.GetDecimal("cantidad"),
                    Motivo = r.GetString("motivo"),
                    CostoEstimado = r.GetDecimal("costo_estimado"),
                    Estado = r.GetString("estado"),
                    Insumo = r.GetString("insumo"),
                    Unidad = r.GetString("unidad"),
                    CodigoLote = r.IsDBNull(ordLote) ? "PEPS (al autorizar)" : r.GetString(ordLote),
                    RegistradoPor = r.GetString("registra"),
                    AutorizadoPor = r.IsDBNull(ordAutoriza) ? null : r.GetString(ordAutoriza)
                });
            }
            return lista;
        }

        // =====================================================================
        // AUXILIARES
        // =====================================================================

        private static MySqlCommand CrearComando(string sql, MySqlConnection conn, DateTime desde, DateTime hastaExclusivo)
        {
            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hastaExclusivo);
            return cmd;
        }
    }
}
