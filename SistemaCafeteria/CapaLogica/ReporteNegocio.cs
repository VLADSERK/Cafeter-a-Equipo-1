using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    /// <summary>
    /// Reportes de ventas, consumo, existencias y mermas (RF-10, diseño 5.1–5.4)
    /// e indicadores del dashboard del Administrador.
    /// - Solo el Administrador puede generarlos (RNF-10).
    /// - Los totales netos excluyen ventas canceladas (RF-10).
    /// - Importes en MXN redondeados Half-Up a 2 decimales (RNF-08).
    /// </summary>
    public class ReporteNegocio
    {
        private readonly ReporteDatos _datos = new();
        private readonly InventarioDatos _inventarioDatos = new();
        private readonly ConfiguracionDatos _configDatos = new();
        private readonly InventarioNegocio _inventarioNegocio = new();

        // =====================================================================
        // REPORTE FINANCIERO DE UN PERIODO
        // =====================================================================

        public (bool Exito, string Mensaje, ReporteFinanciero? Reporte) GenerarReporte(DateTime desde, DateTime hasta)
        {
            var permiso = ValidarAdministrador();
            if (!permiso.Exito)
                return (false, permiso.Mensaje, null);

            // RIU-06: rango inválido → mensaje de error y no se consulta
            if (desde.Date > hasta.Date)
                return (false, "La fecha inicial no puede ser mayor a la fecha final.", null);

            if (desde.Date > DateTime.Today)
                return (false, "La fecha inicial no puede estar en el futuro.", null);

            try
            {
                return (true, string.Empty, Construir(desde.Date, hasta.Date));
            }
            catch (Exception ex)
            {
                return (false, $"Error al generar el reporte desde MySQL:\n{ex.Message}", null);
            }
        }

        // =====================================================================
        // DASHBOARD
        // =====================================================================

        public (bool Exito, string Mensaje, ResumenDashboard? Resumen) ObtenerResumenDashboard()
        {
            var permiso = ValidarAdministrador();
            if (!permiso.Exito)
                return (false, permiso.Mensaje, null);

            try
            {
                var hoy = Construir(DateTime.Today, DateTime.Today);

                // Alertas con el estado ACTUAL del inventario (mismo cálculo que la pantalla de inventario)
                var inventario = _inventarioNegocio.ObtenerResumenInventarioCompleto();

                return (true, string.Empty, new ResumenDashboard
                {
                    VentasHoy = hoy.VentasNetas,
                    TicketsHoy = hoy.TicketsConfirmados,
                    UtilidadBrutaHoy = hoy.UtilidadBruta,
                    InsumosAmarillo = inventario.Count(i => i.Semaforo == NivelSemaforo.Amarillo),
                    InsumosRojo = inventario.Count(i => i.Semaforo == NivelSemaforo.Rojo),
                    InsumosBlanco = inventario.Count(i => i.Semaforo == NivelSemaforo.Blanco),
                    MermasPendientes = new AutorizacionMermaDatos().ObtenerMermasPendientes().Count
                });
            }
            catch (Exception ex)
            {
                return (false, $"Error al consultar los indicadores en MySQL:\n{ex.Message}", null);
            }
        }

        // =====================================================================
        // CONSTRUCCIÓN
        // =====================================================================

        private ReporteFinanciero Construir(DateTime desde, DateTime hasta)
        {
            DateTime hastaExclusivo = hasta.AddDays(1);
            var reporte = new ReporteFinanciero
            {
                Desde = desde,
                Hasta = hasta,
                GeneradoEn = DateTime.Now
            };

            // 5.1 Ventas
            reporte.Ventas = _datos.ObtenerVentas(desde, hastaExclusivo);
            var confirmadas = reporte.Ventas.Where(v => v.Estado == "Confirmada").ToList();
            var canceladas = reporte.Ventas.Where(v => v.Estado == "Cancelada").ToList();
            reporte.VentasNetas = Redondeo.Importe(confirmadas.Sum(v => v.Total));
            reporte.TicketsConfirmados = confirmadas.Count;
            reporte.ArticulosVendidos = confirmadas.Sum(v => v.Articulos);
            reporte.VentasCanceladas = canceladas.Count;
            reporte.ImporteCancelado = Redondeo.Importe(canceladas.Sum(v => v.Total));

            // 5.2 Consumo (costo PEPS por lote, redondeado por línea)
            reporte.Consumo = _datos.ObtenerConsumo(desde, hastaExclusivo);
            foreach (var c in reporte.Consumo)
                c.CostoTotal = Redondeo.Importe(c.Cantidad * c.CostoUnitario);
            reporte.ConsumoTotal = Redondeo.Importe(reporte.Consumo.Sum(c => c.CostoTotal));
            reporte.InsumosConsumidos = reporte.Consumo.Select(c => c.IdInsumo).Distinct().Count();

            // 5.3 Existencias al cierre del periodo
            reporte.Existencias = ConstruirExistencias(desde, hastaExclusivo);
            reporte.ExistenciasValor = Redondeo.Importe(reporte.Existencias.Sum(e => e.ValorTotal));
            reporte.InsumosActivos = reporte.Existencias.Count;
            reporte.InsumosConExistencia = reporte.Existencias.Count(e => e.Existencia > 0);
            reporte.InsumosPorReabastecer = reporte.Existencias.Count(e => e.Estado != "Verde");

            // 5.4 Mermas (solo las autorizadas son pérdida real de inventario)
            reporte.Mermas = _datos.ObtenerMermas(desde, hastaExclusivo);
            var autorizadas = reporte.Mermas.Where(m => m.Estado == "Autorizada").ToList();
            reporte.MermasAutorizadasImporte = Redondeo.Importe(autorizadas.Sum(m => m.CostoEstimado));
            reporte.MermasAutorizadas = autorizadas.Count;
            reporte.MermasPendientes = reporte.Mermas.Count(m => m.Estado == "Pendiente");
            reporte.MermasRechazadas = reporte.Mermas.Count(m => m.Estado == "Rechazada");

            reporte.UtilidadBruta = Redondeo.Importe(reporte.VentasNetas - reporte.ConsumoTotal - reporte.MermasAutorizadasImporte);
            return reporte;
        }

        private List<ReporteExistenciaFila> ConstruirExistencias(DateTime desde, DateTime hastaExclusivo)
        {
            var config = _configDatos.ObtenerConfiguracion();
            var insumos = _inventarioDatos.ObtenerInsumosConStock();
            var lotesPorInsumo = _datos.ObtenerLotesAlCierre(hastaExclusivo)
                .GroupBy(l => l.IdInsumo)
                .ToDictionary(g => g.Key, g => g.ToList());
            var movimientos = _datos.ObtenerMovimientosPorInsumo(desde, hastaExclusivo);

            var filas = new List<ReporteExistenciaFila>();
            foreach (var ins in insumos)
            {
                var lotes = lotesPorInsumo.TryGetValue(ins.IdInsumo, out var l) ? l : new List<Lote>();
                decimal existencia = Redondeo.Cantidad(lotes.Sum(x => x.CantidadDisponible));
                movimientos.TryGetValue(ins.IdInsumo, out var mov);

                var semaforo = SemaforoExistencias.Evaluar(existencia, ins.StockMaximo, config);

                filas.Add(new ReporteExistenciaFila
                {
                    IdInsumo = ins.IdInsumo,
                    Insumo = ins.Nombre,
                    Categoria = ins.Categoria,
                    Unidad = ins.UnidadAbreviatura,
                    Lotes = lotes.Count,
                    Existencia = existencia,
                    StockMaximo = ins.StockMaximo,
                    Porcentaje = semaforo.Porcentaje,
                    Estado = semaforo.Etiqueta,   // Verde | Amarillo | Rojo | Blanco (RF-08)
                    ValorTotal = Redondeo.Importe(lotes.Sum(x => Redondeo.Importe(x.CantidadDisponible * x.CostoUnitario))),
                    EntradasPeriodo = mov.Entradas,
                    SalidasPeriodo = mov.Salidas
                });
            }

            return filas;
        }

        private static (bool Exito, string Mensaje) ValidarAdministrador()
        {
            var permiso = Permisos.Verificar(Operacion.GenerarReportes);
            if (!permiso.Exito)
                return permiso;

            return (true, string.Empty);
        }
    }
}
