using System.Drawing;

namespace CapaPresentacion.Estilos
{
    public static class TemaCafeteria
    {
        // Fondos y superficies principales
        public static readonly Color FondoCrema = ColorTranslator.FromHtml("#F7F3EE");
        public static readonly Color SuperficieTarjeta = ColorTranslator.FromHtml("#FFFFFF");
        public static readonly Color BarraLateralOscura = ColorTranslator.FromHtml("#2E1E16");

        // Botones y acentos de marca
        public static readonly Color CafePrimario = ColorTranslator.FromHtml("#543726");
        public static readonly Color CafeHover = ColorTranslator.FromHtml("#3E271A");
        public static readonly Color BordeSuave = ColorTranslator.FromHtml("#E2DDD7");

        // Tipografía
        public static readonly Color TextoOscuro = ColorTranslator.FromHtml("#2E231D");
        public static readonly Color TextoSecundario = ColorTranslator.FromHtml("#7A716A");
        public static readonly Color TextoPlaceholder = ColorTranslator.FromHtml("#A39B94");

        // Estados de validación y Semáforo (RF-08 / RIU-03)
        public static readonly Color ErrorRojo = ColorTranslator.FromHtml("#D32F2F");
        public static readonly Color SemaforoVerde = ColorTranslator.FromHtml("#2E7D32");
        public static readonly Color SemaforoAmarillo = ColorTranslator.FromHtml("#F9A825");
        public static readonly Color SemaforoRojo = ColorTranslator.FromHtml("#C62828");
        public static readonly Color SemaforoBlanco = ColorTranslator.FromHtml("#E0E0E0");

        // Fuentes estándar (Windows 11 - RNF-12)
        public static readonly Font FuenteTituloPrincipal = new Font("Segoe UI", 24F, FontStyle.Bold);
        public static readonly Font FuenteSubtitulo = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        public static readonly Font FuenteEtiqueta = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        public static readonly Font FuenteInput = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        public static readonly Font FuenteBoton = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        public static readonly Font FuenteError = new Font("Segoe UI", 9F, FontStyle.Regular);
    }
}