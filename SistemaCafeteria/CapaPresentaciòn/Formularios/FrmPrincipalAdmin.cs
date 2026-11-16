using CapaPresentaciòn.Controles.Admin; // Importante para reconocer la nueva carpeta Admin
using CapaPresentacion.Estilos;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapaPresentaciòn.Formularios
{
    public partial class FrmPrincipalAdmin : Form
    {
        private Panel _pnlSidebar = null!;
        private Panel _panelContenido = null!;

        private Button _btnNavDashboard = null!;
        private Button _btnNavInventarioGeneral = null!;
        private Button _btnNavAuditoriaMermas = null!;
        private Button _btnNavUsuarios = null!;
        private Button _btnNavSalir = null!;

        public FrmPrincipalAdmin()
        {
            InitializeComponent();
            ConfigurarVentana();
            MostrarDashboardAdmin(); // Carga el menú principal del admin al iniciar
        }

        private void ConfigurarVentana()
        {
            Text = "Sistema de Gestión de Cafetería - Panel de Administración";
            BackColor = TemaCafeteria.FondoCrema;
            MinimumSize = new Size(1100, 720);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;

            // 1. Barra lateral izquierda oscura (#2E1E16) para el Admin
            _pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 255,
                BackColor = ColorTranslator.FromHtml("#2E1E16"),
                Visible = false
            };

            _btnNavDashboard = CrearBotonLateral("Dashboard", 28, () => MostrarDashboardAdmin());
            _btnNavInventarioGeneral = CrearBotonLateral("Inventario y Lotes", 84, () => AbrirInventarioGlobal());
            _btnNavAuditoriaMermas = CrearBotonLateral("Auditoría de Mermas", 140, () => AbrirAuditoriaMermas());
            _btnNavUsuarios = CrearBotonLateral("Gestión de Usuarios", 196, () => AbrirGestionUsuarios());

            _btnNavSalir = new Button
            {
                Text = "↪   Cerrar sesión",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#D7CCC8"),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(22, 0, 0, 0),
                Size = new Size(225, 46),
                Cursor = Cursors.Hand
            };
            _btnNavSalir.FlatAppearance.BorderSize = 0;
            _btnNavSalir.Click += (s, e) => Close();

            _pnlSidebar.Controls.AddRange(new Control[]
            {
                _btnNavDashboard, _btnNavInventarioGeneral, _btnNavAuditoriaMermas, _btnNavUsuarios, _btnNavSalir
            });
            _pnlSidebar.Resize += (s, e) => _btnNavSalir.Location = new Point(15, _pnlSidebar.Height - 75);

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
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#EFEBE9"),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 12, 0),
                Location = new Point(15, posY),
                Size = new Size(225, 46),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => accion();
            return btn;
        }

        private void MarcarBotonActivo(Button? activo)
        {
            Button[] botones = { _btnNavDashboard, _btnNavInventarioGeneral, _btnNavAuditoriaMermas, _btnNavUsuarios };
            foreach (var b in botones)
            {
                bool esActivo = b == activo;
                b.BackColor = esActivo ? ColorTranslator.FromHtml("#EAD5B7") : Color.Transparent;
                b.ForeColor = esActivo ? ColorTranslator.FromHtml("#2E1E16") : ColorTranslator.FromHtml("#EFEBE9");
                b.Font = new Font("Segoe UI", 10.5F, esActivo ? FontStyle.Bold : FontStyle.Regular);
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
            // Instanciamos el menú principal que acabas de crear
            UC_MenuAdmin menuAdmin = new UC_MenuAdmin();

            // Conectamos los botones de tu diseño a sus acciones
            menuAdmin.IrAInventario += (s, e) => AbrirInventarioGlobal();
            menuAdmin.IrACatalogo += (s, e) => MessageBox.Show("Módulo de Catálogo y Recetas en desarrollo", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
            menuAdmin.IrAConfiguracion += (s, e) => MessageBox.Show("Módulo de Configuración Global en desarrollo", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
            menuAdmin.IrAAutorizaciones += (s, e) => MessageBox.Show("Módulo de Autorizaciones Pendientes en desarrollo", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
            menuAdmin.IrAHistorial += (s, e) => MessageBox.Show("Módulo de Historial en desarrollo", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
            menuAdmin.IrAReporteFinanciero += (s, e) => MessageBox.Show("Módulo de Reporte Financiero en desarrollo", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Botón inferior rojo para cerrar sesión y volver al login
            menuAdmin.SalirSesion += (s, e) => Close();

            // Se muestra en el contenedor central con la barra lateral oculta
            CambiarVista(menuAdmin, mostrarSidebar: false, botonActivo: _btnNavDashboard);
        }

        private void AbrirInventarioGlobal()
        {
            MessageBox.Show("Módulo de Inventario General del Administrador", "En desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AbrirAuditoriaMermas()
        {
            MessageBox.Show("Módulo de Auditoría Global de Mermas", "En desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AbrirGestionUsuarios()
        {
            MessageBox.Show("Módulo de Gestión de Usuarios y Roles", "En desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}