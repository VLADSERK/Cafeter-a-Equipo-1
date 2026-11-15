using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapaPresentacion.Estilos
{
    public class CajaTextoRedondeada : UserControl
    {
        private readonly TextBox _txt = new TextBox();
        private bool _enfocado = false;
        private bool _tieneError = false;

        public int RadioBorde { get; set; } = 10;

        public string Texto
        {
            get => _txt.Text;
            set => _txt.Text = value;
        }

        public string Placeholder
        {
            get => _txt.PlaceholderText;
            set => _txt.PlaceholderText = value;
        }

        public bool EsContrasena
        {
            get => _txt.UseSystemPasswordChar;
            set => _txt.UseSystemPasswordChar = value;
        }

        public bool TieneError
        {
            get => _tieneError;
            set
            {
                _tieneError = value;
                _txt.ForeColor = value ? TemaCafeteria.ErrorRojo : TemaCafeteria.TextoOscuro;
                Invalidate();
            }
        }

        public CajaTextoRedondeada()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Size = new Size(340, 44);
            Padding = new Padding(14, 11, 14, 10);

            _txt.BorderStyle = BorderStyle.None;
            _txt.Dock = DockStyle.Fill;
            _txt.Font = TemaCafeteria.FuenteInput;
            _txt.ForeColor = TemaCafeteria.TextoOscuro;
            _txt.BackColor = Color.White;

            _txt.Enter += (s, e) => { _enfocado = true; Invalidate(); };
            _txt.Leave += (s, e) => { _enfocado = false; Invalidate(); };
            _txt.TextChanged += (s, e) =>
            {
                if (_tieneError) TieneError = false;
                OnTextChanged(e);
            };

            Controls.Add(_txt);
            Click += (s, e) => _txt.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color colorBorde = _tieneError
                ? TemaCafeteria.ErrorRojo
                : (_enfocado ? TemaCafeteria.CafePrimario : TemaCafeteria.BordeSuave);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = PanelRedondeado.CrearRutaRedondeada(rect, RadioBorde);
            using Pen pen = new Pen(colorBorde, _enfocado || _tieneError ? 1.5F : 1F);
            e.Graphics.DrawPath(pen, path);
        }
    }
}