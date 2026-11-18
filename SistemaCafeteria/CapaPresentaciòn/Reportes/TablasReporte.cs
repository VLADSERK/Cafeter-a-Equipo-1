using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CapaEntidades;

namespace CapaPresentaciòn.Reportes
{
    public enum SeccionReporte
    {
        Ventas,
        Consumo,
        Existencias,
        Mermas
    }

    public sealed class ColumnaReporte
    {
        public ColumnaReporte(string titulo, float peso, bool alinearDerecha = false)
        {
            Titulo = titulo;
            Peso = peso;
            AlinearDerecha = alinearDerecha;
        }

        public string Titulo { get; }
        public float Peso { get; }
        public bool AlinearDerecha { get; }
    }

    /// <summary>Tabla ya formateada (texto) que se muestra igual en pantalla y en el PDF.</summary>
    public sealed class TablaReporte
    {
        public string Titulo { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public List<ColumnaReporte> Columnas { get; init; } = new();
        public List<string[]> Filas { get; init; } = new();
        public string[]? Totales { get; init; }

        /// <summary>Índice de la columna "Estado" o "Semáforo" para colorearla; -1 si no tiene.</summary>
        public int ColumnaEstado { get; init; } = -1;
    }

    /// <summary>
    /// Construye las tablas de los reportes 5.1–5.4 y el resumen consolidado a partir
    /// de un ReporteFinanciero. Solo da formato; los cálculos los hace ReporteNegocio.
    /// </summary>
    public static class TablasReporte
    {
        private static readonly CultureInfo Mxn = CultureInfo.GetCultureInfo("es-MX");

        public static string Dinero(decimal valor) => valor.ToString("C2", Mxn);

        /// <summary>"1 venta", "2 ventas" (plural regular del español).</summary>
        private static string N(int n, string singular, string? plural = null) =>
            $"{n.ToString("N0", Mxn)} {(n == 1 ? singular : plural ?? singular + "s")}";
        private static string Cant(decimal valor, string unidad) => CapaPresentacion.Estilos.FormatoInventario.Cantidad(valor, unidad);

        public static string NombreSeccion(SeccionReporte s) => s switch
        {
            SeccionReporte.Ventas => "Ventas netas",
            SeccionReporte.Consumo => "Consumo real de ingredientes",
            SeccionReporte.Existencias => "Existencias al cierre",
            _ => "Mermas y pérdidas"
        };

        // =====================================================================
        // RESUMEN CONSOLIDADO (prototipo pantalla 13)
        // =====================================================================

        public static TablaReporte Resumen(ReporteFinanciero r) => new()
        {
            Titulo = "Detalle consolidado del periodo seleccionado",
            Descripcion = $"Utilidad bruta del periodo (ventas netas − consumo − mermas autorizadas): {Dinero(r.UtilidadBruta)}",
            Columnas = new()
            {
                new("Categoría de reporte", 22),
                new("Descripción", 34),
                new("Volumen / unidades", 22),
                new("Costo / valor total", 14, true),
                new("Estado", 10)
            },
            ColumnaEstado = 4,
            Filas = new()
            {
                new[]
                {
                    NombreSeccion(SeccionReporte.Ventas),
                    "Ingresos del periodo descontando cancelaciones de caja",
                    $"{N(r.TicketsConfirmados, "ticket")} · {N(r.ArticulosVendidos, "artículo")}",
                    Dinero(r.VentasNetas),
                    "Verificado"
                },
                new[]
                {
                    NombreSeccion(SeccionReporte.Consumo),
                    "Insumos descontados por recetas en ventas confirmadas (costo PEPS)",
                    $"{N(r.InsumosConsumidos, "insumo")} · {N(r.Consumo.Count, "lote")}",
                    Dinero(r.ConsumoTotal),
                    "Calculado"
                },
                new[]
                {
                    NombreSeccion(SeccionReporte.Existencias),
                    $"Inventario valorizado por lote al {r.Hasta:dd/MM/yyyy}",
                    $"{r.InsumosConExistencia:N0} de {r.InsumosActivos:N0} insumos con existencia",
                    Dinero(r.ExistenciasValor),
                    "Actualizado"
                },
                new[]
                {
                    NombreSeccion(SeccionReporte.Mermas),
                    "Pérdidas autorizadas (las pendientes y rechazadas no descuentan)",
                    $"{N(r.MermasAutorizadas, "autorizada")} · {N(r.MermasPendientes, "pendiente")} · {N(r.MermasRechazadas, "rechazada")}",
                    Dinero(r.MermasAutorizadasImporte),
                    "Autorizado"
                }
            }
        };

        // =====================================================================
        // DETALLES 5.1–5.4
        // =====================================================================

        public static TablaReporte Detalle(SeccionReporte seccion, ReporteFinanciero r) => seccion switch
        {
            SeccionReporte.Ventas => Ventas(r),
            SeccionReporte.Consumo => Consumo(r),
            SeccionReporte.Existencias => Existencias(r),
            _ => Mermas(r)
        };

