using System;

namespace CapaEntidades
{
    public class Proveedor
    {
        public int IdProveedor { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public bool Estado { get; set; } = true;
    }

    public class Recepcion
    {
        public int IdRecepcion { get; set; }
        public int IdProveedor { get; set; }
        public int IdUsuario { get; set; }
        public DateTime FechaRecepcion { get; set; }
        public string? Observaciones { get; set; }
    }

    public class Lote
    {
        public int IdLote { get; set; }
        public int IdRecepcion { get; set; }
        public int IdInsumo { get; set; }
        public string CodigoLote { get; set; } = string.Empty;
        public string CodigoBarras { get; set; } = string.Empty;
        public DateTime FechaRecepcion { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadInicial { get; set; }
        public decimal CantidadDisponible { get; set; }
        public decimal CostoUnitario { get; set; }

        // Propiedades auxiliares
        public string NombreInsumo { get; set; } = string.Empty;
        public string UnidadAbreviatura { get; set; } = string.Empty;
        public string NombreProveedor { get; set; } = string.Empty;
        public decimal StockMaximoInsumo { get; set; }
    }

    public class Merma
    {
        public int IdMerma { get; set; }
        public int IdInsumo { get; set; }
        public int? IdLote { get; set; }
        public int IdUsuarioRegistra { get; set; }
        public int? IdUsuarioAutoriza { get; set; }
        public decimal Cantidad { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaMerma { get; set; }
        public decimal CostoEstimado { get; set; }
        public string Estado { get; set; } = "Pendiente"; // Pendiente | Autorizada | Rechazada

        // Propiedades auxiliares
        public string NombreInsumo { get; set; } = string.Empty;
        public string CodigoLote { get; set; } = string.Empty;
        public string NombreUsuarioRegistra { get; set; } = string.Empty;
        public string UnidadAbreviatura { get; set; } = string.Empty;
    }

    public class MovimientoInventario
    {
        public int IdMovimiento { get; set; }
        public int IdLote { get; set; }
        public int IdUsuario { get; set; }
        public int? IdDetalleVenta { get; set; }
        public int? IdMerma { get; set; }
        public int? IdMovimientoOrigen { get; set; }
        public string Tipo { get; set; } = string.Empty; // ENTREGA | CONSUMO_VENTA | MERMA | REVERSION
        public decimal Cantidad { get; set; }
        public DateTime FechaMovimiento { get; set; }
        public string? Observaciones { get; set; }
    }

    /// <summary>Renglón del kardex de un lote (RF-03): movimiento con su origen y el saldo resultante.</summary>
    public class MovimientoKardex
    {
        public int IdMovimiento { get; set; }
        public DateTime FechaMovimiento { get; set; }
        public string Tipo { get; set; } = string.Empty; // ENTREGA | CONSUMO_VENTA | MERMA | REVERSION
        public decimal Cantidad { get; set; }             // Siempre positiva, como se guarda en MySQL
        public string Usuario { get; set; } = string.Empty;
        public string? Observaciones { get; set; }

        // Datos del documento de origen (según el tipo)
        public int? IdRecepcion { get; set; }
        public string? Proveedor { get; set; }
        public string? FolioVenta { get; set; }
        public string? Producto { get; set; }
        public int? IdMerma { get; set; }
        public string? MotivoMerma { get; set; }

        // Calculados en la capa de lógica
        public bool EsEntrada { get; set; }
        public decimal Saldo { get; set; }
        public string TipoDescripcion { get; set; } = string.Empty;
        public string Referencia { get; set; } = string.Empty;
    }
}