using System;
using System.Collections.Generic;
using CapaEntidades;
using MySqlConnector;

namespace CapaDatos
{
    public class CatalogoDatos
    {
        // =====================================================================
        // CATEGORÍAS
        // =====================================================================

        public List<Categoria> ObtenerCategorias()
        {
            var lista = new List<Categoria>();
            const string sql = "SELECT id_categoria, nombre FROM Categoria ORDER BY nombre ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new Categoria
                {
                    IdCategoria = reader.GetInt32("id_categoria"),
                    Nombre = reader.GetString("nombre")
                });
            }
            return lista;
        }

        // =====================================================================
        // PRODUCTOS
        // =====================================================================

        /// <summary>
        /// Lista productos con su categoría y el id de su versión de receta activa (si tiene).
        /// </summary>
        public List<Producto> ObtenerProductos(bool soloActivos = true, int? idCategoria = null)
        {
            var lista = new List<Producto>();
            const string sql = @"
                SELECT p.id_producto,
                       p.id_categoria,
                       p.nombre,
                       p.precio,
                       p.codigo_barras,
                       p.imagen_ruta,
                       p.estado,
                       c.nombre AS nombre_categoria,
                       (SELECT vr.id_version_receta
                        FROM VersionReceta vr
                        WHERE vr.id_producto = p.id_producto
                          AND vr.estado = TRUE
                        ORDER BY vr.numero_version DESC
                        LIMIT 1) AS id_version_activa
                FROM Producto p
                INNER JOIN Categoria c ON c.id_categoria = p.id_categoria
                WHERE (@solo_activos = 0 OR p.estado = 'Activo')
                  AND (@id_categoria IS NULL OR p.id_categoria = @id_categoria)
                ORDER BY c.nombre ASC, p.nombre ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@solo_activos", soloActivos ? 1 : 0);
            cmd.Parameters.AddWithValue("@id_categoria", (object?)idCategoria ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(MapearProducto(reader));

            return lista;
        }

        /// <summary>
        /// Devuelve un producto con los ingredientes de su receta activa cargados en RecetaActiva.
        /// </summary>
        public Producto? ObtenerProductoConReceta(int idProducto)
        {
            const string sql = @"
                SELECT p.id_producto,
                       p.id_categoria,
                       p.nombre,
                       p.precio,
                       p.codigo_barras,
                       p.imagen_ruta,
                       p.estado,
                       c.nombre AS nombre_categoria,
                       (SELECT vr.id_version_receta
                        FROM VersionReceta vr
                        WHERE vr.id_producto = p.id_producto
                          AND vr.estado = TRUE
                        ORDER BY vr.numero_version DESC
                        LIMIT 1) AS id_version_activa
                FROM Producto p
                INNER JOIN Categoria c ON c.id_categoria = p.id_categoria
                WHERE p.id_producto = @id_producto;";

            Producto? producto;

            using (var conn = ConexionMySql.ObtenerConexion())
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id_producto", idProducto);
                using var reader = cmd.ExecuteReader();
                producto = reader.Read() ? MapearProducto(reader) : null;
            }

            if (producto != null && producto.IdVersionRecetaActiva.HasValue)
                producto.RecetaActiva = ObtenerDetalleReceta(producto.IdVersionRecetaActiva.Value);

            return producto;
        }

        public int InsertarProducto(Producto producto)
        {
            const string sql = @"
                INSERT INTO Producto (id_categoria, nombre, precio, codigo_barras, imagen_ruta, estado)
                VALUES (@id_categoria, @nombre, @precio, @codigo_barras, @imagen_ruta, 'Activo');
                SELECT LAST_INSERT_ID();";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            AgregarParametrosProducto(cmd, producto);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void ActualizarProducto(Producto producto)
        {
            const string sql = @"
                UPDATE Producto
                SET id_categoria = @id_categoria,
                    nombre = @nombre,
                    precio = @precio,
                    codigo_barras = @codigo_barras,
                    imagen_ruta = @imagen_ruta
                WHERE id_producto = @id_producto;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            AgregarParametrosProducto(cmd, producto);
            cmd.Parameters.AddWithValue("@id_producto", producto.IdProducto);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Activa o desactiva un producto (no se borra para conservar el historial de ventas).
        /// </summary>
        public void CambiarEstadoProducto(int idProducto, bool activo)
        {
            const string sql = "UPDATE Producto SET estado = @estado WHERE id_producto = @id_producto;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@estado", activo ? "Activo" : "Inactivo");
            cmd.Parameters.AddWithValue("@id_producto", idProducto);
            cmd.ExecuteNonQuery();
        }

        public bool ExisteNombreProducto(string nombre, int idProductoExcluir = 0)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM Producto
                WHERE LOWER(TRIM(nombre)) = LOWER(TRIM(@nombre))
                  AND id_producto <> @excluir;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@nombre", nombre);
            cmd.Parameters.AddWithValue("@excluir", idProductoExcluir);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool ExisteCodigoBarrasProducto(string codigoBarras, int idProductoExcluir = 0)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM Producto
                WHERE codigo_barras = @codigo
                  AND id_producto <> @excluir;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@codigo", codigoBarras.Trim());
            cmd.Parameters.AddWithValue("@excluir", idProductoExcluir);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        // =====================================================================
        // RECETAS VERSIONADAS
        // =====================================================================

        public List<DetalleReceta> ObtenerDetalleReceta(int idVersionReceta)
        {
            var lista = new List<DetalleReceta>();
            const string sql = @"
                SELECT dr.id_detalle_receta,
                       dr.id_version_receta,
                       dr.id_insumo,
                       dr.id_unidad,
                       dr.cantidad,
                       i.nombre       AS nombre_insumo,
                       u.abreviatura  AS unidad_abrev
                FROM DetalleReceta dr
                INNER JOIN Insumo       i ON i.id_insumo = dr.id_insumo
                INNER JOIN UnidadMedida u ON u.id_unidad = dr.id_unidad
                WHERE dr.id_version_receta = @id_version
                ORDER BY i.nombre ASC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_version", idVersionReceta);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new DetalleReceta
                {
                    IdDetalleReceta = reader.GetInt32("id_detalle_receta"),
                    IdVersionReceta = reader.GetInt32("id_version_receta"),
                    IdInsumo = reader.GetInt32("id_insumo"),
                    IdUnidad = reader.GetInt32("id_unidad"),
                    Cantidad = reader.GetDecimal("cantidad"),
                    NombreInsumo = reader.GetString("nombre_insumo"),
                    UnidadAbreviatura = reader.GetString("unidad_abrev")
                });
            }
            return lista;
        }

        public List<VersionReceta> ObtenerVersionesReceta(int idProducto)
        {
            var lista = new List<VersionReceta>();
            const string sql = @"
                SELECT id_version_receta, id_producto, numero_version, fecha_creacion, estado
                FROM VersionReceta
                WHERE id_producto = @id_producto
                ORDER BY numero_version DESC;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_producto", idProducto);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new VersionReceta
                {
                    IdVersionReceta = reader.GetInt32("id_version_receta"),
                    IdProducto = reader.GetInt32("id_producto"),
                    NumeroVersion = reader.GetInt32("numero_version"),
                    FechaCreacion = reader.GetDateTime("fecha_creacion"),
                    Estado = reader.GetBoolean("estado")
                });
            }
            return lista;
        }

        /// <summary>
        /// Transacción atómica: desactiva la versión vigente, crea una versión nueva (número + 1)
        /// e inserta sus ingredientes. Las versiones viejas se conservan porque DetalleVenta
        /// guarda qué versión se usó en cada venta.
        /// </summary>
        public int GuardarNuevaVersionReceta(int idProducto, List<DetalleReceta> ingredientes)
        {
            using var conn = ConexionMySql.ObtenerConexion();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1. Bloquear el producto para que dos usuarios no creen la misma versión a la vez
                const string sqlLock = "SELECT id_producto FROM Producto WHERE id_producto = @id_producto FOR UPDATE;";
                using (var cmdLock = new MySqlCommand(sqlLock, conn, tx))
                {
                    cmdLock.Parameters.AddWithValue("@id_producto", idProducto);
                    if (cmdLock.ExecuteScalar() == null)
                        throw new InvalidOperationException("El producto ya no existe en la base de datos.");
                }

                // 2. Calcular el siguiente número de versión
                int numeroVersion;
                const string sqlNum = @"
                    SELECT COALESCE(MAX(numero_version), 0) + 1
                    FROM VersionReceta
                    WHERE id_producto = @id_producto;";
                using (var cmdNum = new MySqlCommand(sqlNum, conn, tx))
                {
                    cmdNum.Parameters.AddWithValue("@id_producto", idProducto);
                    numeroVersion = Convert.ToInt32(cmdNum.ExecuteScalar());
                }

                // 3. Desactivar la versión vigente
                const string sqlDesactivar = @"
                    UPDATE VersionReceta
                    SET estado = FALSE
                    WHERE id_producto = @id_producto AND estado = TRUE;";
                using (var cmdDes = new MySqlCommand(sqlDesactivar, conn, tx))
                {
                    cmdDes.Parameters.AddWithValue("@id_producto", idProducto);
                    cmdDes.ExecuteNonQuery();
                }

                // 4. Crear la nueva versión activa
                int idVersion;
                const string sqlVersion = @"
                    INSERT INTO VersionReceta (id_producto, numero_version, fecha_creacion, estado)
                    VALUES (@id_producto, @numero_version, @fecha_creacion, TRUE);
                    SELECT LAST_INSERT_ID();";
                using (var cmdVer = new MySqlCommand(sqlVersion, conn, tx))
                {
                    cmdVer.Parameters.AddWithValue("@id_producto", idProducto);
                    cmdVer.Parameters.AddWithValue("@numero_version", numeroVersion);
                    cmdVer.Parameters.AddWithValue("@fecha_creacion", DateTime.Now);
                    idVersion = Convert.ToInt32(cmdVer.ExecuteScalar());
                }

                // 5. Insertar los ingredientes
                const string sqlDetalle = @"
                    INSERT INTO DetalleReceta (id_version_receta, id_insumo, id_unidad, cantidad)
                    VALUES (@id_version_receta, @id_insumo, @id_unidad, @cantidad);";

                foreach (var ing in ingredientes)
                {
                    using var cmdDet = new MySqlCommand(sqlDetalle, conn, tx);
                    cmdDet.Parameters.AddWithValue("@id_version_receta", idVersion);
                    cmdDet.Parameters.AddWithValue("@id_insumo", ing.IdInsumo);
                    cmdDet.Parameters.AddWithValue("@id_unidad", ing.IdUnidad);
                    cmdDet.Parameters.AddWithValue("@cantidad", ing.Cantidad);
                    cmdDet.ExecuteNonQuery();
                }

                tx.Commit();
                return idVersion;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =====================================================================
        // AUXILIARES
        // =====================================================================

        private static Producto MapearProducto(MySqlDataReader reader)
        {
            int ordCodigo = reader.GetOrdinal("codigo_barras");
            int ordImagen = reader.GetOrdinal("imagen_ruta");
            int ordVersion = reader.GetOrdinal("id_version_activa");

            return new Producto
            {
                IdProducto = reader.GetInt32("id_producto"),
                IdCategoria = reader.GetInt32("id_categoria"),
                Nombre = reader.GetString("nombre"),
                Precio = reader.GetDecimal("precio"),
                CodigoBarras = reader.IsDBNull(ordCodigo) ? null : reader.GetString(ordCodigo),
                ImagenRuta = reader.IsDBNull(ordImagen) ? null : reader.GetString(ordImagen),
                Estado = reader.GetString("estado"),
                NombreCategoria = reader.GetString("nombre_categoria"),
                IdVersionRecetaActiva = reader.IsDBNull(ordVersion) ? null : reader.GetInt32(ordVersion)
            };
        }

        private static void AgregarParametrosProducto(MySqlCommand cmd, Producto producto)
        {
            cmd.Parameters.AddWithValue("@id_categoria", producto.IdCategoria);
            cmd.Parameters.AddWithValue("@nombre", producto.Nombre.Trim());
            cmd.Parameters.AddWithValue("@precio", producto.Precio);
            // NULL en vez de '' para no violar el índice UNIQUE de codigo_barras
            cmd.Parameters.AddWithValue("@codigo_barras",
                string.IsNullOrWhiteSpace(producto.CodigoBarras) ? DBNull.Value : (object)producto.CodigoBarras.Trim());
            cmd.Parameters.AddWithValue("@imagen_ruta",
                string.IsNullOrWhiteSpace(producto.ImagenRuta) ? DBNull.Value : (object)producto.ImagenRuta.Trim());
        }
    }
}