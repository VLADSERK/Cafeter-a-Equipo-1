using System;
using CapaEntidades;
using MySqlConnector;

namespace CapaDatos
{
    public class ConfiguracionDatos
    {
        public ConfiguracionGeneral ObtenerConfiguracion()
        {
            const string sql = @"
                SELECT id_configuracion,
                       verde_minimo,
                       amarillo_minimo,
                       amarillo_maximo,
                       rojo_maximo,
                       limite_economico_merma
                FROM ConfiguracionGeneral
                ORDER BY id_configuracion ASC
                LIMIT 1;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new ConfiguracionGeneral
                {
                    IdConfiguracion = reader.GetInt32("id_configuracion"),
                    VerdeMinimo = reader.GetDecimal("verde_minimo"),
                    AmarilloMinimo = reader.GetDecimal("amarillo_minimo"),
                    AmarilloMaximo = reader.GetDecimal("amarillo_maximo"),
                    RojoMaximo = reader.GetDecimal("rojo_maximo"),
                    LimiteEconomicoMerma = reader.GetDecimal("limite_economico_merma")
                };
            }

            // Si por alguna razón la tabla estuviera vacía, devuelve valores por defecto
            return new ConfiguracionGeneral();
        }

        public bool ActualizarConfiguracion(ConfiguracionGeneral config)
        {
            const string sql = @"
                UPDATE ConfiguracionGeneral
                SET verde_minimo = @verde_minimo,
                    amarillo_minimo = @amarillo_minimo,
                    amarillo_maximo = @amarillo_maximo,
                    rojo_maximo = @rojo_maximo,
                    limite_economico_merma = @limite_economico_merma
                WHERE id_configuracion = @id_configuracion;";

            using var conn = ConexionMySql.ObtenerConexion();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@verde_minimo", config.VerdeMinimo);
            cmd.Parameters.AddWithValue("@amarillo_minimo", config.AmarilloMinimo);
            cmd.Parameters.AddWithValue("@amarillo_maximo", config.AmarilloMaximo);
            cmd.Parameters.AddWithValue("@rojo_maximo", config.RojoMaximo);
            cmd.Parameters.AddWithValue("@limite_economico_merma", config.LimiteEconomicoMerma);
            cmd.Parameters.AddWithValue("@id_configuracion", config.IdConfiguracion > 0 ? config.IdConfiguracion : 1);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}