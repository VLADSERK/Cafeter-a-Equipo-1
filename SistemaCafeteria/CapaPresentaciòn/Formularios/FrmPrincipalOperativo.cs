using CapaPresentaciòn.Controles;
using CapaPresentacion.Estilos;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapaPresentaciòn.Formularios
{
    public partial class FrmPrincipalOperativo : Form
    {
        private Panel _pnlSidebar = null!;
        private Panel _panelContenido = null!;

        private Button _btnNavVender = null!;
        private Button _btnNavRecepcion = null!;
        private Button _btnNavInventario = null!;
        private Button _btnNavMermas = null!;
        private Button _btnNavSalir = null!;

        public FrmPrincipalOperativo()
        {
            InitializeComponent();
            ConfigurarVentana();
            MostrarMenuPrincipal();
        }

        private void ConfigurarVentana()
        {
            Text = "Sistema de Gestión de Cafetería - Operación";
            BackColor = TemaCafeteria.FondoCrema;
            MinimumSize = new Size(1100, 720);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;

            // 1. Barra lateral izquierda oscura (#2E1E16)
            // 1. Barra lateral izquierda oscura (#2E1E16)
            _pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 255,
                BackColor = ColorTranslator.FromHtml("#2E1E16"),
                Visible = false
            };

            _btnNavVender = CrearBotonLateral("Vender", 28, () => AbrirModuloVender());
            _btnNavRecepcion = CrearBotonLateral("Recepción de\nmercancía", 84, () => AbrirModuloRecepcion());
            _btnNavInventario = CrearBotonLateral("Inventario", 162, () => AbrirModuloInventario());
            _btnNavMermas = CrearBotonLateral("Mermas", 218, () => AbrirModuloMermas());

            _btnNavSalir = new Button
            {
                Text = "↪   Salir",
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
    _btnNavVender, _btnNavRecepcion, _btnNavInventario, _btnNavMermas, _btnNavSalir
            });
            _pnlSidebar.Resize += (s, e) => _btnNavSalir.Location = new Point(15, _pnlSidebar.Height - 75);

            // 2. Contenedor principal dinámico
            _panelContenido = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TemaCafeteria.FondoCrema
            };

            // Importante: agregar primero _panelContenido y luego _pnlSidebar para que Dock funcione bien
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
                Size = new Size(225, texto.Contains('\n') ? 68 : 46),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => accion();
            return btn;
        }

        private void MarcarBotonActivo(Button? activo)
        {
            Button[] botones = { _btnNavVender, _btnNavRecepcion, _btnNavInventario, _btnNavMermas };
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

        public void MostrarMenuPrincipal()
        {
            UC_MenuOperativo menu = new UC_MenuOperativo();
            menu.IrAVender += (s, e) => AbrirModuloVender();
            menu.IrARecopilacion += (s, e) => AbrirModuloRecepcion();
            menu.IrAInventario += (s, e) => AbrirModuloInventario();
            menu.IrAMerma += (s, e) => AbrirModuloMermas();
            menu.SalirSesion += (s, e) => Close();

            CambiarVista(menu, mostrarSidebar: false);
        }

        private void AbrirModuloVender()
        {
            UC_PuntoVenta vistaVenta = new UC_PuntoVenta();
            CambiarVista(vistaVenta, mostrarSidebar: true, botonActivo: _btnNavVender);
        }
        private void AbrirModuloRecepcion()
        {
            UC_RecepcionMercancia vistaRecepcion = new UC_RecepcionMercancia();
            CambiarVista(vistaRecepcion, mostrarSidebar: true, botonActivo: _btnNavRecepcion);
        }
        private void AbrirModuloInventario()
        {
            UC_Inventario vistaInventario = new UC_Inventario();
            CambiarVista(vistaInventario, mostrarSidebar: true, botonActivo: _btnNavInventario);
        }
        private void AbrirModuloMermas()
        {
            UC_RegistroMerma vistaMerma = new UC_RegistroMerma();
            CambiarVista(vistaMerma, mostrarSidebar: true, botonActivo: _btnNavMermas);
        }
    }
}