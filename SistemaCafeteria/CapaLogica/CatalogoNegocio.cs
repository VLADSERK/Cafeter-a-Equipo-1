using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class CatalogoNegocio
    {
        private readonly CatalogoDatos _datos = new();
        private readonly InventarioDatos _inventarioDatos = new();

        // =====================================================================
        // CONSULTAS (las puede usar cualquier usuario con sesión)
        // =====================================================================

        public List<Categoria> ObtenerCategorias()
            => OrdenAlfabetico.Por(_datos.ObtenerCategorias(), c => c.Nombre);

        /// <summary>Productos para la pantalla de catálogo (Administrador), en orden alfabético por nombre.</summary>
        public List<Producto> ObtenerProductos(bool soloActivos = false, int? idCategoria = null)
            => OrdenAlfabetico.Por(_datos.ObtenerProductos(soloActivos, idCategoria), p => p.Nombre);

        /// <summary>
        /// Productos que se pueden vender: activos y con receta vigente (para el Punto de Venta).
        /// </summary>
        public List<Producto> ObtenerProductosParaVenta(int? idCategoria = null)
            => _datos.ObtenerProductos(true, idCategoria)
                     .Where(p => p.IdVersionRecetaActiva.HasValue)
                     .ToList();

        public Producto? ObtenerProductoConReceta(int idProducto) => _datos.ObtenerProductoConReceta(idProducto);

        public List<VersionReceta> ObtenerVersionesReceta(int idProducto) => _datos.ObtenerVersionesReceta(idProducto);

        public List<DetalleReceta> ObtenerDetalleReceta(int idVersionReceta) => _datos.ObtenerDetalleReceta(idVersionReceta);

        /// <summary>Insumos activos para elegir ingredientes de una receta, en orden alfabético.</summary>
        public List<Insumo> ObtenerInsumosParaReceta()
            => OrdenAlfabetico.Por(_inventarioDatos.ObtenerInsumosConStock(), i => i.Nombre);

        /// <summary>Ordena las líneas de una receta por el nombre del insumo (en el mismo objeto lista).</summary>
        public static void OrdenarLineasReceta(List<DetalleReceta> lineas)
            => lineas.Sort((a, b) => OrdenAlfabetico.Nombres.Compare(a.NombreInsumo, b.NombreInsumo));

        public List<UnidadMedida> ObtenerUnidadesMedida() => _inventarioDatos.ObtenerUnidadesMedida();

        /// <summary>
        /// Costo de referencia por insumo (en la unidad del insumo): promedio ponderado de los lotes
        /// con existencia; si ya no hay existencia, el costo del lote más reciente.
        /// Los insumos que nunca han tenido lotes no aparecen en el diccionario.
        /// </summary>
        public Dictionary<int, decimal> ObtenerCostosReferencia()
        {
            var resultado = new Dictionary<int, decimal>();
            var lotes = _inventarioDatos.ObtenerLotesPeps(false);

            foreach (var grupo in lotes.GroupBy(l => l.IdInsumo))
            {
                var conStock = grupo.Where(l => l.CantidadDisponible > 0).ToList();
                decimal totalDisponible = conStock.Sum(l => l.CantidadDisponible);

                decimal costo = totalDisponible > 0
                    ? conStock.Sum(l => l.CantidadDisponible * l.CostoUnitario) / totalDisponible
                    : grupo.OrderByDescending(l => l.FechaRecepcion).First().CostoUnitario;

                // Costo de referencia intermedio: 4 decimales para no perder precisión antes
                // de multiplicarlo por la cantidad de la receta (el importe final se redondea a 2).
                resultado[grupo.Key] = Redondeo.A(costo, 4);
            }

            return resultado;
        }

        /// <summary>
        /// Factor para convertir una cantidad de la unidad "desde" a la unidad "hacia"
        /// (por ejemplo g → kg = 0.001). Si son de distinto tipo devuelve 1.
        /// </summary>
        public static decimal FactorConversion(UnidadMedida desde, UnidadMedida hacia)
        {
            if (desde.IdUnidad == hacia.IdUnidad)
                return 1m;

            return ConversionUnidades.Factor(desde.Abreviatura, desde.Tipo, hacia.Abreviatura, hacia.Tipo);
        }

        // =====================================================================
        // PRODUCTOS (solo Administrador)
        // =====================================================================

        /// <summary>
        /// Crea el producto si IdProducto = 0, o lo actualiza si ya existe.
        /// Devuelve el id del producto guardado.
        /// </summary>
        public (bool Exito, string Mensaje, int IdProducto) GuardarProducto(Producto producto)
        {
            var permiso = ValidarAdministrador();
            if (!permiso.Exito)
                return (false, permiso.Mensaje, 0);

            if (producto == null)
                return (false, "No se recibió información del producto.", 0);

            if (string.IsNullOrWhiteSpace(producto.Nombre))
                return (false, "El nombre del producto es obligatorio.", 0);

            if (producto.Nombre.Trim().Length > 100)
                return (false, "El nombre del producto no puede pasar de 100 caracteres.", 0);

            if (producto.IdCategoria <= 0)
                return (false, "Selecciona una categoría.", 0);

            if (producto.Precio <= 0)
                return (false, "El precio debe ser mayor a cero.", 0);

            if (Redondeo.ExcedeDecimales(producto.Precio, Redondeo.DecimalesImporte))
                return (false, "El precio admite como máximo 2 decimales (centavos).", 0);

            try
            {
                if (_datos.ExisteNombreProducto(producto.Nombre, producto.IdProducto))
                    return (false, $"Ya existe un producto llamado \"{producto.Nombre.Trim()}\".", 0);

                if (!string.IsNullOrWhiteSpace(producto.CodigoBarras)
                    && _datos.ExisteCodigoBarrasProducto(producto.CodigoBarras, producto.IdProducto))
                    return (false, "Ese código de barras ya está asignado a otro producto.", 0);

                if (producto.IdProducto == 0)
                {
                    int nuevoId = _datos.InsertarProducto(producto);
                    return (true, $"Producto \"{producto.Nombre.Trim()}\" creado. Ahora agrégale su receta para poder venderlo.", nuevoId);
                }

                _datos.ActualizarProducto(producto);
                return (true, $"Producto \"{producto.Nombre.Trim()}\" actualizado.", producto.IdProducto);
            }
            catch (Exception ex)
            {
                return (false, $"Error al guardar el producto en MySQL:\n{ex.Message}", 0);
            }
        }

        public (bool Exito, string Mensaje) CambiarEstadoProducto(int idProducto, bool activo)
        {
            var permiso = ValidarAdministrador();
            if (!permiso.Exito)
                return permiso;

            if (idProducto <= 0)
                return (false, "Selecciona un producto válido.");

            try
            {
                _datos.CambiarEstadoProducto(idProducto, activo);
                return (true, activo ? "Producto activado." : "Producto desactivado. Ya no aparecerá en el Punto de Venta.");
            }
            catch (Exception ex)
            {
                return (false, $"Error al cambiar el estado del producto en MySQL:\n{ex.Message}");
            }
        }

        // =====================================================================
        // RECETAS (solo Administrador)
        // =====================================================================

        /// <summary>
        /// Guarda la receta como una versión nueva. Valida que no esté vacía, que no se repitan
        /// insumos, que las cantidades sean mayores a cero y que la unidad sea del mismo tipo
        /// que la del insumo (masa con masa, volumen con volumen, pieza con pieza).
        /// </summary>
        public (bool Exito, string Mensaje, int IdVersionReceta) GuardarReceta(int idProducto, List<DetalleReceta> ingredientes)
        {
            var permiso = ValidarAdministrador();
            if (!permiso.Exito)
                return (false, permiso.Mensaje, 0);

            if (idProducto <= 0)
                return (false, "Primero guarda el producto antes de capturar su receta.", 0);

            if (ingredientes == null || ingredientes.Count == 0)
                return (false, "La receta debe tener al menos un ingrediente.", 0);

            if (ingredientes.Any(i => i.Cantidad <= 0))
                return (false, "Todas las cantidades de la receta deben ser mayores a cero.", 0);

            if (ingredientes.Any(i => Redondeo.ExcedeDecimales(i.Cantidad, Redondeo.DecimalesCantidad)))
                return (false, "Las cantidades de la receta admiten como máximo 3 decimales.", 0);

            var repetido = ingredientes.GroupBy(i => i.IdInsumo).FirstOrDefault(g => g.Count() > 1);
            if (repetido != null)
                return (false, "Hay un insumo repetido en la receta. Junta las cantidades en una sola línea.", 0);

            try
            {
                var unidades = _inventarioDatos.ObtenerUnidadesMedida().ToDictionary(u => u.IdUnidad);
                var insumos = _inventarioDatos.ObtenerInsumosConStock().ToDictionary(i => i.IdInsumo);

                foreach (var ing in ingredientes)
                {
                    if (!insumos.TryGetValue(ing.IdInsumo, out var insumo))
                        return (false, "Uno de los insumos de la receta no existe o está inactivo.", 0);

                    if (!unidades.TryGetValue(ing.IdUnidad, out var unidadReceta))
                        return (false, $"Selecciona una unidad válida para \"{insumo.Nombre}\".", 0);

                    if (unidades.TryGetValue(insumo.IdUnidad, out var unidadInsumo)
                        && !string.Equals(unidadReceta.Tipo, unidadInsumo.Tipo, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false,
                            $"La unidad \"{unidadReceta.Abreviatura}\" no es compatible con \"{insumo.Nombre}\", " +
                            $"que se maneja en \"{unidadInsumo.Abreviatura}\" ({unidadInsumo.Tipo}).", 0);
                    }
                }

                int idVersion = _datos.GuardarNuevaVersionReceta(idProducto, ingredientes);
                return (true, "Receta guardada como nueva versión. Las ventas anteriores conservan su receta original.", idVersion);
            }
            catch (Exception ex)
            {
                return (false, $"Error al guardar la receta en MySQL:\n{ex.Message}", 0);
            }
        }

        // =====================================================================
        // AUXILIARES
        // =====================================================================

        private static (bool Exito, string Mensaje) ValidarAdministrador()
            => Permisos.Verificar(Operacion.AdministrarCatalogo);
    }
}