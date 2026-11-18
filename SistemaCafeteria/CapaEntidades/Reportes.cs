using System;
using System.Collections.Generic;

namespace CapaEntidades
{
    /// <summary>5.1 Reporte de ventas: una fila por venta del periodo (incluye canceladas, marcadas).</summary>
    public class ReporteVentaFila
    {
        public int IdVenta { get; set; }
        public string Folio { get; set; } = string.Empty;
        public DateTime FechaVenta { get; set; }
        public string Cajero { get; set; } = string.Empty;
        public string? MetodoPago { get; set; }
        public string Productos { get; set; } = string.Empty;   // "2x Latte, 1x Americano"
        public int Articulos { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;      // Confirmada | Cancelada
    }

    /// <summary>5.2 Reporte de consumo: insumo consumido por ventas confirmadas, desglosado por lote.</summary>
    public class ReporteConsumoFila
    {
        public int IdInsumo { get; set; }
        public string Insumo { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public string CodigoLote { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal CostoTotal { get; set; }
    }

    /// <summary>5.3 Reporte de existencias: insumo al cierre del periodo, valorizado por lote.</summary>
    public class ReporteExistenciaFila
    {
        public int IdInsumo { get; set; }
        public string Insumo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public int Lotes { get; set; }
        public decimal Existencia { get; set; }
        public decimal StockMaximo { get; set; }
        public decimal Porcentaje { get; set; }
        public string Estado { get; set; } = string.Empty;      // Verde | Amarillo | Rojo | Blanco
        public decimal ValorTotal { get; set; }
        public decimal EntradasPeriodo { get; set; }            // ENTREGA + REVERSION
        public decimal SalidasPeriodo { get; set; }             // CONSUMO_VENTA + MERMA
    }

    /// <summary>5.4 Reporte de mermas registradas en el periodo.</summary>
    public class ReporteMermaFila
    {
        public int IdMerma { get; set; }
        public DateTime FechaMerma { get; set; }
        public string Insumo { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public string CodigoLote { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public decimal CostoEstimado { get; set; }
        public string Estado { get; set; } = string.Empty;      // Pendiente | Autorizada | Rechazada
        public string RegistradoPor { get; set; } = string.Empty;
        public string? AutorizadoPor { get; set; }
    }

    /// <summary>Reporte financiero completo de un periodo (RF-10). Importes en MXN, Half-Up (RNF-08).</summary>
    public class ReporteFinanciero
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public DateTime GeneradoEn { get; set; }

        public List<ReporteVentaFila> Ventas { get; set; } = new();
        public List<ReporteConsumoFila> Consumo { get; set; } = new();
        public List<ReporteExistenciaFila> Existencias { get; set; } = new();
        public List<ReporteMermaFila> Mermas { get; set; } = new();

        // 5.1 Ventas (los totales netos excluyen canceladas)
        public decimal VentasNetas { get; set; }
        public int TicketsConfirmados { get; set; }
        public int ArticulosVendidos { get; set; }
        public int VentasCanceladas { get; set; }
        public decimal ImporteCancelado { get; set; }

        // 5.2 Consumo
        public decimal ConsumoTotal { get; set; }
        public int InsumosConsumidos { get; set; }

        // 5.3 Existencias al cierre
        public decimal ExistenciasValor { get; set; }
        public int InsumosActivos { get; set; }
        public int InsumosConExistencia { get; set; }
        public int InsumosPorReabastecer { get; set; }          // Amarillo + Rojo + Blanco

        // 5.4 Mermas
        public decimal MermasAutorizadasImporte { get; set; }
        public int MermasAutorizadas { get; set; }
        public int MermasPendientes { get; set; }
        public int MermasRechazadas { get; set; }

        /// <summary>Ventas netas − consumo real − mermas autorizadas.</summary>
        public decimal UtilidadBruta { get; set; }
    }

    /// <summary>Indicadores del dashboard del Administrador (pantalla 7).</summary>
    public class ResumenDashboard
    {
        public decimal VentasHoy { get; set; }
        public int TicketsHoy { get; set; }
        public decimal UtilidadBrutaHoy { get; set; }
        public int InsumosAmarillo { get; set; }
        public int InsumosRojo { get; set; }
        public int InsumosBlanco { get; set; }
        public int MermasPendientes { get; set; }

        /// <summary>Insumos que requieren reabastecimiento (RIU-03).</summary>
        public int AlertasInventario => InsumosAmarillo + InsumosRojo + InsumosBlanco;
    }
}
