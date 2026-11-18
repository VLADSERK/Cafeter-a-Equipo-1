using System;
using System.Collections.Generic;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class AutorizacionMermaNegocio
    {
        private readonly AutorizacionMermaDatos _datos = new();

        /// <summary>Bandeja de mermas pendientes (solo Administrador, RNF-10).</summary>
        public List<Merma> ObtenerPendientes()
        {
            var permiso = Permisos.Verificar(Operacion.AutorizarMerma);
            if (!permiso.Exito)
                throw new UnauthorizedAccessException(permiso.Mensaje);

            return _datos.ObtenerMermasPendientes();
        }

        public (bool Exito, string Mensaje) ResolverSolicitud(int idMerma, bool aprobar, string? observaciones)
        {
            var permiso = Permisos.Verificar(Operacion.AutorizarMerma);
            if (!permiso.Exito)
                return permiso;

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