using System;
using System.Collections.Generic;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class AutorizacionMermaNegocio
    {
        private readonly AutorizacionMermaDatos _datos = new();

        public List<Merma> ObtenerPendientes() => _datos.ObtenerMermasPendientes();

        public (bool Exito, string Mensaje) ResolverSolicitud(int idMerma, bool aprobar, string? observaciones)
        {
            if (!SesionActual.HaySesionActiva)
                return (false, "No hay una sesión activa.");

            if (SesionActual.Rol != "Administrador")
                return (false, "Se requieren privilegios de Administrador para autorizar o rechazar mermas.");

            if (idMerma <= 0)
                return (false, "Selecciona una merma válida de la lista.");

            try
            {
                var resultado = _datos.ResolverMerma(idMerma, SesionActual.IdUsuario, aprobar, observaciones);

                if (!resultado.Exito)
                    return (false, resultado.Mensaje);

                string accion = aprobar ? "autorizada y aplicada al inventario" : "rechazada";
                return (true, $"La merma #MRM-{idMerma:000} fue {accion} exitosamente.");
            }
            catch (Exception ex)
            {
                return (false, $"Error al procesar la autorización en MySQL:\n{ex.Message}");
            }
        }
    }
}