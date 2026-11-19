using System;
using System.Collections.Generic;
using System.Linq;
using CapaEntidades;

namespace CapaLogica
{
    /// <summary>
    /// Cuántas unidades de cada producto caben en la comanda actual (Punto de venta).
    ///
    /// Máximo de un producto = el menor entre:
    ///   • el límite fijo por producto en cada venta (<see cref="LimitePorProducto"/>), y
    ///   • las unidades que alcanzan los insumos de su receta vigente, descontando lo que ya
    ///     consumen los demás productos de la comanda (un latte y un capuchino comparten la leche).
    ///
    /// Aplica el mismo cálculo que el descuento real (RF-06): consumo = cantidad de receta × factor
    /// de unidad × unidades, redondeado a 3 decimales por producto e insumo, contra la suma de los
    /// lotes con existencia. Es una guía para la pantalla; la venta vuelve a validar existencias
    /// dentro de su transacción con bloqueo de filas (RNF-02 / RNF-05).
    /// </summary>
    public sealed class DisponibilidadVenta
    {
        private readonly Dictionary<int, List<(int IdInsumo, decimal ConsumoPorUnidad)>> _recetas;
        private readonly Dictionary<int, decimal> _existencias;

        public int LimitePorProducto { get; }

        internal DisponibilidadVenta(IEnumerable<ConsumoRecetaInsumo> consumos, int limitePorProducto)
        {
            LimitePorProducto = limitePorProducto;

            var lista = consumos.ToList();
            _recetas = lista
                .GroupBy(c => c.IdProducto)
                .ToDictionary(g => g.Key, g => g.Select(c => (c.IdInsumo, c.ConsumoPorUnidad)).ToList());

            _existencias = new Dictionary<int, decimal>();
            foreach (var c in lista)
                _existencias[c.IdInsumo] = c.Existencia;
        }

        /// <summary>Sin datos de inventario: solo aplica el límite por producto (la venta valida existencias al confirmar).</summary>
        public static DisponibilidadVenta SoloLimite(int limitePorProducto)
            => new(Array.Empty<ConsumoRecetaInsumo>(), limitePorProducto);

        /// <summary>
        /// Máximo de unidades del producto que puede tener la comanda, dadas las cantidades
        /// actuales de los demás productos. <paramref name="comanda"/> es IdProducto → cantidad.
        /// </summary>
        public int MaximoPara(int idProducto, IReadOnlyDictionary<int, int> comanda)
        {
            // Sin receta registrada no hay insumos que limiten: la venta decidirá (igual que hoy)
            if (!_recetas.TryGetValue(idProducto, out var receta))
                return LimitePorProducto;

            var consumoOtros = new Dictionary<int, decimal>();
            foreach (var (idOtro, cantidad) in comanda)
            {
                if (idOtro == idProducto || cantidad <= 0 || !_recetas.TryGetValue(idOtro, out var recetaOtro))
                    continue;

                foreach (var (idInsumo, consumo) in recetaOtro)
                {
                    consumoOtros.TryGetValue(idInsumo, out decimal acumulado);
                    consumoOtros[idInsumo] = acumulado + Redondeo.Cantidad(consumo * cantidad);
                }
            }

            int maximo = 0;
            for (int unidades = 1; unidades <= LimitePorProducto; unidades++)
            {
                if (!Alcanza(receta, unidades, consumoOtros))
                    break;
                maximo = unidades;
            }
            return maximo;
        }

        /// <summary>Unidades que todavía se pueden agregar del producto (0 si ya llegó al máximo).</summary>
        public int RestantePara(int idProducto, IReadOnlyDictionary<int, int> comanda)
        {
            comanda.TryGetValue(idProducto, out int actual);
            return Math.Max(0, MaximoPara(idProducto, comanda) - actual);
        }

        private bool Alcanza(List<(int IdInsumo, decimal ConsumoPorUnidad)> receta, int unidades, Dictionary<int, decimal> consumoOtros)
        {
            foreach (var (idInsumo, consumo) in receta)
            {
                decimal requerido = Redondeo.Cantidad(consumo * unidades);
                if (requerido <= 0)
                    continue;

                _existencias.TryGetValue(idInsumo, out decimal existencia);
                consumoOtros.TryGetValue(idInsumo, out decimal usadoPorOtros);

                if (requerido > existencia - usadoPorOtros)
                    return false;
            }
            return true;
        }
    }
}
