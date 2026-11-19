using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    /// <summary>
    /// Gestión de usuarios y credenciales desde Configuración Global (RF-12, Diseño 6.2).
    /// Reglas:
    ///   • Solo el Administrador opera (RNF-10) y cada cambio exige el NIP de un Administrador (RNF-13).
    ///   • Contraseñas y NIP se guardan con bcrypt y salt individual (RNF-09 / RI-04).
    ///   • El NIP es de 4 dígitos, solo para Administradores y no se repite entre ellos
    ///     (cada NIP identifica a quien autoriza mermas, cancelaciones y cambios).
    ///   • Nunca queda el sistema sin un Administrador activo con NIP para autorizar.
    /// Los métodos Validar* revisan los datos sin NIP para que la pantalla avise antes de pedirlo;
    /// las operaciones vuelven a validar todo antes de guardar.
    /// </summary>
    public class UsuarioNegocio
    {
        public const int LongitudMinimaContrasena = 6;
        public const int LongitudMaximaContrasenaBytes = 72;   // límite de bcrypt
        public const int LongitudNip = 4;
        private const int FactorTrabajoBcrypt = 11;            // el mismo de los usuarios iniciales

        private static readonly Regex FormatoNombreUsuario = new(@"^[A-Za-z0-9._-]{3,50}$", RegexOptions.Compiled);
        private static readonly Regex FormatoNip = new(@"^\d{4}$", RegexOptions.Compiled);

        private readonly UsuarioDatos _datos = new();
        private readonly AuthNegocio _auth = new();

        public static bool EsAdministrador(Usuario usuario)
            => string.Equals(usuario.NombreRol?.Trim(), Permisos.RolAdministrador, StringComparison.OrdinalIgnoreCase);

        public static bool EstaActivo(Usuario usuario)
            => string.Equals(usuario.Estado?.Trim(), "Activo", StringComparison.OrdinalIgnoreCase);

        // =====================================================================
        // CONSULTA
        // =====================================================================

        public List<Usuario> ObtenerUsuarios()
        {
            var permiso = Permisos.Verificar(Operacion.AdministrarUsuarios);
            if (!permiso.Exito)
                throw new UnauthorizedAccessException(permiso.Mensaje);

            return _datos.ObtenerTodos();
        }

        // =====================================================================
        // DATOS (nombre y nombre de usuario)
        // =====================================================================

        public (bool Exito, string Mensaje) ValidarDatos(int idUsuario, string nombre, string nombreUsuario)
            => Ejecutar(() => ValidarDatosInterno(idUsuario, nombre, nombreUsuario).Resultado);

        public (bool Exito, string Mensaje) ActualizarDatos(int idUsuario, string nombre, string nombreUsuario, string nipAdministrador)
            => Ejecutar(() =>
            {
                var v = ValidarDatosInterno(idUsuario, nombre, nombreUsuario);
                if (!v.Resultado.Exito) return v.Resultado;

                var nip = ValidarNipAutoriza(nipAdministrador);
                if (!nip.Exito) return (false, nip.Mensaje);

                _datos.ActualizarDatos(idUsuario, nombre.Trim(), nombreUsuario.Trim());
                RefrescarSesionSiEsPropia(idUsuario);
                return (true, $"Datos de \"{nombreUsuario.Trim()}\" actualizados (autorizó: {nip.NombreAdmin}).");
            });

        private ((bool Exito, string Mensaje) Resultado, Usuario? Usuario) ValidarDatosInterno(int idUsuario, string nombre, string nombreUsuario)
        {
            var u = ObtenerUsuarioParaEditar(idUsuario, out var error);
            if (u == null) return (error, null);

            if (string.IsNullOrWhiteSpace(nombre))
                return ((false, "El nombre completo es obligatorio."), u);

            if (nombre.Trim().Length > 100)
                return ((false, "El nombre completo no puede pasar de 100 caracteres."), u);

            string usuario = (nombreUsuario ?? string.Empty).Trim();
            if (usuario.Length == 0)
                return ((false, "El nombre de usuario es obligatorio."), u);

            if (!FormatoNombreUsuario.IsMatch(usuario))
                return ((false, "El nombre de usuario debe tener de 3 a 50 caracteres: letras sin acento, números, punto, guion o guion bajo (sin espacios)."), u);

            if (_datos.ExisteNombreUsuario(usuario, idUsuario))
                return ((false, $"El nombre de usuario \"{usuario}\" ya está en uso."), u);

            return ((true, string.Empty), u);
        }

        // =====================================================================
        // CONTRASEÑA
        // =====================================================================

        public (bool Exito, string Mensaje) ValidarContrasena(int idUsuario, string nueva, string confirmacion)
            => Ejecutar(() => ValidarContrasenaInterno(idUsuario, nueva, confirmacion));

        public (bool Exito, string Mensaje) CambiarContrasena(int idUsuario, string nueva, string confirmacion, string nipAdministrador)
            => Ejecutar(() =>
            {
                var v = ValidarContrasenaInterno(idUsuario, nueva, confirmacion);
                if (!v.Exito) return v;

                var nip = ValidarNipAutoriza(nipAdministrador);
                if (!nip.Exito) return (false, nip.Mensaje);

                string hash = BCrypt.Net.BCrypt.HashPassword(nueva, workFactor: FactorTrabajoBcrypt);
                _datos.ActualizarContrasenaHash(idUsuario, hash);
                RefrescarSesionSiEsPropia(idUsuario);

                var u = _datos.ObtenerPorId(idUsuario);
                return (true, $"Contraseña de \"{u?.NombreUsuario}\" actualizada (autorizó: {nip.NombreAdmin}).");
            });

        private (bool Exito, string Mensaje) ValidarContrasenaInterno(int idUsuario, string nueva, string confirmacion)
        {
            var u = ObtenerUsuarioParaEditar(idUsuario, out var error);
            if (u == null) return error;

            if (string.IsNullOrWhiteSpace(nueva))
                return (false, "Escribe la nueva contraseña.");

            if (nueva.Length < LongitudMinimaContrasena)
                return (false, $"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres.");

            if (Encoding.UTF8.GetByteCount(nueva) > LongitudMaximaContrasenaBytes)
                return (false, $"La contraseña es demasiado larga (máximo {LongitudMaximaContrasenaBytes} caracteres sin acentos).");

            if (nueva != confirmacion)
                return (false, "La confirmación no coincide con la nueva contraseña.");

            if (BCrypt.Net.BCrypt.Verify(nueva, u.ContrasenaHash))
                return (false, "La nueva contraseña debe ser distinta de la actual.");

            return (true, string.Empty);
        }

        // =====================================================================
        // NIP (solo Administradores, 4 dígitos, sin repetirse)
        // =====================================================================

        public (bool Exito, string Mensaje) ValidarNuevoNip(int idUsuario, string nuevoNip, string confirmacion)
            => Ejecutar(() => ValidarNuevoNipInterno(idUsuario, nuevoNip, confirmacion));

        public (bool Exito, string Mensaje) AsignarNip(int idUsuario, string nuevoNip, string confirmacion, string nipAdministrador)
            => Ejecutar(() =>
            {
                var v = ValidarNuevoNipInterno(idUsuario, nuevoNip, confirmacion);
                if (!v.Exito) return v;

                var nip = ValidarNipAutoriza(nipAdministrador);
                if (!nip.Exito) return (false, nip.Mensaje);

                string hash = BCrypt.Net.BCrypt.HashPassword(nuevoNip.Trim(), workFactor: FactorTrabajoBcrypt);
                _datos.ActualizarNipHash(idUsuario, hash);
                RefrescarSesionSiEsPropia(idUsuario);

                var u = _datos.ObtenerPorId(idUsuario);
                return (true, $"NIP de \"{u?.NombreUsuario}\" asignado (autorizó: {nip.NombreAdmin}).");
            });

        private (bool Exito, string Mensaje) ValidarNuevoNipInterno(int idUsuario, string nuevoNip, string confirmacion)
        {
            var u = ObtenerUsuarioParaEditar(idUsuario, out var error);
            if (u == null) return error;

            if (!EsAdministrador(u))
                return (false, "Solo las cuentas de Administrador tienen NIP de autorización.");

            string nip = (nuevoNip ?? string.Empty).Trim();
            if (!FormatoNip.IsMatch(nip))
                return (false, $"El NIP debe tener exactamente {LongitudNip} dígitos.");

            if (nip != (confirmacion ?? string.Empty).Trim())
                return (false, "La confirmación no coincide con el nuevo NIP.");

            if (!string.IsNullOrEmpty(u.NipHash) && BCrypt.Net.BCrypt.Verify(nip, u.NipHash))
                return (false, "El nuevo NIP debe ser distinto del actual.");

            foreach (var hashOtro in _datos.ObtenerNipHashAdministradores(idUsuario))
            {
                if (BCrypt.Net.BCrypt.Verify(nip, hashOtro))
                    return (false, "Ese NIP ya lo usa otro Administrador. Elige uno distinto.");
            }

            return (true, string.Empty);
        }

        // =====================================================================
        // ACTIVAR / DESACTIVAR
        // =====================================================================

        public (bool Exito, string Mensaje) ValidarCambioEstado(int idUsuario, bool activar)
            => Ejecutar(() => ValidarCambioEstadoInterno(idUsuario, activar));

        public (bool Exito, string Mensaje) CambiarEstado(int idUsuario, bool activar, string nipAdministrador)
            => Ejecutar(() =>
            {
                var v = ValidarCambioEstadoInterno(idUsuario, activar);
                if (!v.Exito) return v;

                var nip = ValidarNipAutoriza(nipAdministrador);
                if (!nip.Exito) return (false, nip.Mensaje);

                // Se revisa de nuevo justo antes de guardar por si otro Administrador cambió algo
                v = ValidarCambioEstadoInterno(idUsuario, activar);
                if (!v.Exito) return v;

                _datos.CambiarEstado(idUsuario, activar ? "Activo" : "Inactivo");

                var u = _datos.ObtenerPorId(idUsuario);
                return (true, $"Cuenta \"{u?.NombreUsuario}\" {(activar ? "activada" : "desactivada")} (autorizó: {nip.NombreAdmin}).");
            });

        private (bool Exito, string Mensaje) ValidarCambioEstadoInterno(int idUsuario, bool activar)
        {
            var u = ObtenerUsuarioParaEditar(idUsuario, out var error);
            if (u == null) return error;

            if (activar)
                return EstaActivo(u) ? (false, "La cuenta ya está activa.") : (true, string.Empty);

            if (!EstaActivo(u))
                return (false, "La cuenta ya está inactiva.");

            if (idUsuario == SesionActual.IdUsuario)
                return (false, "No puedes desactivar la cuenta con la que tienes la sesión abierta.");

            if (EsAdministrador(u) && _datos.ContarAdministradoresActivosConNip(idUsuario) == 0)
                return (false, "No se puede desactivar: debe quedar al menos otro Administrador activo con NIP asignado para autorizar operaciones.");

            return (true, string.Empty);
        }

        // =====================================================================
        // AUXILIARES
        // =====================================================================

        private Usuario? ObtenerUsuarioParaEditar(int idUsuario, out (bool Exito, string Mensaje) error)
        {
            var permiso = Permisos.Verificar(Operacion.AdministrarUsuarios);
            if (!permiso.Exito)
            {
                error = permiso;
                return null;
            }

            if (idUsuario <= 0)
            {
                error = (false, "Selecciona un usuario válido.");
                return null;
            }

            var u = _datos.ObtenerPorId(idUsuario);
            error = u == null ? (false, "El usuario seleccionado ya no existe.") : (true, string.Empty);
            return u;
        }

        private (bool Exito, string Mensaje, string NombreAdmin) ValidarNipAutoriza(string nipAdministrador)
        {
            var nip = _auth.ValidarNipAdministrador(nipAdministrador);
            return nip.Autorizado
                ? (true, string.Empty, nip.NombreAdmin)
                : (false, "NIP de Administrador incorrecto. No se guardó ningún cambio.", string.Empty);
        }

        /// <summary>Si el Administrador cambió su propia cuenta, la sesión toma los datos nuevos.</summary>
        private void RefrescarSesionSiEsPropia(int idUsuario)
        {
            if (idUsuario != SesionActual.IdUsuario)
                return;

            var actualizado = _datos.ObtenerPorId(idUsuario);
            if (actualizado != null)
                SesionActual.Iniciar(actualizado);
        }

        private static (bool Exito, string Mensaje) Ejecutar(Func<(bool Exito, string Mensaje)> accion)
        {
            try
            {
                return accion();
            }
            catch (Exception ex)
            {
                return (false, $"Error al consultar o guardar usuarios en MySQL:\n{ex.Message}");
            }
        }
    }
}