        private static TablaReporte Ventas(ReporteFinanciero r) => new()
        {
            Titulo = "5.1 Reporte de ventas",
            Descripcion = $"{N(r.TicketsConfirmados, "venta confirmada", "ventas confirmadas")} · {N(r.VentasCanceladas, "cancelada")} por {Dinero(r.ImporteCancelado)} (excluidas del total neto)",
            Columnas = new()
            {
                new("Folio", 9), new("Fecha y hora", 12), new("Cajero", 13), new("Productos", 34),
                new("Arts.", 5, true), new("Pago", 8), new("Estado", 9), new("Total", 10, true)
            },
            ColumnaEstado = 6,
            Filas = r.Ventas.Select(v => new[]
            {
                v.Folio, v.FechaVenta.ToString("dd/MM/yyyy HH:mm", Mxn), v.Cajero, v.Productos,
                v.Articulos.ToString("N0", Mxn), v.MetodoPago ?? "—", v.Estado, Dinero(v.Total)
            }).ToList(),
            Totales = new[] { "Ventas netas", "", "", "Excluye ventas canceladas", r.ArticulosVendidos.ToString("N0", Mxn), "", "", Dinero(r.VentasNetas) }
        };

        private static TablaReporte Consumo(ReporteFinanciero r) => new()
        {
            Titulo = "5.2 Reporte de consumo de insumos",
            Descripcion = "Movimientos CONSUMO_VENTA de ventas confirmadas, por lote PEPS y costo unitario de cada lote",
            Columnas = new()
            {
                new("Insumo", 30), new("Lote PEPS", 20), new("Cantidad consumida", 18, true),
                new("Costo unitario", 15, true), new("Costo total", 15, true)
            },
            Filas = r.Consumo.Select(c => new[]
            {
                c.Insumo, c.CodigoLote, Cant(c.Cantidad, c.Unidad), Dinero(c.CostoUnitario), Dinero(c.CostoTotal)
            }).ToList(),
            Totales = new[] { "Consumo total", "", "", "", Dinero(r.ConsumoTotal) }
        };

        private static TablaReporte Existencias(ReporteFinanciero r) => new()
        {
            Titulo = "5.3 Reporte de existencias",
            Descripcion = $"Existencias al cierre del {r.Hasta:dd/MM/yyyy}; entradas y salidas del periodo. Semáforo: % del stock máximo",
            Columnas = new()
            {
                new("Insumo", 19), new("Categoría", 13), new("Lotes", 5, true), new("Existencia", 10, true),
                new("Stock máx.", 9, true), new("% actual", 7, true), new("Semáforo", 8),
                new("Entradas", 9, true), new("Salidas", 9, true), new("Valor", 11, true)
            },
            ColumnaEstado = 6,
            Filas = r.Existencias
                .OrderBy(e => OrdenSemaforo(e.Estado)).ThenBy(e => e.Insumo)
                .Select(e => new[]
                {
                    e.Insumo, e.Categoria, e.Lotes.ToString("N0", Mxn), Cant(e.Existencia, e.Unidad),
                    Cant(e.StockMaximo, e.Unidad), $"{e.Porcentaje.ToString("0.0", Mxn)} %", e.Estado,
                    Cant(e.EntradasPeriodo, e.Unidad), Cant(e.SalidasPeriodo, e.Unidad), Dinero(e.ValorTotal)
                }).ToList(),
            Totales = new[] { "Valor total del inventario", "", "", "", "", "", "", "", "", Dinero(r.ExistenciasValor) }
        };

        private static TablaReporte Mermas(ReporteFinanciero r) => new()
        {
            Titulo = "5.4 Reporte de mermas",
            Descripcion = $"{N(r.MermasAutorizadas, "autorizada")} · {N(r.MermasPendientes, "pendiente")} · {N(r.MermasRechazadas, "rechazada")}. Solo las autorizadas cuentan como pérdida",
            Columnas = new()
            {
                new("Fecha", 11), new("Insumo", 15), new("Lote", 11), new("Cantidad", 9, true),
                new("Motivo", 17), new("Registró", 11), new("Estado", 9), new("Autorizó", 10), new("Costo est.", 9, true)
            },
            ColumnaEstado = 6,
            Filas = r.Mermas.Select(m => new[]
            {
                m.FechaMerma.ToString("dd/MM/yyyy HH:mm", Mxn), m.Insumo, m.CodigoLote, Cant(m.Cantidad, m.Unidad),
                m.Motivo, m.RegistradoPor, m.Estado,
                m.AutorizadoPor ?? (m.Estado == "Autorizada" ? "Automática (≤ límite)" : "—"),
                Dinero(m.CostoEstimado)
            }).ToList(),
            Totales = new[] { "Total autorizado", "", "", "", "", "", "", "", Dinero(r.MermasAutorizadasImporte) }
        };

        private static int OrdenSemaforo(string estado) => estado switch
        {
            "Blanco" => 0,
            "Rojo" => 1,
            "Amarillo" => 2,
            _ => 3
        };
    }
}
