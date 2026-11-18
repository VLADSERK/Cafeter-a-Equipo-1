using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    /// <summary>Cómo se repartiría una merma entre los lotes y cuánto costaría (RF-09, RNF-08).</summary>
    public sealed class EstimacionMerma
    {
        public decimal Cantidad { get; init; }
        public decimal CostoEstimado { get; init; }

        /// <summary>Existencia del lote elegido o, sin lote, de todo el insumo.</summary>
        public decimal Disponible { get; init; }

        public bool Alcanza => Cantidad <= Disponible;

        /// <summary>Lotes que se descontarían y cuánto de cada uno (en orden PEPS si no se eligió lote).</summary>
        public List<(Lote Lote, decimal Cantidad)> Asignacion { get; init; } = new();
    }

    public class MermaNegocio
    {
        private readonly MermaDatos _mermaDatos = new();
        private readonly ConfiguracionDatos _configDatos = new();
        private readonly InventarioDatos _inventarioDatos = new();

        public ConfiguracionGeneral ObtenerConfiguracionActual() => _configDatos.ObtenerConfiguracion();

        public int ObtenerConteoMermasHoyUsuarioActual()
        {
            if (!SesionActual.HaySesionActiva) return 0;
            return _mermaDatos.ContarMermasHoyPorUsuario(SesionActual.IdUsuario);
        }

        /// <summary>Motivo que obliga a capturar una descripción (RIU-05).</summary>
        public const string MotivoOtros = "Otros";

        /// <summary>Catálogo de motivos (registro de mermas y filtro de la bandeja de autorizaciones).</summary>
        public static readonly string[] Motivos =
        {
            "Caducidad vencida",
            "Derrame o accidente en barra",
            "Producto dañado en almacén",
            "Error de preparación en receta",
            "Mal estado / Calidad no óptima",
            MotivoOtros
        };

        /// <summary>RNF-08: costo estimado = cantidad × costo unitario del lote, Half-Up a 2 decimales.</summary>
        public static decimal CalcularCostoEstimado(decimal cantidad, decimal costoUnitario)
            => Redondeo.Importe(cantidad * costoUnitario);

        /// <summary>RF-09: la merma requiere NIP de Administrador si su costo supera el límite económico global.</summary>
        public static bool RequiereAutorizacion(decimal costoEstimado, ConfiguracionGeneral cfg)
            => costoEstimado > cfg.LimiteEconomicoMerma;

        /// <summary>
        /// Estima la merma con los lotes ya cargados del insumo (en orden PEPS).
        /// Con lote: todo sale de ese lote. Sin lote: se reparte como se descontará, del lote con
        /// la recepción más antigua al más reciente, y el costo usa el costo unitario de cada lote.
        /// </summary>
        public static EstimacionMerma Estimar(IEnumerable<Lote> lotesPepsDelInsumo, Lote? lote, decimal cantidad)
        {
            if (lote != null)
            {
                return new EstimacionMerma
                {
                    Cantidad = cantidad,
                    Disponible = lote.CantidadDisponible,
                    CostoEstimado = CalcularCostoEstimado(Math.Max(0, cantidad), lote.CostoUnitario),
                    Asignacion = cantidad > 0 ? new() { (lote, Math.Min(cantidad, lote.CantidadDisponible)) } : new()
                };
            }

            var asignacion = new List<(Lote, decimal)>();
            decimal restante = Math.Max(0, cantidad);
            decimal costo = 0m;
            var lotes = lotesPepsDelInsumo.Where(l => l.CantidadDisponible > 0).ToList();

            foreach (var l in lotes)
            {
                if (restante <= 0) break;
                decimal tomar = Math.Min(restante, l.CantidadDisponible);
                asignacion.Add((l, tomar));
                costo += tomar * l.CostoUnitario;
                restante -= tomar;
            }

            return new EstimacionMerma
            {
                Cantidad = cantidad,
                Disponible = lotes.Sum(l => l.CantidadDisponible),
                CostoEstimado = Redondeo.Importe(costo),
                Asignacion = asignacion
            };
        }

        /// <summary>Compatibilidad: merma de un lote conocido.</summary>
        public (bool Exito, string EstadoFinal, string Mensaje, int IdMerma) RegistrarMerma(
            Lote loteAfectado,
            decimal cantidadMermada,
            string motivo,
            string? observaciones,
            string? nipAdministrador = null)
        {
            if (loteAfectado == null || loteAfectado.IdLote <= 0)
                return (false, "", "Selecciona un insumo y un lote PEPS válido.", 0);

            return RegistrarMerma(loteAfectado.IdInsumo, loteAfectado, cantidadMermada, motivo, observaciones, nipAdministrador);
        }

        /// <summary>
        /// Registra una merma (RF-09, RIU-05). El lote es opcional ("cuando se conozca"):
        /// si se omite, se descuenta por PEPS (RF-02) y el costo se calcula con los lotes que se afectarán.
        /// - Costo ≤ límite: se autoriza y descuenta automáticamente.
        /// - Costo &gt; límite y <paramref name="nipAdministrador"/> válido: se autoriza en el momento y se descuenta.
        /// - Costo &gt; límite sin NIP: queda Pendiente sin descontar inventario.
        /// Un NIP incorrecto no registra nada (la pantalla vuelve a pedirlo o deja la merma pendiente).
        /// </summary>
        public (bool Exito, string EstadoFinal, string Mensaje, int IdMerma) RegistrarMerma(
            int idInsumo,
            Lote? lote,
            decimal cantidadMermada,
            string motivo,
            string? observaciones,
            string? nipAdministrador = null)
        {
            var permiso = Permisos.Verificar(Operacion.RegistrarMerma);
            if (!permiso.Exito)
                return (false, "", permiso.Mensaje, 0);

            if (idInsumo <= 0)
                return (false, "", "Selecciona el insumo afectado.", 0);

            if (lote != null && (lote.IdLote <= 0 || lote.IdInsumo != idInsumo))
                return (false, "", "El lote seleccionado no corresponde al insumo.", 0);

            if (cantidadMermada <= 0)
                return (false, "", "La cantidad mermada debe ser mayor a 0.", 0);

            if (Redondeo.ExcedeDecimales(cantidadMermada, Redondeo.DecimalesCantidad))
                return (false, "", "La cantidad mermada admite como máximo 3 decimales.", 0);

            if (string.IsNullOrWhiteSpace(motivo))
                return (false, "", "Selecciona el motivo de la merma.", 0);

            bool esOtros = string.Equals(motivo.Trim(), MotivoOtros, StringComparison.OrdinalIgnoreCase);
            if (esOtros && string.IsNullOrWhiteSpace(observaciones))
                return (false, "", "Con el motivo \"Otros\" es obligatorio describir qué ocurrió.", 0);

            ConfiguracionGeneral config;
            EstimacionMerma estimacion;
            try
            {
                config = _configDatos.ObtenerConfiguracion();
                // Existencias actuales desde MySQL (no las de la pantalla) para estimar costo y reparto PEPS
                var lotesInsumo = _inventarioDatos.ObtenerLotesPeps(soloConDisponibilidad: true)
                    .Where(l => l.IdInsumo == idInsumo)
                    .ToList();
                Lote? loteActual = lote == null ? null : lotesInsumo.FirstOrDefault(l => l.IdLote == lote.IdLote);
                if (lote != null && loteActual == null)
                    return (false, "", "El lote seleccionado ya no tiene existencia.", 0);

                estimacion = Estimar(lotesInsumo, loteActual, cantidadMermada);
            }
            catch (Exception ex)
            {
                return (false, "", $"No se pudo consultar el inventario en MySQL:\n{ex.Message}", 0);
            }

            if (!estimacion.Alcanza)
            {
                string donde = lote != null ? "la existencia disponible del lote" : "la existencia total del insumo";
                return (false, "", $"La cantidad mermada no puede superar {donde} ({estimacion.Disponible:0.###}).", 0);
            }

            decimal costoEstimado = estimacion.CostoEstimado;
            bool requiereAutorizacion = RequiereAutorizacion(costoEstimado, config);

            // Autorización inmediata: el NIP se valida aquí aunque la pantalla ya lo haya revisado
            int? idAdminAutoriza = null;
            string nombreAdmin = string.Empty;
            if (requiereAutorizacion && !string.IsNullOrWhiteSpace(nipAdministrador))
            {
                try
                {
                    var validacion = new AuthNegocio().ValidarNipAdministrador(nipAdministrador);
                    if (!validacion.Autorizado)
                        return (false, "", "NIP incorrecto o sin privilegios de Administrador. La merma no se registró.", 0);

                    idAdminAutoriza = validacion.IdAdminAutoriza;
                    nombreAdmin = validacion.NombreAdmin;
                }
                catch (Exception ex)
                {
                    return (false, "", $"No se pudo validar el NIP en MySQL:\n{ex.Message}", 0);
                }
            }

            var merma = new Merma
            {
                IdInsumo = idInsumo,
                IdLote = lote?.IdLote,
                IdUsuarioRegistra = SesionActual.IdUsuario,
                IdUsuarioAutoriza = idAdminAutoriza,
                Cantidad = cantidadMermada,
                Motivo = motivo.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
                FechaMerma = DateTime.Now,
                CostoEstimado = costoEstimado
            };

            string destino = lote != null
                ? $"al lote {lote.CodigoLote}"
                : $"por PEPS a {string.Join(", ", estimacion.Asignacion.Select(a => $"{a.Lote.CodigoLote} ({a.Cantidad:0.###})"))}";

            try
            {
                var (idMerma, estadoFinal) = _mermaDatos.RegistrarMermaOperativa(merma, config.LimiteEconomicoMerma, idAdminAutoriza);

                string msg;
                if (estadoFinal == "Pendiente")
                    msg = $"Merma #MRM-{idMerma:000} registrada como PENDIENTE DE AUTORIZACIÓN (costo ${costoEstimado:N2} > límite ${config.LimiteEconomicoMerma:N2}).\n" +
                          "El inventario NO se descontó: el Administrador deberá aprobarla con su NIP en la bandeja de autorizaciones." +
                          (lote == null ? "\nAl autorizarla se descontará por PEPS con las existencias de ese momento." : "");
                else if (idAdminAutoriza.HasValue)
                    msg = $"Merma #MRM-{idMerma:000} AUTORIZADA por {nombreAdmin} con NIP (costo ${costoEstimado:N2} > límite ${config.LimiteEconomicoMerma:N2}).\n" +
                          $"El descuento se aplicó {destino}.";
                else
                    msg = $"Merma #MRM-{idMerma:000} AUTORIZADA automáticamente (costo ${costoEstimado:N2} ≤ límite ${config.LimiteEconomicoMerma:N2}).\n" +
                          $"El descuento se aplicó {destino}.";

                return (true, estadoFinal, msg, idMerma);
            }
            catch (InvalidOperationException ex)
            {
                return (false, "", ex.Message, 0);
            }
            catch (Exception ex)
            {
                return (false, "", $"Error al registrar la merma en MySQL:\n{ex.Message}", 0);
            }
        }
    }
}
