using System;
using System.Collections.Generic;
using System.Linq;
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
        // CONFIGURACIÓN GLOBAL · ROL DE CAJERO (nombre, usuario, contraseña, turno)
        // =====================================================================

        /// <summary>Únicos turnos válidos (mismos valores que el ENUM de Usuario.turno).</summary>
        public static readonly IReadOnlyList<string> Turnos = new[] { "Matutino", "Vespertino" };

        /// <summary>
        /// Cuenta que se edita en la tarjeta "Configuración del Rol de Cajero": la primera cuenta
        /// con rol Cajero (por antigüedad), dando preferencia a una activa. null si no hay ninguna.
        /// </summary>
        public Usuario? ObtenerCuentaCajero()
        {
            var permiso = Permisos.Verificar(Operacion.AdministrarUsuarios);
            if (!permiso.Exito)
                throw new UnauthorizedAccessException(permiso.Mensaje);

            return _datos.ObtenerTodos()
                .Where(u => string.Equals(u.NombreRol?.Trim(), Permisos.RolCajero, StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => EstaActivo(u) ? 0 : 1)
                .ThenBy(u => u.IdUsuario)
                .FirstOrDefault();
        }

        /// <summary>Turno guardado de la cuenta (Matutino, Vespertino o null si no tiene uno válido).</summary>
        public string? ObtenerTurno(int idUsuario)
        {
            var permiso = Permisos.Verificar(Operacion.AdministrarUsuarios);
            if (!permiso.Exito)
                throw new UnauthorizedAccessException(permiso.Mensaje);

            return NormalizarTurno(_datos.ObtenerTurno(idUsuario));
        }

        /// <summary>
        /// Valida sin NIP los cambios de una cuenta de Cajero/Barista. La contraseña es opcional:
        /// vacía (y sin confirmación) significa "conservar la actual". <c>HayCambios</c> indica si
        /// hay algo distinto de lo guardado.
        /// </summary>
        public (bool Exito, string Mensaje, bool HayCambios) ValidarCuentaOperativa(
            int idUsuario, string nombre, string nombreUsuario, string? nuevaContrasena, string? confirmacion, string? turno)
        {
            try
            {
                var r = ValidarCuentaOperativaInterno(idUsuario, nombre, nombreUsuario, nuevaContrasena, confirmacion, turno);
                return (r.Exito, r.Mensaje, r.HayCambios);
            }
            catch (Exception ex)
            {
                return (false, $"Error al consultar o guardar usuarios en MySQL:\n{ex.Message}", false);
            }
        }

        /// <summary>
        /// Guarda los cambios de la cuenta de Cajero/Barista en un solo UPDATE, con NIP de Administrador
        /// (RNF-13). La contraseña nueva se cifra con bcrypt (RNF-09 / RI-04).
        /// </summary>
        public (bool Exito, string Mensaje) GuardarCuentaOperativa(
            int idUsuario, string nombre, string nombreUsuario, string? nuevaContrasena, string? confirmacion,
            string? turno, string nipAdministrador)
            => Ejecutar(() =>
            {
                var v = ValidarCuentaOperativaInterno(idUsuario, nombre, nombreUsuario, nuevaContrasena, confirmacion, turno);
                if (!v.Exito) return (false, v.Mensaje);
                if (!v.HayCambios) return (true, $"La cuenta \"{nombreUsuario.Trim()}\" no tenía cambios.");

                var nip = ValidarNipAutoriza(nipAdministrador);
                if (!nip.Exito) return (false, nip.Mensaje);

                string? hash = v.CambiaContrasena
                    ? BCrypt.Net.BCrypt.HashPassword(nuevaContrasena, workFactor: FactorTrabajoBcrypt)
                    : null;

                _datos.ActualizarCuenta(idUsuario, nombre.Trim(), nombreUsuario.Trim(), hash, v.CambiaTurno, NormalizarTurno(turno));
                RefrescarSesionSiEsPropia(idUsuario);

                return (true, $"Cuenta \"{nombreUsuario.Trim()}\" actualizada" +
                              (hash != null ? " (nueva contraseña cifrada con bcrypt)" : "") +
                              $" · autorizó: {nip.NombreAdmin}.");
            });

        private (bool Exito, string Mensaje, bool HayCambios, bool CambiaContrasena, bool CambiaTurno) ValidarCuentaOperativaInterno(
            int idUsuario, string nombre, string nombreUsuario, string? nuevaContrasena, string? confirmacion, string? turno)
        {
            var datos = ValidarDatosInterno(idUsuario, nombre, nombreUsuario);
            if (!datos.Resultado.Exito) return (false, datos.Resultado.Mensaje, false, false, false);

            var u = datos.Usuario!;
            if (!Permisos.EsOperativo(u.NombreRol))
                return (false, "La cuenta seleccionada no es de Cajero ni de Barista.", false, false, false);

            bool cambiaContrasena = !string.IsNullOrEmpty(nuevaContrasena) || !string.IsNullOrEmpty(confirmacion);
            if (cambiaContrasena)
            {
                var c = ValidarContrasenaInterno(idUsuario, nuevaContrasena ?? string.Empty, confirmacion ?? string.Empty);
                if (!c.Exito) return (false, c.Mensaje, false, false, false);
            }

            // Solo Matutino o Vespertino; vacío se acepta únicamente si la cuenta aún no tiene turno
            string? turnoNuevo = NormalizarTurno(turno);
            if (!string.IsNullOrWhiteSpace(turno) && turnoNuevo == null)
                return (false, "El turno debe ser Matutino o Vespertino.", false, false, false);

            string? turnoActual = NormalizarTurno(_datos.ObtenerTurno(idUsuario));
            if (turnoNuevo == null && turnoActual != null)
                return (false, "Selecciona el turno (Matutino o Vespertino).", false, false, false);

            bool cambiaTurno = !string.Equals(turnoNuevo, turnoActual, StringComparison.Ordinal);

            bool cambiaDatos = !string.Equals(nombre.Trim(), u.Nombre, StringComparison.Ordinal)
                            || !string.Equals(nombreUsuario.Trim(), u.NombreUsuario, StringComparison.Ordinal);

            return (true, string.Empty, cambiaDatos || cambiaContrasena || cambiaTurno, cambiaContrasena, cambiaTurno);
        }

        /// <summary>Devuelve el turno con su escritura oficial, o null si está vacío o no es válido.</summary>
        private static string? NormalizarTurno(string? turno)
            => string.IsNullOrWhiteSpace(turno)
                ? null
                : Turnos.FirstOrDefault(t => string.Equals(t, turno.Trim(), StringComparison.OrdinalIgnoreCase));

        // =====================================================================
        // CONFIGURACIÓN GLOBAL · ROL DE ADMINISTRADOR (cuenta en sesión)
        // =====================================================================

        /// <summary>El Administrador con la sesión abierta (sus credenciales se editan en la tarjeta).</summary>
        public Usuario? ObtenerAdministradorEnSesion()
        {
            var permiso = Permisos.Verificar(Operacion.AdministrarUsuarios);
            if (!permiso.Exito)
                throw new UnauthorizedAccessException(permiso.Mensaje);

            return _datos.ObtenerPorId(SesionActual.IdUsuario);
        }

        /// <summary>
        /// Valida sin NIP la nueva contraseña y/o el nuevo NIP (4 dígitos, no repetido entre
        /// Administradores) de la cuenta en sesión. Campos vacíos = conservar el valor actual.
        /// </summary>
        public (bool Exito, string Mensaje, bool HayCambios) ValidarCredencialesAdministrador(
            string? nuevaContrasena, string? confirmacion, string? nuevoNip, string? confirmacionNip)
        {
            try
            {
                var r = ValidarCredencialesAdministradorInterno(nuevaContrasena, confirmacion, nuevoNip, confirmacionNip);
                return (r.Exito, r.Mensaje, r.CambiaContrasena || r.CambiaNip);
            }
            catch (Exception ex)
            {
                return (false, $"Error al consultar o guardar usuarios en MySQL:\n{ex.Message}", false);
            }
        }

        /// <summary>
        /// Guarda la contraseña y/o el NIP del Administrador en sesión (bcrypt) en un solo UPDATE.
        /// La operación se autoriza con el NIP actual de un Administrador.
        /// </summary>
        public (bool Exito, string Mensaje) GuardarCredencialesAdministrador(
            string? nuevaContrasena, string? confirmacion, string? nuevoNip, string? confirmacionNip, string nipAdministrador)
            => Ejecutar(() =>
            {
                var v = ValidarCredencialesAdministradorInterno(nuevaContrasena, confirmacion, nuevoNip, confirmacionNip);
                if (!v.Exito) return (false, v.Mensaje);
                if (!v.CambiaContrasena && !v.CambiaNip) return (true, "Las credenciales del Administrador no tenían cambios.");

                var nip = ValidarNipAutoriza(nipAdministrador);
                if (!nip.Exito) return (false, nip.Mensaje);

                string? hashContrasena = v.CambiaContrasena
                    ? BCrypt.Net.BCrypt.HashPassword(nuevaContrasena, workFactor: FactorTrabajoBcrypt)
                    : null;
                string? hashNip = v.CambiaNip
                    ? BCrypt.Net.BCrypt.HashPassword(nuevoNip!.Trim(), workFactor: FactorTrabajoBcrypt)
                    : null;

                _datos.ActualizarCredenciales(SesionActual.IdUsuario, hashContrasena, hashNip);
                RefrescarSesionSiEsPropia(SesionActual.IdUsuario);

                var cambios = new List<string>();
                if (hashContrasena != null) cambios.Add("contraseña");
                if (hashNip != null) cambios.Add("NIP");
                return (true, $"Administrador \"{SesionActual.UsuarioAutenticado?.NombreUsuario}\": {string.Join(" y ", cambios)} " +
                              $"actualizado{(cambios.Count > 1 ? "s" : "")} con bcrypt · autorizó: {nip.NombreAdmin}.");
            });

        private (bool Exito, string Mensaje, bool CambiaContrasena, bool CambiaNip) ValidarCredencialesAdministradorInterno(
            string? nuevaContrasena, string? confirmacion, string? nuevoNip, string? confirmacionNip)
        {
            int id = SesionActual.IdUsuario;
            var u = ObtenerUsuarioParaEditar(id, out var error);
            if (u == null) return (false, error.Mensaje, false, false);

            if (!EsAdministrador(u))
                return (false, "La sesión actual no es de un Administrador.", false, false);

            bool cambiaContrasena = !string.IsNullOrEmpty(nuevaContrasena) || !string.IsNullOrEmpty(confirmacion);
            if (cambiaContrasena)
            {
                var c = ValidarContrasenaInterno(id, nuevaContrasena ?? string.Empty, confirmacion ?? string.Empty);
                if (!c.Exito) return (false, $"Contraseña de Administrador: {c.Mensaje}", false, false);
            }

            bool cambiaNip = !string.IsNullOrWhiteSpace(nuevoNip) || !string.IsNullOrWhiteSpace(confirmacionNip);
            if (cambiaNip)
            {
                var n = ValidarNuevoNipInterno(id, nuevoNip ?? string.Empty, confirmacionNip ?? string.Empty);
                if (!n.Exito) return (false, $"NIP de autorización: {n.Mensaje}", false, false);
            }

            return (true, string.Empty, cambiaContrasena, cambiaNip);
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
