using System;
using System.Collections.Generic;
using CapaEntidades;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    public class UsuarioDatos
    {
        private const string SelectUsuario = @"
            SELECT u.id_usuario,
                   u.id_rol,
                   u.nombre,
                   u.usuario,
                   u.contrasena_hash,
                   u.nip_hash,
                   u.estado,
                   r.nombre AS nombre_rol
            FROM Usuario u
            INNER JOIN Rol r ON u.id_rol = r.id_rol";

        public Usuario? ObtenerPorNombreUsuario(string nombreUsuario)
        {
            string sql = SelectUsuario + " WHERE u.usuario = @usuario LIMIT 1;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@usuario", nombreUsuario.Trim());

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Mapear(reader) : null;
        }

        public Usuario? ObtenerPorId(int idUsuario)
        {
            string sql = SelectUsuario + " WHERE u.id_usuario = @id LIMIT 1;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idUsuario);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Mapear(reader) : null;
        }

        /// <summary>Todos los usuarios (activos e inactivos) con su rol, para la gestión de credenciales.</summary>
        public List<Usuario> ObtenerTodos()
        {
            string sql = SelectUsuario + " ORDER BY r.id_rol ASC, u.nombre ASC;";
            var lista = new List<Usuario>();

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                lista.Add(Mapear(reader));

            return lista;
        }

        public List<Usuario> ObtenerAdministradoresActivos()
        {
            string sql = SelectUsuario + " WHERE r.nombre = 'Administrador' AND u.estado = 'Activo' AND u.nip_hash IS NOT NULL;";
            var lista = new List<Usuario>();

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                lista.Add(Mapear(reader));

            return lista;
        }

        /// <summary>
        /// Hashes de NIP de todos los Administradores (activos o no) excepto el indicado,
        /// para impedir NIP repetidos: un NIP debe identificar a un solo Administrador.
        /// </summary>
        public List<string> ObtenerNipHashAdministradores(int idUsuarioExcluir)
        {
            const string sql = @"
                SELECT u.nip_hash
                FROM Usuario u
                INNER JOIN Rol r ON u.id_rol = r.id_rol
                WHERE r.nombre = 'Administrador'
                  AND u.nip_hash IS NOT NULL
                  AND u.id_usuario <> @excluir;";

            var lista = new List<string>();

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@excluir", idUsuarioExcluir);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                lista.Add(reader.GetString("nip_hash"));

            return lista;
        }

        /// <summary>
        /// Administradores activos con NIP asignado, sin contar al usuario indicado
        /// (son los que pueden seguir autorizando operaciones).
        /// </summary>
        public int ContarAdministradoresActivosConNip(int idUsuarioExcluir)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM Usuario u
                INNER JOIN Rol r ON u.id_rol = r.id_rol
                WHERE r.nombre = 'Administrador'
                  AND u.estado = 'Activo'
                  AND u.nip_hash IS NOT NULL
                  AND u.id_usuario <> @excluir;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@excluir", idUsuarioExcluir);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool ExisteNombreUsuario(string nombreUsuario, int idUsuarioExcluir)
        {
            const string sql = "SELECT COUNT(*) FROM Usuario WHERE usuario = @usuario AND id_usuario <> @excluir;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@usuario", nombreUsuario.Trim());
            cmd.Parameters.AddWithValue("@excluir", idUsuarioExcluir);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int ContarUsuarios()
        {
            const string sql = "SELECT COUNT(*) FROM Usuario;";
            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void InsertarUsuario(Usuario usuario)
        {
            const string sql = @"
                INSERT INTO Usuario (id_rol, nombre, usuario, contrasena_hash, nip_hash, estado)
                VALUES (@id_rol, @nombre, @usuario, @contrasena_hash, @nip_hash, @estado);";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_rol", usuario.IdRol);
            cmd.Parameters.AddWithValue("@nombre", usuario.Nombre);
            cmd.Parameters.AddWithValue("@usuario", usuario.NombreUsuario);
            cmd.Parameters.AddWithValue("@contrasena_hash", usuario.ContrasenaHash);
            cmd.Parameters.AddWithValue("@nip_hash", (object?)usuario.NipHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estado", usuario.Estado);
            cmd.ExecuteNonQuery();
        }

        public bool ActualizarDatos(int idUsuario, string nombre, string nombreUsuario)
        {
            const string sql = "UPDATE Usuario SET nombre = @nombre, usuario = @usuario WHERE id_usuario = @id;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@nombre", nombre.Trim());
            cmd.Parameters.AddWithValue("@usuario", nombreUsuario.Trim());
            cmd.Parameters.AddWithValue("@id", idUsuario);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool ActualizarContrasenaHash(int idUsuario, string contrasenaHash)
        {
            const string sql = "UPDATE Usuario SET contrasena_hash = @hash WHERE id_usuario = @id;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@hash", contrasenaHash);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool ActualizarNipHash(int idUsuario, string nipHash)
        {
            const string sql = "UPDATE Usuario SET nip_hash = @hash WHERE id_usuario = @id;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@hash", nipHash);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>estado: "Activo" o "Inactivo".</summary>
        public bool CambiarEstado(int idUsuario, string estado)
        {
            const string sql = "UPDATE Usuario SET estado = @estado WHERE id_usuario = @id;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static Usuario Mapear(MySqlDataReader reader) => new Usuario
        {
            IdUsuario = reader.GetInt32("id_usuario"),
            IdRol = reader.GetInt32("id_rol"),
            Nombre = reader.GetString("nombre"),
            NombreUsuario = reader.GetString("usuario"),
            ContrasenaHash = reader.GetString("contrasena_hash"),
            NipHash = reader.IsDBNull(reader.GetOrdinal("nip_hash")) ? null : reader.GetString("nip_hash"),
            Estado = reader.GetString("estado"),
            NombreRol = reader.GetString("nombre_rol")
        };
    }
}
