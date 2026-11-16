namespace CapaEntidades
{
    public class Rol
    {
        public int IdRol { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class Usuario
    {
        public int IdUsuario { get; set; }
        public int IdRol { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public string ContrasenaHash { get; set; } = string.Empty;
        public string? NipHash { get; set; }
        public string Estado { get; set; } = "Activo";

        // Propiedad auxiliar de lectura (JOIN con Rol)
        public string NombreRol { get; set; } = string.Empty;
    }

    public class ConfiguracionGeneral
    {
        public int IdConfiguracion { get; set; }
        public decimal VerdeMinimo { get; set; } = 50.00m;
        public decimal AmarilloMinimo { get; set; } = 21.00m;
        public decimal AmarilloMaximo { get; set; } = 49.00m;
        public decimal RojoMaximo { get; set; } = 20.00m;
        public decimal LimiteEconomicoMerma { get; set; } = 100.00m;
    }
}
