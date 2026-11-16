using System;
using System.Drawing;
using System.Windows.Forms;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_MenuAdmin : UserControl
    {
        // Eventos para la navegación hacia los submódulos del administrador
        public event EventHandler? IrAInventario;
        public event EventHandler? IrACatalogo;
        public event EventHandler? IrAConfiguracion;
        public event EventHandler? IrAAutorizaciones;
        public event EventHandler? IrAHistorial;
        public event EventHandler? IrAReporteFinanciero;
        public event EventHandler? SalirSesion;

        private Panel _pnlContenedorCentral = null!;
        private Label _lblTitulo = null!;

        // Tarjetas de resumen superior
        private Panel _cardVentas = null!;
        private Panel _cardFinanciero = null!;
        private Panel _cardAlertas = null!;

        // Botones del menú
        private Button _btnInventario = null!;
        private Button _btnCatalogo = null!;
        private Button _btnConfiguracion = null!;
        private Button _btnAutorizaciones = null!;
        private Button _btnHistorial = null!;
        private Button _btnReporteFinanciero = null!;
        private Button _btnCerrarSesion = null!;

        public UC_MenuAdmin()
        {
           /// InitializeComponent();
            ConfigurarInterfaz();
        }

        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // Panel contenedor principal centrado para diseño responsivo
            _pnlContenedorCentral = new Panel
            {
                Size = new Size(920, 620),
                BackColor = Color.Transparent
            };
            _pnlContenedorCentral.Location = new Point(
                Math.Max(0, (ClientSize.Width - _pnlContenedorCentral.Width) / 2),
                Math.Max(0, (ClientSize.Height - _pnlContenedorCentral.Height) / 2)
            );
            Resize += (s, e) => CentrarContenedor();

            // 1. Título principal
            _lblTitulo = new Label
            {
                Text = "Centro de Control del Administrador",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(920, 40),
                Location = new Point(0, 10)
            };

            // 2. Tarjetas de Resumen Superior (Y = 65)
            _cardVentas = CrearTarjetaResumen("Ventas del Día", "$7,256.25", Color.FromArgb(46, 125, 50), 0);
            _cardFinanciero = CrearTarjetaResumen("Estado Financiero", "Balance Positivo", Color.FromArgb(0, 122, 255), 308);
            _cardAlertas = CrearTarjetaResumen("Alertas de Inventario", "3", Color.FromArgb(211, 47, 47), 616);

            // 3. Botones de Navegación Grid (2 columnas x 3 filas) (Y = 195)
            int yInicio = 195;
            int anchoBtn = 446;
            int altoBtn = 65;
            int gapX = 28;
            int gapY = 16;

            _btnInventario = CrearBotonMenu("Inventario", 0, yInicio, anchoBtn, altoBtn);
            _btnCatalogo = CrearBotonMenu("Catálogo y Recetas", anchoBtn + gapX, yInicio, anchoBtn, altoBtn);

            _btnConfiguracion = CrearBotonMenu("Configuración global", 0, yInicio + altoBtn + gapY, anchoBtn, altoBtn);
            _btnAutorizaciones = CrearBotonMenu("Autorizaciones Pendientes", anchoBtn + gapX, yInicio + altoBtn + gapY, anchoBtn, altoBtn);

            _btnHistorial = CrearBotonMenu("Historial", 0, yInicio + (altoBtn + gapY) * 2, anchoBtn, altoBtn);
            _btnReporteFinanciero = CrearBotonMenu("Reporte Financiero", anchoBtn + gapX, yInicio + (altoBtn + gapY) * 2, anchoBtn, altoBtn);

            // Asignar eventos de clic
            _btnInventario.Click += (s, e) => IrAInventario?.Invoke(this, EventArgs.Empty);
            _btnCatalogo.Click += (s, e) => IrACatalogo?.Invoke(this, EventArgs.Empty);
            _btnConfiguracion.Click += (s, e) => IrAConfiguracion?.Invoke(this, EventArgs.Empty);
            _btnAutorizaciones.Click += (s, e) => IrAAutorizaciones?.Invoke(this, EventArgs.Empty);
            _btnHistorial.Click += (s, e) => IrAHistorial?.Invoke(this, EventArgs.Empty);
            _btnReporteFinanciero.Click += (s, e) => IrAReporteFinanciero?.Invoke(this, EventArgs.Empty);

            // 4. Botón inferior Cerrar Sesión (Rojo)
            _btnCerrarSesion = new Button
            {
                Text = "Cerrar Sesión",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(211, 47, 47), // Rojo
                FlatStyle = FlatStyle.Flat,
                Size = new Size(920, 55),
                Location = new Point(0, 535),
                Cursor = Cursors.Hand
            };
            _btnCerrarSesion.FlatAppearance.BorderSize = 0;
            _btnCerrarSesion.Click += (s, e) => SalirSesion?.Invoke(this, EventArgs.Empty);

            // Agregar controles al contenedor central
            _pnlContenedorCentral.Controls.AddRange(new Control[]
            {
                _lblTitulo,
                _cardVentas, _cardFinanciero, _cardAlertas,
                _btnInventario, _btnCatalogo,
                _btnConfiguracion, _btnAutorizaciones,
                _btnHistorial, _btnReporteFinanciero,
                _btnCerrarSesion
            });

            Controls.Add(_pnlContenedorCentral);
        }

        private Panel CrearTarjetaResumen(string titulo, string valor, Color colorValor, int posX)
        {
            Panel card = new Panel
            {
                Size = new Size(288, 105),
                Location = new Point(posX, 65),
                BackColor = Color.White
            };

            Label lblTit = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(288, 25),
                Location = new Point(0, 18)
            };

            Label lblVal = new Label
            {
                Text = valor,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = colorValor,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(288, 40),
                Location = new Point(0, 48)
            };

            card.Controls.Add(lblTit);
            card.Controls.Add(lblVal);
            return card;
        }

        private Button CrearBotonMenu(string texto, int x, int y, int ancho, int alto)
        {
            Button btn = new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ancho, alto),
                Location = new Point(x, y),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 190, 180);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private void CentrarContenedor()
        {
            _pnlContenedorCentral.Location = new Point(
                Math.Max(0, (ClientSize.Width - _pnlContenedorCentral.Width) / 2),
                Math.Max(0, (ClientSize.Height - _pnlContenedorCentral.Height) / 2)
            );
        }
    }
}
