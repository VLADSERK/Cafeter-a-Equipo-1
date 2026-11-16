using System;
using System.Collections.Generic;

namespace CapaEntidades
{
    public class Categoria
    {
        public int IdCategoria { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class Producto
    {
        public int IdProducto { get; set; }
        public int IdCategoria { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string? CodigoBarras { get; set; }
        public string? ImagenRuta { get; set; }
        public string Estado { get; set; } = "Activo";

        // Propiedades auxiliares
        public string NombreCategoria { get; set; } = string.Empty;
        public int? IdVersionRecetaActiva { get; set; }
        public List<DetalleReceta> RecetaActiva { get; set; } = new();
    }

    public class UnidadMedida
    {
        public int IdUnidad { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Abreviatura { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }

    public class Insumo
    {
        public int IdInsumo { get; set; }
        public int IdUnidad { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal StockMaximo { get; set; }
        public bool Estado { get; set; } = true;
        public string Categoria { get; set; } = "General";
        public decimal StockMinimo { get; set; } = 1.000m;

        // Propiedades auxiliares
        public string UnidadAbreviatura { get; set; } = string.Empty;
        public decimal StockActualTotal { get; set; }
    }

    public class VersionReceta
    {
        public int IdVersionReceta { get; set; }
        public int IdProducto { get; set; }
        public int NumeroVersion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool Estado { get; set; } = true;
    }

    public class DetalleReceta
    {
        public int IdDetalleReceta { get; set; }
        public int IdVersionReceta { get; set; }
        public int IdInsumo { get; set; }
        public int IdUnidad { get; set; }
        public decimal Cantidad { get; set; }

        // Propiedades auxiliares
        public string NombreInsumo { get; set; } = string.Empty;
        public string UnidadAbreviatura { get; set; } = string.Empty;
    }
}