using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CapaLogica;
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

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblFechaTurno = null!;

        // Contenedor principal fluido
        private Panel _pnlContenido = null!;

        // Tarjetas KPI superiores
        private PanelRedondeado _cardVentas = null!;
        private PanelRedondeado _cardFinanciero = null!;
        private PanelRedondeado _cardAlertas = null!;
        private Label _lblVentasVal = null!;
        private Label _lblVentasSub = null!;
        private Label _lblFinancieroVal = null!;
        private Label _lblFinancieroSub = null!;
        private Label _lblAlertasVal = null!;
        private Label _lblAlertasSub = null!;

        // Tarjetas de los 6 módulos administrativos
        private readonly List<PanelRedondeado> _tarjetasMenu = new();
        private Button _btnCerrarSesion = null!;

        public UC_MenuAdmin()
        {
            ConfigurarInterfaz();

            Load += (s, e) =>
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
                    return;

                CargarIndicadores();
            };
        }

        /// <summary>Indicadores reales del día y del inventario (ReporteNegocio).</summary>
        private void CargarIndicadores()
        {
            var cultura = CultureInfo.GetCultureInfo("es-MX");
            var resultado = new ReporteNegocio().ObtenerResumenDashboard();

            if (!resultado.Exito || resultado.Resumen == null)
            {
                foreach (var lbl in new[] { _lblVentasVal, _lblFinancieroVal, _lblAlertasVal })
                    lbl.Text = "—";
                _lblVentasSub.Text = _lblFinancieroSub.Text = _lblAlertasSub.Text = "No se pudo consultar MySQL";
                AjustarLayoutResponsivo();
                return;
            }

            var r = resultado.Resumen;

            _lblVentasVal.Text = r.VentasHoy.ToString("C2", cultura);
            _lblVentasSub.Text = $"{r.TicketsHoy} {(r.TicketsHoy == 1 ? "venta confirmada" : "ventas confirmadas")} hoy";

            if (r.TicketsHoy == 0 && r.UtilidadBrutaHoy == 0)
            {
                _lblFinancieroVal.Text = "Sin movimientos";
                _lblFinancieroVal.ForeColor = TemaCafeteria.TextoSecundario;
            }
            else
            {
                bool positivo = r.UtilidadBrutaHoy >= 0;
                _lblFinancieroVal.Text = positivo ? "Balance Positivo" : "Balance Negativo";
                _lblFinancieroVal.ForeColor = positivo ? ColorTranslator.FromHtml("#0D47A1") : ColorTranslator.FromHtml("#C62828");
            }
            _lblFinancieroSub.Text = $"Utilidad bruta hoy: {r.UtilidadBrutaHoy.ToString("C2", cultura)}";

            _lblAlertasVal.Text = $"{r.AlertasInventario} {(r.AlertasInventario == 1 ? "insumo" : "insumos")}";
            _lblAlertasSub.Text = $"{r.InsumosBlanco} agotados · {r.InsumosRojo} en rojo · {r.InsumosAmarillo} en amarillo"
                                  + (r.MermasPendientes > 0 ? $" · {r.MermasPendientes} mermas por autorizar" : string.Empty);

            AjustarLayoutResponsivo();
        }

        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra blanca superior institucional
            _pnlTopBar = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Top,
                Height = 86
            };
            _pnlTopBar.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, _pnlTopBar.Height - 1, _pnlTopBar.Width, _pnlTopBar.Height - 1);
            };

            _lblRuta = new Label
            {
                Text = "ADMINISTRACIÓN / PANEL DE CONTROL",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(40, 16),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Centro de Control del Administrador",
                Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(38, 35),
                AutoSize = true
            };

            _lblFechaTurno = new Label
            {
                Text = $"{DateTime.Now:dddd, dd 'de' MMMM 'de' yyyy}\n● Sistema operativo activo",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblFechaTurno });

            // 2. Contenedor fluido principal
            _pnlContenido = new Panel
            {
                BackColor = Color.Transparent
            };

            // 3. Tarjetas KPI Superiores
            _cardVentas = CrearTarjetaKpi(
                "VENTAS DEL DÍA",
                "—",
                "Ventas netas (excluye canceladas)",
                ColorTranslator.FromHtml("#1F4E35"),
                ColorTranslator.FromHtml("#EAF3EE"),
                out _lblVentasVal, out _lblVentasSub);

            _cardFinanciero = CrearTarjetaKpi(
                "ESTADO FINANCIERO",
                "—",
                "Utilidad bruta del día",
                ColorTranslator.FromHtml("#0D47A1"),
                ColorTranslator.FromHtml("#E3F2FD"),
                out _lblFinancieroVal, out _lblFinancieroSub);

            _cardAlertas = CrearTarjetaKpi(
                "ALERTAS DE INVENTARIO",
                "—",
                "Insumos que requieren reabastecimiento",
                ColorTranslator.FromHtml("#C62828"),
                ColorTranslator.FromHtml("#FFEBEE"),
                out _lblAlertasVal, out _lblAlertasSub);

            _pnlContenido.Controls.AddRange(new Control[] { _cardVentas, _cardFinanciero, _cardAlertas });

            // 4. Cuadrícula de 6 módulos (2 columnas x 3 filas amplias)
            var modulos = new (string num, string titulo, string desc, Action accion)[]
            {
                ("01", "Inventario", "Consulta existencias PEPS, lotes activos y semáforo de stock", () => IrAInventario?.Invoke(this, EventArgs.Empty)),
                ("02", "Catálogo y Recetas", "Administra productos vendibles, precios e ingredientes por receta", () => IrACatalogo?.Invoke(this, EventArgs.Empty)),
                ("03", "Configuración Global", "Parámetros de caja, umbrales de alerta, usuarios y proveedores", () => IrAConfiguracion?.Invoke(this, EventArgs.Empty)),
                ("04", "Autorizaciones Pendientes", "Revisa y aprueba mermas mayores al umbral operativo", () => IrAAutorizaciones?.Invoke(this, EventArgs.Empty)),
                ("05", "Historial", "Bitácora completa de ventas, recepciones y movimientos de lote", () => IrAHistorial?.Invoke(this, EventArgs.Empty)),
                ("06", "Reporte Financiero", "Cortes de caja, utilidad bruta, costos de insumos y balance", () => IrAReporteFinanciero?.Invoke(this, EventArgs.Empty))
            };

            _tarjetasMenu.Clear();
            foreach (var (num, titulo, desc, accion) in modulos)
            {
                var cardModulo = CrearTarjetaModulo(num, titulo, desc, accion);
                _tarjetasMenu.Add(cardModulo);
                _pnlContenido.Controls.Add(cardModulo);
            }

            // 5. Botón inferior Cerrar Sesión
            _btnCerrarSesion = new Button
            {
                Text = "Cerrar Sesión",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#C62828"),
                FlatStyle = FlatStyle.Flat,
                Height = 52,
                Cursor = Cursors.Hand
            };
            _btnCerrarSesion.FlatAppearance.BorderSize = 0;
            _btnCerrarSesion.Click += (s, e) => SalirSesion?.Invoke(this, EventArgs.Empty);

            _pnlContenido.Controls.Add(_btnCerrarSesion);

            Controls.AddRange(new Control[] { _pnlContenido, _pnlTopBar });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private PanelRedondeado CrearTarjetaKpi(string etiqueta, string valor, string subtexto, Color colorAcento, Color colorBadge,
            out Label lblValor, out Label lblSubtexto)
        {
            PanelRedondeado card = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label lblBadge = new Label
            {
                Text = $"●  {etiqueta}",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = colorAcento,
                BackColor = colorBadge,
                Location = new Point(22, 18),
                Padding = new Padding(8, 3, 8, 3),
                AutoSize = true
            };

            Label lblVal = new Label
            {
                Text = valor,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = colorAcento,
                Location = new Point(20, 50),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = subtexto,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(22, 90),
                AutoSize = true
            };

            card.Controls.AddRange(new Control[] { lblBadge, lblVal, lblSub });
            lblValor = lblVal;
            lblSubtexto = lblSub;
            return card;
        }

        private PanelRedondeado CrearTarjetaModulo(string numero, string titulo, string descripcion, Action onClick)
        {
            PanelRedondeado card = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };

            Label lblNum = new Label
            {
                Text = numero,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#EFE6DC"),
                Size = new Size(44, 44),
                Location = new Point(22, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };

            Label lblTit = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(82, 20),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            Label lblDesc = new Label
            {
                Name = "lblDesc",
                Text = descripcion,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(82, 48),
                Height = 36,
                Cursor = Cursors.Hand
            };

            Label lblFlecha = new Label
            {
                Name = "lblFlecha",
                Text = "→",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            // Efecto visual al pasar el cursor y clic en cualquier parte de la tarjeta
            Color colorNormal = Color.White;
            Color colorHover = ColorTranslator.FromHtml("#FAF5EF");

            void ActivarHover(object? s, EventArgs e) => card.BackColor = colorHover;
            void DesactivarHover(object? s, EventArgs e) => card.BackColor = colorNormal;
            void EjecutarClic(object? s, EventArgs e) => onClick();

            card.MouseEnter += ActivarHover;
            card.MouseLeave += DesactivarHover;
            card.Click += EjecutarClic;

            foreach (Control hijo in new Control[] { lblNum, lblTit, lblDesc, lblFlecha })
            {
                hijo.MouseEnter += ActivarHover;
                hijo.MouseLeave += DesactivarHover;
                hijo.Click += EjecutarClic;
            }

            card.Controls.AddRange(new Control[] { lblNum, lblTit, lblDesc, lblFlecha });
            return card;
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            _lblFechaTurno.Location = new Point(_pnlTopBar.Width - _lblFechaTurno.Width - 40, 22);

            int margenX = Math.Max(36, (int)(ClientSize.Width * 0.03));
            int topContenido = _pnlTopBar.Height + 22;
            int altoDisponible = Math.Max(560, ClientSize.Height - topContenido - 24);
            int anchoUtil = Math.Max(900, ClientSize.Width - (margenX * 2));

            _pnlContenido.Bounds = new Rectangle(margenX, topContenido, anchoUtil, altoDisponible);

            // 1. Fila superior de 3 tarjetas KPI (más altas y ocupando todo el ancho útil)
            int altoKpi = Math.Clamp((int)(altoDisponible * 0.21), 124, 165);
            int gapKpi = 22;
            int anchoKpi = (anchoUtil - (gapKpi * 2)) / 3;

            _cardVentas.Bounds = new Rectangle(0, 0, anchoKpi, altoKpi);
            _cardFinanciero.Bounds = new Rectangle(anchoKpi + gapKpi, 0, anchoKpi, altoKpi);
            _cardAlertas.Bounds = new Rectangle((anchoKpi + gapKpi) * 2, 0, anchoUtil - ((anchoKpi + gapKpi) * 2), altoKpi);

            // 2. Botón Cerrar Sesión anclado abajo
            int altoBotonSalir = 56;
            int yCerrar = altoDisponible - altoBotonSalir;
            _btnCerrarSesion.Bounds = new Rectangle(0, yCerrar, anchoUtil, altoBotonSalir);

            // 3. Cuadrícula de 6 tarjetas de módulos (ocupa todo el espacio vertical intermedio)
            int yGridInicio = altoKpi + 22;
            int altoGridTotal = Math.Max(310, yCerrar - yGridInicio - 22);

            int gapX = 24;
            int gapY = 20;
            int anchoCardModulo = (anchoUtil - gapX) / 2;
            int altoCardModulo = Math.Max(96, (altoGridTotal - (gapY * 2)) / 3);

            for (int i = 0; i < _tarjetasMenu.Count; i++)
            {
                int fila = i / 2;
                int col = i % 2;
                int x = col * (anchoCardModulo + gapX);
                int y = yGridInicio + fila * (altoCardModulo + gapY);

                var card = _tarjetasMenu[i];
                card.Bounds = new Rectangle(x, y, anchoCardModulo, altoCardModulo);

                if (card.Controls["lblDesc"] is Label desc)
                    desc.Width = Math.Max(220, anchoCardModulo - 145);

                if (card.Controls["lblFlecha"] is Label flecha)
                    flecha.Location = new Point(anchoCardModulo - 48, (altoCardModulo - flecha.Height) / 2);
            }
        }
    }
    }
