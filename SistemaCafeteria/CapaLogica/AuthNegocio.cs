using System;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class AuthNegocio
    {
        private readonly UsuarioDatos _usuarioDatos = new();

        /// <summary>
        /// Crea los 2 usuarios iniciales con contraseñas y NIP cifrados en bcrypt
        /// únicamente si la tabla Usuario en MySQL está vacía.
        /// </summary>
        public void AsegurarUsuariosIniciales()
        {
            if (_usuarioDatos.ContarUsuarios() > 0)
                return;

            // Rol 1 = Administrador | Rol 2 = Cajero (según el script SQL semilla)
            var admin = new Usuario
            {
                IdRol = 1,
                Nombre = "Administrador General",
                NombreUsuario = "admin",
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 11),
                NipHash = BCrypt.Net.BCrypt.HashPassword("1234", workFactor: 11),
                Estado = "Activo"
            };

            var cajero = new Usuario
            {
                IdRol = 2,
                Nombre = "Sofía Ramírez",
                NombreUsuario = "cajero",
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 11),
                NipHash = null,
                Estado = "Activo"
            };

            _usuarioDatos.InsertarUsuario(admin);
            _usuarioDatos.InsertarUsuario(cajero);
        }

        public (bool Exito, string Mensaje, Usuario? Usuario) IniciarSesion(string nombreUsuario, string contrasenaPlano)
        {
            if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(contrasenaPlano))
            {
                return (false, "Ingresa tu usuario y contraseña para continuar.", null);
            }

            try
            {
                // Garantiza que existan los usuarios semilla la primera vez que se conecta a MySQL
                AsegurarUsuariosIniciales();

                Usuario? usuarioDb = _usuarioDatos.ObtenerPorNombreUsuario(nombreUsuario);
                if (usuarioDb == null)
                {
                    return (false, "Usuario o contraseña incorrectos.", null);
                }

                if (!string.Equals(usuarioDb.Estado, "Activo", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Tu cuenta está inactiva. Contacta al Administrador.", null);
                }

                bool passwordValida = BCrypt.Net.BCrypt.Verify(contrasenaPlano, usuarioDb.ContrasenaHash);
                if (!passwordValida)
                {
                    return (false, "Usuario o contraseña incorrectos.", null);
                }

                SesionActual.Iniciar(usuarioDb);
                return (true, $"Bienvenido, {usuarioDb.Nombre}", usuarioDb);
            }
            catch (Exception ex)
            {
                return (false, $"Error de conexión con la base de datos MySQL:\n{ex.Message}", null);
            }
        }

        /// <summary>
        /// Valida el NIP de autorización contra cualquier Administrador activo en MySQL
        /// y devuelve el ID del Administrador que autorizó (para guardarlo en Merma.id_usuario_autoriza).
        /// </summary>
        public (bool Autorizado, int IdAdminAutoriza, string NombreAdmin) ValidarNipAdministrador(string nipPlano)
        {
            if (string.IsNullOrWhiteSpace(nipPlano))
                return (false, 0, string.Empty);

            var admins = _usuarioDatos.ObtenerAdministradoresActivos();
            foreach (var admin in admins)
            {
                if (!string.IsNullOrEmpty(admin.NipHash) && BCrypt.Net.BCrypt.Verify(nipPlano.Trim(), admin.NipHash))
                {
                    return (true, admin.IdUsuario, admin.Nombre);
                }
            }

            return (false, 0, string.Empty);
        }
    }
}