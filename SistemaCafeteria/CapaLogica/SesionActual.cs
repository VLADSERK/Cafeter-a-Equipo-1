using CapaEntidades;

namespace CapaLogica
{
    public static class SesionActual
    {
        public static Usuario? UsuarioAutenticado { get; private set; }

        public static bool HaySesionActiva => UsuarioAutenticado != null;
        public static int IdUsuario => UsuarioAutenticado?.IdUsuario ?? 0;
        public static string NombreUsuario => UsuarioAutenticado?.Nombre ?? "Invitado";
        public static string Rol => UsuarioAutenticado?.NombreRol ?? string.Empty;

        public static void Iniciar(Usuario usuario)
        {
            UsuarioAutenticado = usuario;
        }

        public static void Cerrar()
        {
            UsuarioAutenticado = null;
        }
    }
}