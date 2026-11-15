using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapaPresentacion.Estilos
{
    public class PanelRedondeado : Panel
    {
        public int RadioBorde { get; set; } = 20;
        public Color ColorBorde { get; set; } = ColorTranslator.FromHtml("#EAE5DF");
        public int GrosorBorde { get; set; } = 1;

        public PanelRedondeado()
        {
            DoubleBuffered = true;
            BackColor = TemaCafeteria.SuperficieTarjeta;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = CrearRutaRedondeada(rect, RadioBorde);
            using SolidBrush relleno = new SolidBrush(BackColor);
            using Pen lapiz = new Pen(ColorBorde, GrosorBorde);

            Region = new Region(CrearRutaRedondeada(new Rectangle(0, 0, Width, Height), RadioBorde));
            e.Graphics.FillPath(relleno, path);
            if (GrosorBorde > 0)
                e.Graphics.DrawPath(lapiz, path);
        }

        public static GraphicsPath CrearRutaRedondeada(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}