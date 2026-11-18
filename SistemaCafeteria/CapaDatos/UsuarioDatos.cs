using System;
using System.Collections.Generic;
using CapaEntidades;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    public class UsuarioDatos
    {
        public Usuario? ObtenerPorNombreUsuario(string nombreUsuario)
        {
            const string sql = @"
                SELECT u.id_usuario,
                       u.id_rol,
                       u.nombre,
                       u.usuario,
                       u.contrasena_hash,
                       u.nip_hash,
                       u.estado,
                       r.nombre AS nombre_rol
                FROM Usuario u
                INNER JOIN Rol r ON u.id_rol = r.id_rol
                WHERE u.usuario = @usuario
                LIMIT 1;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@usuario", nombreUsuario.Trim());

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new Usuario
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

        public List<Usuario> ObtenerAdministradoresActivos()
        {
            var lista = new List<Usuario>();
            const string sql = @"
                SELECT u.id_usuario, u.id_rol, u.nombre, u.usuario, u.contrasena_hash, u.nip_hash, u.estado, r.nombre AS nombre_rol
                FROM Usuario u
                INNER JOIN Rol r ON u.id_rol = r.id_rol
                WHERE r.nombre = 'Administrador' AND u.estado = 'Activo' AND u.nip_hash IS NOT NULL;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new Usuario
                {
                    IdUsuario = reader.GetInt32("id_usuario"),
                    IdRol = reader.GetInt32("id_rol"),
                    Nombre = reader.GetString("nombre"),
                    NombreUsuario = reader.GetString("usuario"),
                    ContrasenaHash = reader.GetString("contrasena_hash"),
                    NipHash = reader.GetString("nip_hash"),
                    Estado = reader.GetString("estado"),
                    NombreRol = reader.GetString("nombre_rol")
                });
            }

            return lista;
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
    }
}