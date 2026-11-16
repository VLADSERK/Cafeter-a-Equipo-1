using System;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Controles.Admin;

namespace CapaPresentaciòn.Formularios
{
    public partial class FrmPrincipalAdmin : Form
    {
        private Panel _pnlSidebar = null!;
        private Panel _panelContenido = null!;
        private Label _lblMarcaSistema = null!;
        private Panel _sepInferior = null!;

        // Los 7 botones exactos del menú lateral
        private Button _btnNavDashboard = null!;
        private Button _btnNavInventario = null!;
        private Button _btnNavCatalogo = null!;
        private Button _btnNavAutorizaciones = null!;
        private Button _btnNavHistorial = null!;
        private Button _btnNavReporteFinanciero = null!;
        private Button _btnNavConfiguracion = null!;
        private Button _btnNavSalir = null!;

        private Button? _botonActivoActual;

        public FrmPrincipalAdmin()
        {
            InitializeComponent();
            ConfigurarVentana();
            MostrarDashboardAdmin();
        }

        private void ConfigurarVentana()
        {
            Text = "Sistema de Gestión de Cafetería - Panel de Administración";
            BackColor = TemaCafeteria.FondoCrema;
            MinimumSize = new Size(1100, 720);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;

            // 1. Barra lateral izquierda con borde sutil y estética limpia
            _pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = ColorTranslator.FromHtml("#EAE6E1"),
                Visible = false
            };
            _pnlSidebar.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, _pnlSidebar.Width - 1, 0, _pnlSidebar.Width - 1, _pnlSidebar.Height);
            };

            _lblMarcaSistema = new Label
            {
                Text = "SISTEMA DE GESTIÓN",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(22, 24),
                AutoSize = true
            };

            int yInicio = 68;
            int pasoY = 48;

            _btnNavDashboard = CrearBotonLateral("Dashboard", yInicio, () => MostrarDashboardAdmin());
            _btnNavInventario = CrearBotonLateral("Inventario", yInicio + pasoY, () => AbrirInventarioGlobal());
            _btnNavCatalogo = CrearBotonLateral("Catálogo y recetas", yInicio + pasoY * 2, () => AbrirCatalogoRecetas());
            _btnNavAutorizaciones = CrearBotonLateral("Autorización pendiente", yInicio + pasoY * 3, () => AbrirAuditoriaMermas());
            _btnNavHistorial = CrearBotonLateral("Historial", yInicio + pasoY * 4, () => AbrirHistorial());
            _btnNavReporteFinanciero = CrearBotonLateral("Reporte financiero", yInicio + pasoY * 5, () => AbrirReporteFinanciero());
            _btnNavConfiguracion = CrearBotonLateral("Configuración global", yInicio + pasoY * 6, () => AbrirConfiguracionGlobal());

            // Línea divisoria inferior
            _sepInferior = new Panel
            {
                Height = 1,
                Width = 206,
                BackColor = ColorTranslator.FromHtml("#C8C2BA")
            };

            // Botón inferior Cerrar sesión en rojo
            _btnNavSalir = new Button
            {
                Text = "Cerrar sesión",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#C62828"),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Size = new Size(214, 42),
                Cursor = Cursors.Hand
            };
            _btnNavSalir.FlatAppearance.BorderSize = 0;
            _btnNavSalir.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#F5E6E6");
            _btnNavSalir.Click += (s, e) => Close();

            _pnlSidebar.Controls.AddRange(new Control[]
            {
                _lblMarcaSistema,
                _btnNavDashboard,
                _btnNavInventario,
                _btnNavCatalogo,
                _btnNavAutorizaciones,
                _btnNavHistorial,
                _btnNavReporteFinanciero,
                _btnNavConfiguracion,
                _sepInferior,
                _btnNavSalir
            });

            _pnlSidebar.Resize += (s, e) =>
            {
                _sepInferior.Location = new Point(22, _pnlSidebar.Height - 86);
                _btnNavSalir.Location = new Point(18, _pnlSidebar.Height - 72);
            };

            // 2. Contenedor principal dinámico
            _panelContenido = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TemaCafeteria.FondoCrema
            };

            Controls.Add(_panelContenido);
            Controls.Add(_pnlSidebar);
        }

        private Button CrearBotonLateral(string texto, int posY, Action accion)
        {
            Button btn = new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 10, 0),
                Location = new Point(18, posY),
                Size = new Size(214, 42),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#DFD9D2");

            // Dibujar esquinas redondeadas en el botón activo
            btn.Paint += (s, e) =>
            {
                if (_botonActivoActual == btn)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    using GraphicsPath path = CrearRutaRedondeada(rect, 10);
                    using SolidBrush brush = new SolidBrush(ColorTranslator.FromHtml("#5C5650"));
                    using Pen pen = new Pen(ColorTranslator.FromHtml("#2E1E16"), 1.5f);

                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);

                    TextRenderer.DrawText(
                        e.Graphics,
                        btn.Text,
                        new Font("Segoe UI", 10F, FontStyle.Bold),
                        new Rectangle(14, 0, btn.Width - 20, btn.Height),
                        Color.White,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left
                    );
                }
            };

            btn.Click += (s, e) => accion();
            return btn;
        }

        private static GraphicsPath CrearRutaRedondeada(Rectangle rect, int radio)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radio * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void MarcarBotonActivo(Button? activo)
        {
            _botonActivoActual = activo;
            Button[] botones =
            {
                _btnNavDashboard,
                _btnNavInventario,
                _btnNavCatalogo,
                _btnNavAutorizaciones,
                _btnNavHistorial,
                _btnNavReporteFinanciero,
                _btnNavConfiguracion
            };

            foreach (var b in botones)
            {
                bool esActivo = b == activo;
                b.BackColor = Color.Transparent;
                b.ForeColor = esActivo ? Color.White : TemaCafeteria.TextoOscuro;
                b.Font = new Font("Segoe UI", 10F, esActivo ? FontStyle.Bold : FontStyle.Regular);
                b.Invalidate();
            }
        }

        private void CambiarVista(UserControl nuevaVista, bool mostrarSidebar, Button? botonActivo = null)
        {
            _pnlSidebar.Visible = mostrarSidebar;
            MarcarBotonActivo(botonActivo);

            _panelContenido.Controls.Clear();
            nuevaVista.Dock = DockStyle.Fill;
            _panelContenido.Controls.Add(nuevaVista);
            nuevaVista.BringToFront();
        }

        public void MostrarDashboardAdmin()
        {
            UC_MenuAdmin menuAdmin = new UC_MenuAdmin();

            menuAdmin.IrAInventario += (s, e) => AbrirInventarioGlobal();
            menuAdmin.IrACatalogo += (s, e) => AbrirCatalogoRecetas();
            menuAdmin.IrAAutorizaciones += (s, e) => AbrirAuditoriaMermas();
            menuAdmin.IrAHistorial += (s, e) => AbrirHistorial();
            menuAdmin.IrAReporteFinanciero += (s, e) => AbrirReporteFinanciero();
            menuAdmin.IrAConfiguracion += (s, e) => AbrirConfiguracionGlobal();
            menuAdmin.SalirSesion += (s, e) => Close();

            CambiarVista(menuAdmin, mostrarSidebar: false, botonActivo: _btnNavDashboard);
        }

        private void AbrirInventarioGlobal()
        {
            UC_InventarioAdmin vistaInventario = new UC_InventarioAdmin();
            CambiarVista(vistaInventario, mostrarSidebar: true, botonActivo: _btnNavInventario);
        }

        private void AbrirCatalogoRecetas()
        {
            UC_CatalogoRecetas vistaCatalogo = new UC_CatalogoRecetas();
            CambiarVista(vistaCatalogo, mostrarSidebar: true, botonActivo: _btnNavCatalogo);
        }

        private void AbrirAuditoriaMermas()
        {
            UC_AutorizacionMermas vistaAutorizaciones = new UC_AutorizacionMermas();
            CambiarVista(vistaAutorizaciones, mostrarSidebar: true, botonActivo: _btnNavAutorizaciones);
        }
        private void AbrirHistorial()
        {
            UC_HistorialVentas vistaHistorial = new UC_HistorialVentas();
            CambiarVista(vistaHistorial, mostrarSidebar: true, botonActivo: _btnNavHistorial);
        }

        private void AbrirReporteFinanciero()
        {
            UC_ReporteFinanciero vistaReporte = new UC_ReporteFinanciero();
            CambiarVista(vistaReporte, mostrarSidebar: true, botonActivo: _btnNavReporteFinanciero);
        }

        private void AbrirConfiguracionGlobal()
        {
            UC_ConfiguracionGlobal vistaConfig = new UC_ConfiguracionGlobal();
            CambiarVista(vistaConfig, mostrarSidebar: true, botonActivo: _btnNavConfiguracion);
        }
    }
}