using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapaPresentacion.Estilos
{
    public class BotonRedondeado : Button
    {
        public int RadioBorde { get; set; } = 10;
        public Color ColorNormal { get; set; } = TemaCafeteria.CafePrimario;
        public Color ColorHover { get; set; } = TemaCafeteria.CafeHover;
        private bool _sobreBoton = false;

        public BotonRedondeado()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = ColorNormal;
            ForeColor = Color.White;
            Font = TemaCafeteria.FuenteBoton;
            Cursor = Cursors.Hand;
            Size = new Size(340, 46);

            MouseEnter += (s, e) => { _sobreBoton = true; Invalidate(); };
            MouseLeave += (s, e) => { _sobreBoton = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width, Height);

            using GraphicsPath path = PanelRedondeado.CrearRutaRedondeada(rect, RadioBorde);
            Region = new Region(path);

            using SolidBrush brush = new SolidBrush(_sobreBoton ? ColorHover : ColorNormal);
            e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                rect,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
