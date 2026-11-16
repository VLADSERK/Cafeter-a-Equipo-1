using System;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class MermaNegocio
    {
        private readonly MermaDatos _mermaDatos = new();
        private readonly ConfiguracionDatos _configDatos = new();

        public ConfiguracionGeneral ObtenerConfiguracionActual() => _configDatos.ObtenerConfiguracion();

        public int ObtenerConteoMermasHoyUsuarioActual()
        {
            if (!SesionActual.HaySesionActiva) return 0;
            return _mermaDatos.ContarMermasHoyPorUsuario(SesionActual.IdUsuario);
        }

        public (bool Exito, string EstadoFinal, string Mensaje) RegistrarMerma(
            Lote loteAfectado,
            decimal cantidadMermada,
            string motivo,
            string observaciones)
        {
            if (!SesionActual.HaySesionActiva)
                return (false, "", "No hay una sesión de usuario activa.");

            if (loteAfectado == null || loteAfectado.IdLote <= 0)
                return (false, "", "Selecciona un insumo y un lote PEPS válido.");

            if (cantidadMermada <= 0)
                return (false, "", "La cantidad mermada debe ser mayor a 0.");

            if (cantidadMermada > loteAfectado.CantidadDisponible)
                return (false, "", "La cantidad mermada no puede superar la existencia disponible del lote.");

            if (string.IsNullOrWhiteSpace(motivo))
                return (false, "", "Selecciona el motivo de la merma.");

            if (string.IsNullOrWhiteSpace(observaciones))
                return (false, "", "Escribe una breve observación o justificación de la incidencia.");

            var config = _configDatos.ObtenerConfiguracion();
            decimal costoEstimado = Math.Round(cantidadMermada * loteAfectado.CostoUnitario, 2);

            var merma = new Merma
            {
                IdInsumo = loteAfectado.IdInsumo,
                IdLote = loteAfectado.IdLote,
                IdUsuarioRegistra = SesionActual.IdUsuario,
                Cantidad = cantidadMermada,
                Motivo = motivo.Trim(),
                Descripcion = observaciones.Trim(),
                FechaMerma = DateTime.Now,
                CostoEstimado = costoEstimado
            };

            try
            {
                var (idMerma, estadoFinal) = _mermaDatos.RegistrarMermaOperativa(merma, config.LimiteEconomicoMerma);

                string msg = estadoFinal == "Autorizada"
                    ? $"Merma #MRM-{idMerma:000} AUTORIZADA automáticamente (costo ${costoEstimado:N2} ≤ límite ${config.LimiteEconomicoMerma:N2}).\nEl descuento se aplicó al lote {loteAfectado.CodigoLote}."
                    : $"Merma #MRM-{idMerma:000} registrada como PENDIENTE DE AUTORIZACIÓN (costo ${costoEstimado:N2} > límite ${config.LimiteEconomicoMerma:N2}).\nEl Administrador deberá aprobarla con su NIP antes de descontar el lote.";

                return (true, estadoFinal, msg);
            }
            catch (Exception ex)
            {
                return (false, "", $"Error al registrar la merma en MySQL:\n{ex.Message}");
            }
        }
    }
}