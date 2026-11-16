using System;
using System.Collections.Generic;

namespace CapaEntidades
{
    public class Venta
    {
        public int IdVenta { get; set; }
        public int IdUsuario { get; set; }
        public string Folio { get; set; } = string.Empty;
        public DateTime FechaVenta { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = "Confirmada"; // Confirmada | Cancelada
        public string? MetodoPago { get; set; }
        public decimal? MontoRecibido { get; set; }
        public decimal? Cambio { get; set; }
        public string? MotivoCancelacion { get; set; }

        // Propiedades auxiliares
        public string NombreCajero { get; set; } = string.Empty;
        public List<DetalleVenta> Detalles { get; set; } = new();
    }

    public class DetalleVenta
    {
        public int IdDetalleVenta { get; set; }
        public int IdVenta { get; set; }
        public int IdProducto { get; set; }
        public int IdVersionReceta { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }

        // Propiedad auxiliar
        public string NombreProducto { get; set; } = string.Empty;
    }

    public class HistorialRecetaVenta
    {
        public int IdHistorial { get; set; }
        public int IdDetalleVenta { get; set; }
        public int IdInsumo { get; set; }
        public int IdLote { get; set; }
        public decimal CantidadConsumida { get; set; }
        public decimal CostoUnitario { get; set; }
    }
}