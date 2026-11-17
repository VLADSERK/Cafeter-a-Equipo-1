using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class VentaNegocio
    {
        private readonly VentaDatos _datos = new();
        private readonly AuthNegocio _auth = new();

        public static readonly string[] MetodosPago = { "Efectivo", "Tarjeta", "Transferencia" };

        // =====================================================================
        // COBRO
        // =====================================================================

        /// <summary>
        /// Registra una venta. "productos" es la lista del carrito: (IdProducto, Cantidad).
        /// Para pago en efectivo se requiere montoRecibido; para tarjeta/transferencia se ignora.
        /// Devuelve la venta guardada (con Folio, Total y Cambio) o un mensaje de error.
        /// </summary>
        public (bool Exito, string Mensaje, Venta? Venta) RegistrarVenta(
            List<(int IdProducto, int Cantidad)> productos,
            string metodoPago,
            decimal? montoRecibido)
        {
            if (!SesionActual.HaySesionActiva)
                return (false, "No hay una sesión activa.", null);

            if (productos == null || productos.Count == 0)
                return (false, "Agrega al menos un producto a la orden.", null);

            if (productos.Any(p => p.IdProducto <= 0 || p.Cantidad <= 0))
                return (false, "Hay un producto inválido o con cantidad cero en la orden.", null);

            string? metodo = MetodosPago.FirstOrDefault(m => string.Equals(m, metodoPago?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (metodo == null)
                return (false, "Selecciona un método de pago válido (Efectivo, Tarjeta o Transferencia).", null);

            bool esEfectivo = metodo == "Efectivo";
            if (esEfectivo && (!montoRecibido.HasValue || montoRecibido.Value <= 0))
                return (false, "Captura el monto recibido en efectivo.", null);

            // Juntar productos repetidos en una sola línea
            var detalles = productos
                .GroupBy(p => p.IdProducto)
                .Select(g => new DetalleVenta { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
                .ToList();

            var venta = new Venta
            {
                IdUsuario = SesionActual.IdUsuario,
                FechaVenta = DateTime.Now,
                MetodoPago = metodo,
                MontoRecibido = esEfectivo ? montoRecibido : null,
                Detalles = detalles
            };

            try
            {
                var guardada = _datos.RegistrarVenta(venta);

                string mensaje = $"Venta {guardada.Folio} registrada por {guardada.Total:C2}.";
                if (guardada.Cambio.HasValue)
                    mensaje += $"\nCambio: {guardada.Cambio.Value:C2}";

                return (true, mensaje, guardada);
            }
            catch (InvalidOperationException ex)
            {
                // Errores de negocio: stock insuficiente, producto sin receta, monto menor al total, etc.
                return (false, ex.Message, null);
            }
            catch (Exception ex)
            {
                return (false, $"Error al registrar la venta en MySQL:\n{ex.Message}", null);
            }
        }

        // =====================================================================
        // HISTORIAL
        // =====================================================================

        /// <summary>estado: null = todas, "Confirmada" o "Cancelada".</summary>
        public List<Venta> ObtenerVentas(DateTime desde, DateTime hasta, string? estado = null)
        {
            if (hasta.Date < desde.Date)
                (desde, hasta) = (hasta, desde);

            return _datos.ObtenerVentas(desde, hasta, estado);
        }

        public Venta? ObtenerVentaConDetalle(int idVenta) => _datos.ObtenerVentaConDetalle(idVenta);

        public int ContarMisVentasHoy()
            => SesionActual.HaySesionActiva ? _datos.ContarVentasHoyPorUsuario(SesionActual.IdUsuario) : 0;

        // =====================================================================
        // CANCELACIÓN (requiere NIP de Administrador)
        // =====================================================================

        /// <summary>
        /// Cancela una venta confirmada y regresa sus insumos al inventario.
        /// Cualquier usuario puede solicitarla, pero debe capturarse el NIP de un Administrador activo.
        /// </summary>
        public (bool Exito, string Mensaje) CancelarVenta(int idVenta, string motivo, string nipAdministrador)
        {
            if (!SesionActual.HaySesionActiva)
                return (false, "No hay una sesión activa.");

            if (idVenta <= 0)
                return (false, "Selecciona una venta válida.");

            if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 5)
                return (false, "Escribe el motivo de la cancelación (mínimo 5 caracteres).");

            try
            {
                var nip = _auth.ValidarNipAdministrador(nipAdministrador);
                if (!nip.Autorizado)
                    return (false, "NIP de Administrador incorrecto.");

                string motivoFinal = $"{motivo.Trim()} (autorizó: {nip.NombreAdmin})";
                return _datos.CancelarVenta(idVenta, nip.IdAdminAutoriza, motivoFinal);
            }
            catch (Exception ex)
            {
                return (false, $"Error al cancelar la venta en MySQL:\n{ex.Message}");
            }
        }
    }
}

