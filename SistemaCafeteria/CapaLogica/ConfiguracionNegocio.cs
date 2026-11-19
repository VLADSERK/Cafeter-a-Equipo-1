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
        private readonly AuthNegocio _auth = new();

        /// <summary>Diferencia máxima (en puntos porcentuales) entre el fin de un rango y el inicio del siguiente.</summary>
        private const decimal SaltoMaximoEntreRangos = 1m;

        public ConfiguracionGeneral ObtenerConfiguracionActual()
        {
            return _configDatos.ObtenerConfiguracion();
        }

        /// <summary>
        /// Interpreta y valida los rangos del semáforo (textos tipo "50% - 100%", "21% - 49%", "1% - 20%")
        /// y el límite de mermas, sin pedir NIP, para que la pantalla avise antes de solicitarlo (RF-08, RF-12).
        /// Reglas:
        ///   • Verde termina en 100 %; Blanco es fijo en 0 % (sin existencias).
        ///   • 0 &lt; Rojo máx. &lt; Amarillo mín. ≤ Amarillo máx. &lt; Verde mín. ≤ 100.
        ///   • Los rangos no se traslapan ni dejan huecos de más de 1 punto (cada % cae en un solo color).
        ///   • Límite económico ≥ 0 con máximo 2 decimales.
        /// </summary>
        public (bool Exito, string Mensaje, ConfiguracionGeneral? Config) ValidarParametros(
            int idConfiguracion,
            string textoVerde,
            string textoAmarillo,
            string textoRojo,
            decimal limiteEconomicoMerma)
        {
            var permiso = Permisos.Verificar(Operacion.ConfigurarParametros);
            if (!permiso.Exito)
                return (false, permiso.Mensaje, null);

            if (limiteEconomicoMerma < 0)
                return (false, "El límite económico de mermas no puede ser negativo.", null);

            if (Redondeo.ExcedeDecimales(limiteEconomicoMerma, Redondeo.DecimalesImporte))
                return (false, "El límite económico admite como máximo 2 decimales (centavos).", null);

            if (!IntentarExtraer(textoVerde, out decimal verdeMin, out decimal? verdeFin) ||
                !IntentarExtraer(textoAmarillo, out decimal amarilloMin, out decimal? amarilloFin) || amarilloFin == null ||
                !IntentarExtraer(textoRojo, out decimal rojoInicio, out decimal? rojoFin))
            {
                return (false, "Verifica el formato de los porcentajes del semáforo (ejemplo: '50% - 100%', '21% - 49%', '1% - 20%').", null);
            }

            decimal amarilloMax = amarilloFin.Value;
            decimal rojoMax = rojoFin ?? rojoInicio;

            if (verdeFin.HasValue && verdeFin.Value != 100m)
                return (false, "El rango Verde debe terminar en 100 % (el stock máximo).", null);

            if (verdeMin > 100m || amarilloMax > 100m || rojoMax > 100m)
                return (false, "Los porcentajes del semáforo no pueden pasar de 100 %.", null);

            if (rojoMax <= 0m)
                return (false, "El rango Rojo debe ser mayor a 0 % (0 % corresponde a Blanco, sin existencias).", null);

            if (rojoFin.HasValue && rojoInicio > rojoMax)
                return (false, "En el rango Rojo el valor inicial no puede ser mayor que el final.", null);

            if (amarilloMin > amarilloMax)
                return (false, "En el rango Amarillo el valor inicial no puede ser mayor que el final.", null);

            if (rojoMax >= amarilloMin || amarilloMax >= verdeMin)
                return (false, "Los rangos del semáforo no deben traslaparse (Rojo < Amarillo < Verde).", null);

            if (amarilloMin - rojoMax > SaltoMaximoEntreRangos || verdeMin - amarilloMax > SaltoMaximoEntreRangos)
                return (false, "Los rangos deben ser continuos: cada uno empieza justo después del anterior " +
                               "(por ejemplo Rojo hasta 20 %, Amarillo desde 21 %).", null);

            return (true, string.Empty, new ConfiguracionGeneral
            {
                IdConfiguracion = idConfiguracion > 0 ? idConfiguracion : 1,
                VerdeMinimo = verdeMin,
                AmarilloMinimo = amarilloMin,
                AmarilloMaximo = amarilloMax,
                RojoMaximo = rojoMax,
                LimiteEconomicoMerma = limiteEconomicoMerma
            });
        }

        /// <summary>True si los parámetros son iguales a los guardados (no hay nada que actualizar).</summary>
        public static bool MismosParametros(ConfiguracionGeneral a, ConfiguracionGeneral b)
            => a.VerdeMinimo == b.VerdeMinimo
               && a.AmarilloMinimo == b.AmarilloMinimo
               && a.AmarilloMaximo == b.AmarilloMaximo
               && a.RojoMaximo == b.RojoMaximo
               && a.LimiteEconomicoMerma == b.LimiteEconomicoMerma;

        /// <summary>
        /// Valida y guarda los parámetros globales (RF-12). Solo el Administrador (RNF-10) y con el NIP
        /// de un Administrador como confirmación (RNF-13), validado aquí y no solo en la pantalla.
        /// </summary>
        public (bool Exito, string Mensaje, ConfiguracionGeneral? ConfigGuardada) GuardarDesdeRangosTexto(
            int idConfiguracion,
            string textoVerde,
            string textoAmarillo,
            string textoRojo,
            decimal limiteEconomicoMerma,
            string nipAdministrador)
        {
            var v = ValidarParametros(idConfiguracion, textoVerde, textoAmarillo, textoRojo, limiteEconomicoMerma);
            if (!v.Exito)
                return (false, v.Mensaje, null);

            try
            {
                var nip = _auth.ValidarNipAdministrador(nipAdministrador);
                if (!nip.Autorizado)
                    return (false, "NIP de Administrador incorrecto. No se guardaron los parámetros.", null);

                bool actualizado = _configDatos.ActualizarConfiguracion(v.Config!);
                if (!actualizado)
                    return (false, "No se encontró el registro de configuración en MySQL para actualizar.", null);

                return (true, $"Parámetros globales actualizados (autorizó: {nip.NombreAdmin}).", v.Config);
            }
            catch (Exception ex)
            {
                return (false, $"Error al guardar en MySQL: {ex.Message}", null);
            }
        }

        /// <summary>Primer número del texto y, si hay un segundo, el último ("21% - 49%" → 21 y 49).</summary>
        private static bool IntentarExtraer(string texto, out decimal inicio, out decimal? fin)
        {
            inicio = 0;
            fin = null;
            var matches = Regex.Matches(texto ?? string.Empty, @"\d+(\.\d+)?");
            if (matches.Count == 0 || matches.Count > 2)
                return false;

            if (!decimal.TryParse(matches[0].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out inicio))
                return false;

            if (matches.Count == 2)
            {
                if (!decimal.TryParse(matches[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal f))
                    return false;
                fin = f;
            }
            return true;
        }
    }
}
