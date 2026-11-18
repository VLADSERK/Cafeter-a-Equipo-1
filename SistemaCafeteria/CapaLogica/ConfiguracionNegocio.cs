using System;
using System.Globalization;
using System.Text.RegularExpressions;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class ConfiguracionNegocio
    {
        private readonly ConfiguracionDatos _configDatos = new();

        public ConfiguracionGeneral ObtenerConfiguracionActual()
        {
            return _configDatos.ObtenerConfiguracion();
        }

        /// <summary>
        /// Convierte textos tipo "50% - 100%" o "20%" en sus valores numéricos y valida coherencia
        /// antes de actualizar la tabla ConfiguracionGeneral en MySQL.
        /// </summary>
        public (bool Exito, string Mensaje, ConfiguracionGeneral? ConfigGuardada) GuardarDesdeRangosTexto(
            int idConfiguracion,
            string textoVerde,
            string textoAmarillo,
            string textoRojo,
            decimal limiteEconomicoMerma)
        {
            // RF-12 / RNF-10: solo el Administrador modifica los parámetros globales
            var permiso = Permisos.Verificar(Operacion.ConfigurarParametros);
            if (!permiso.Exito)
                return (false, permiso.Mensaje, null);

            if (limiteEconomicoMerma < 0)
                return (false, "El límite económico de mermas no puede ser negativo.", null);

            if (Redondeo.ExcedeDecimales(limiteEconomicoMerma, Redondeo.DecimalesImporte))
                return (false, "El límite económico admite como máximo 2 decimales (centavos).", null);

            if (!IntentarExtraerMinimo(textoVerde, out decimal verdeMin) ||
                !IntentarExtraerRango(textoAmarillo, out decimal amarilloMin, out decimal amarilloMax) ||
                !IntentarExtraerMaximo(textoRojo, out decimal rojoMax))
            {
                return (false, "Verifica el formato de los porcentajes del semáforo (ejemplo: '50% - 100%', '21% - 49%', '1% - 20%').", null);
            }

            if (rojoMax >= amarilloMin || amarilloMax >= verdeMin || amarilloMin > amarilloMax)
            {
                return (false, "Los rangos del semáforo no deben traslaparse (Rojo < Amarillo < Verde).", null);
            }

            var nuevaConfig = new ConfiguracionGeneral
            {
                IdConfiguracion = idConfiguracion > 0 ? idConfiguracion : 1,
                VerdeMinimo = verdeMin,
                AmarilloMinimo = amarilloMin,
                AmarilloMaximo = amarilloMax,
                RojoMaximo = rojoMax,
                LimiteEconomicoMerma = limiteEconomicoMerma
            };

            try
            {
                bool actualizado = _configDatos.ActualizarConfiguracion(nuevaConfig);
                if (!actualizado)
                    return (false, "No se encontró el registro de configuración en MySQL para actualizar.", null);

                return (true, "Parámetros globales actualizados correctamente en MySQL.", nuevaConfig);
            }
            catch (Exception ex)
            {
                return (false, $"Error al guardar en MySQL: {ex.Message}", null);
            }
        }

        private static bool IntentarExtraerMinimo(string texto, out decimal min)
        {
            min = 0;
            var matches = Regex.Matches(texto ?? string.Empty, @"\d+(\.\d+)?");
            if (matches.Count == 0) return false;
            return decimal.TryParse(matches[0].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out min);
        }

        private static bool IntentarExtraerMaximo(string texto, out decimal max)
        {
            max = 0;
            var matches = Regex.Matches(texto ?? string.Empty, @"\d+(\.\d+)?");
            if (matches.Count == 0) return false;
            return decimal.TryParse(matches[^1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out max);
        }

        private static bool IntentarExtraerRango(string texto, out decimal min, out decimal max)
        {
            min = 0;
            max = 0;
            var matches = Regex.Matches(texto ?? string.Empty, @"\d+(\.\d+)?");
            if (matches.Count < 2) return false;
            return decimal.TryParse(matches[0].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out min) &&
                   decimal.TryParse(matches[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out max);
        }
    }
}