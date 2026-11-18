using System;
using MySql.Data.MySqlClient;

namespace CapaDatos
{
    public static class ConexionMySql
    {
        // Ajusta User ID y Password según tu instancia local de MySQL
        private static string _cadenaConexion =
            "Server=localhost;Port=3306;Database=cafeteria_db;User ID=root;Password=123456;SslMode=Preferred;";

        public static void ConfigurarCadena(string cadenaConexion)
        {
            if (!string.IsNullOrWhiteSpace(cadenaConexion))
                _cadenaConexion = cadenaConexion;
        }

        public static MySqlConnection ObtenerConexion()
        {
            var conexion = new MySqlConnection(_cadenaConexion);
            conexion.Open();
            return conexion;
        }

        public static bool ProbarConexion(out string mensajeError)
        {
            try
            {
                using var conn = ObtenerConexion();
                mensajeError = string.Empty;
                return conn.State == System.Data.ConnectionState.Open;
            }
            catch (Exception ex)
            {
                mensajeError = ex.Message;
                return false;
            }
        }
    }
}