using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CapaLogica;

namespace CapaPresentacion.Estilos
{
    /// <summary>
    /// Colores, símbolos y dibujo del semáforo de existencias (RIU-03).
    /// La regla de cálculo vive en CapaLogica.SemaforoExistencias; aquí solo se presenta.
    /// </summary>
    public static class SemaforoVisual
    {
        public static Color Acento(NivelSemaforo nivel) => nivel switch
        {
            NivelSemaforo.Verde => ColorTranslator.FromHtml("#1F6B3A"),
            NivelSemaforo.Amarillo => ColorTranslator.FromHtml("#B26A00"),
            NivelSemaforo.Rojo => ColorTranslator.FromHtml("#C62828"),
            _ => ColorTranslator.FromHtml("#5F5A55")
        };

        public static Color FondoSuave(NivelSemaforo nivel) => nivel switch
        {
            NivelSemaforo.Verde => ColorTranslator.FromHtml("#EAF3EE"),
            NivelSemaforo.Amarillo => ColorTranslator.FromHtml("#FFF4E5"),
            NivelSemaforo.Rojo => ColorTranslator.FromHtml("#FFEBEE"),
            _ => ColorTranslator.FromHtml("#F1EFEC")
        };

        /// <summary>Color del círculo del semáforo (Blanco se dibuja relleno blanco con borde gris).</summary>
        public static Color Relleno(NivelSemaforo nivel) => nivel switch
        {
            NivelSemaforo.Verde => TemaCafeteria.SemaforoVerde,
            NivelSemaforo.Amarillo => TemaCafeteria.SemaforoAmarillo,
            NivelSemaforo.Rojo => TemaCafeteria.SemaforoRojo,
            _ => Color.White
        };

        /// <summary>Etiqueta corta para combos y tarjetas: "Verde", "Amarillo"...</summary>
        public static string Etiqueta(NivelSemaforo nivel) => nivel.ToString();

        /// <summary>
        /// Dibuja en una celda un círculo con el color del estado y su etiqueta textual
        /// (el texto evita depender solo del color).
        /// </summary>
        private const int Diametro = 14;
        private const int MargenIzquierdo = 10;
        private static readonly Font FuenteCelda = new("Segoe UI", 9F, FontStyle.Bold);

        /// <summary>Ancho del círculo más la etiqueta, desde el borde izquierdo de la celda.</summary>
        public static int AnchoContenido(NivelSemaforo nivel)
            => MargenIzquierdo + Diametro + 6 + TextRenderer.MeasureText(Etiqueta(nivel), FuenteCelda).Width;

        public static void PintarCelda(DataGridViewCellPaintingEventArgs e, NivelSemaforo nivel, bool centrar = false)
        {
            // Fondo, selección y también el borde de la celda: antes solo se pintaba el fondo
            // y la línea de la cuadrícula quedaba cortada en esta columna.
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);

            var g = e.Graphics!;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int diametro = Diametro;
            string texto = Etiqueta(nivel);
            var fuente = FuenteCelda;
            Size medida = TextRenderer.MeasureText(texto, fuente);

            int anchoTotal = diametro + 8 + medida.Width;
            int x = centrar
                ? e.CellBounds.X + (e.CellBounds.Width - anchoTotal) / 2
                : e.CellBounds.X + MargenIzquierdo;
            int y = e.CellBounds.Y + (e.CellBounds.Height - diametro) / 2;

            using (var relleno = new SolidBrush(Relleno(nivel)))
                g.FillEllipse(relleno, x, y, diametro, diametro);
            using (var borde = new Pen(nivel == NivelSemaforo.Blanco ? ColorTranslator.FromHtml("#9E9E9E") : Acento(nivel), 1.5F))
                g.DrawEllipse(borde, x, y, diametro, diametro);

            var rectTexto = new Rectangle(x + diametro + 6, e.CellBounds.Y, e.CellBounds.Right - (x + diametro + 6), e.CellBounds.Height);
            TextRenderer.DrawText(g, texto, fuente, rectTexto, Acento(nivel),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            e.Handled = true;
        }
    }
}
