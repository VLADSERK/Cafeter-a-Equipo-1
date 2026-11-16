using CapaLogica;
using CapaPresentacion.Estilos;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_HistorialVentas : UserControl
    {
        private class VentaHistorialMock
        {
            public string Folio { get; set; } = "";
            public string FechaHora { get; set; } = "";
            public decimal TotalNeto { get; set; }
            public string Estado { get; set; } = "Confirmada"; // Confirmada | Cancelada
            public string Cajero { get; set; } = "";
            public string MetodoPago { get; set; } = "";
            public List<string> DetalleProductos { get; set; } = new();
            public string MotivoCancelacion { get; set; } = "";
        }

        private const string NIP_AUTORIZACION_ADMIN = "1234";
        private readonly List<VentaHistorialMock> _ventas = new();
        private VentaHistorialMock? _ventaSeleccionada;
        private readonly CultureInfo _culturaMxn = CultureInfo.GetCultureInfo("es-MX");

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;

        // Columna izquierda: Buscador + Resultados de ventas
        private PanelRedondeado _cardBusqueda = null!;
        private TextBox _txtBuscarFolio = null!;
        private Button _btnBuscar = null!;

        private PanelRedondeado _cardResultados = null!;
        private Label _lblTitResultados = null!;
        private DataGridView _dgvVentas = null!;

        // Columna derecha: Cancelar Venta / Detalle de la orden
        private PanelRedondeado _cardCancelarVenta = null!;
        private Label _lblPanelDerTitulo = null!;
        private Label _lblFolioBadge = null!;
        private Panel _pnlDetalleOrden = null!;
        private Label _lblDetalleLista = null!;
        private Label _lblTotalNetoVal = null!;
        private TextBox _txtMotivoCancelacion = null!;
        private CheckBox _chkRestaurarInsumos = null!;
        private Label _lblNotaRestaurar = null!;
        private Button _btnCerrarPanel = null!;
        private Button _btnConfirmarCancelacion = null!;

        public UC_HistorialVentas()
        {
           
            CargarVentasIniciales();
            ConfigurarInterfaz();
            FiltrarVentas();

            if (_ventas.Count > 0)
            {
                SeleccionarVenta(_ventas[0]);
            }
        }

        private void CargarVentasIniciales()
        {
            _ventas.AddRange(new[]
            {
                new VentaHistorialMock
                {
                    Folio = "FOLIO-8492",
                    FechaHora = "15 Nov, 10:30",
                    TotalNeto = 148.00m,
                    Estado = "Confirmada",
                    Cajero = "Sofía Ramírez (Caja 01)",
                    MetodoPago = "Tarjeta Débito",
                    DetalleProductos = new List<string>
                    {
                        "1x Capuchino grande  —  $58.00",
                        "2x Croissant de mantequilla  —  $90.00"
                    }
                },
                new VentaHistorialMock
                {
                    Folio = "FOLIO-8491",
                    FechaHora = "15 Nov, 09:15",
                    TotalNeto = 92.00m,
                    Estado = "Confirmada",
                    Cajero = "Carlos Méndez (Caja 01)",
                    MetodoPago = "Efectivo",
                    DetalleProductos = new List<string>
                    {
                        "1x Latte  —  $60.00",
                        "1x Galleta de chocolate  —  $32.00"
                    }
                },
                new VentaHistorialMock
                {
                    Folio = "FOLIO-8490",
                    FechaHora = "14 Nov, 19:40",
                    TotalNeto = 132.00m,
                    Estado = "Confirmada",
                    Cajero = "Sofía Ramírez (Caja 01)",
                    MetodoPago = "Tarjeta Crédito",
                    DetalleProductos = new List<string>
                    {
                        "1x Frappé Moka  —  $74.00",
                        "1x Cappuccino  —  $58.00"
                    }
                },
                new VentaHistorialMock
                {
                    Folio = "FOLIO-8489",
                    FechaHora = "14 Nov, 19:05",
                    TotalNeto = 100.00m,
                    Estado = "Confirmada",
                    Cajero = "Diego Torres (Caja 02)",
                    MetodoPago = "Efectivo",
                    DetalleProductos = new List<string>
                    {
                        "1x Cheesecake de Fresa  —  $65.00",
                        "1x Dona Glaseada  —  $35.00"
                    }
                },
                new VentaHistorialMock
                {
                    Folio = "FOLIO-8488",
                    FechaHora = "14 Nov, 18:45",
                    TotalNeto = 210.00m,
                    Estado = "Cancelada",
                    Cajero = "Carlos Méndez (Caja 01)",
                    MetodoPago = "Efectivo",
                    MotivoCancelacion = "Error de captura en orden duplicada antes de preparación.",
                    DetalleProductos = new List<string>
                    {
                        "2x Moka Caliente  —  $136.00",
                        "1x Frappé Moka  —  $74.00"
                    }
                }
            });
        }

        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra superior
            _pnlTopBar = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Top,
                Height = 92
            };
            _pnlTopBar.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, _pnlTopBar.Height - 1, _pnlTopBar.Width, _pnlTopBar.Height - 1);
            };

            _lblRuta = new Label
            {
                Text = "ADMINISTRACIÓN / HISTORIAL Y AUDITORÍA DE VENTAS",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(32, 14),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Historial y Cancelación de Ventas",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(30, 32),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Busca ventas confirmadas por folio único, ejecuta cancelaciones formales y gestiona el reintegro de ingredientes.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(32, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });

            ConstruirColumnaIzquierda();
            ConstruirColumnaDerecha();

            Controls.AddRange(new Control[] { _cardBusqueda, _cardResultados, _cardCancelarVenta, _pnlTopBar });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private void ConstruirColumnaIzquierda()
        {
            // Tarjeta superior izquierda: Buscar venta por folio único
            _cardBusqueda = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White
            };

            Label lblBuscarTit = new Label
            {
                Text = "Buscar venta por folio único",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(20, 14),
                AutoSize = true
            };

            _txtBuscarFolio = new TextBox
            {
                Font = new Font("Segoe UI", 10.5F),
                PlaceholderText = "Ej. FOLIO-8492 (deja vacío para ver todas)",
                Location = new Point(20, 42),
                Height = 36
            };
            _txtBuscarFolio.TextChanged += (s, e) => FiltrarVentas();

            _btnBuscar = new Button
            {
                Text = "Buscar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#5C5650"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(105, 34),
                Cursor = Cursors.Hand
            };
            _btnBuscar.FlatAppearance.BorderSize = 0;
            _btnBuscar.Click += (s, e) => FiltrarVentas();

            _cardBusqueda.Controls.AddRange(new Control[] { lblBuscarTit, _txtBuscarFolio, _btnBuscar });

            // Tarjeta inferior izquierda: Resultados de ventas
            _cardResultados = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            _lblTitResultados = new Label
            {
                Text = "Resultados de ventas (5)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(20, 16),
                AutoSize = true
            };

            _dgvVentas = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 46 },
                EnableHeadersVisualStyles = false,
                GridColor = TemaCafeteria.BordeSuave,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _dgvVentas.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoSecundario,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Padding = new Padding(8, 0, 8, 0)
            };

            _dgvVentas.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9F),
                SelectionBackColor = ColorTranslator.FromHtml("#EFE6DC"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(8, 0, 8, 0)
            };

            _dgvVentas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFolio", HeaderText = "Folio", FillWeight = 22 });
            _dgvVentas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFecha", HeaderText = "Fecha / Hora", FillWeight = 22 });
            _dgvVentas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Total", FillWeight = 17 });
            _dgvVentas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEstado", HeaderText = "Estado", FillWeight = 19 });
            _dgvVentas.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colAccion",
                HeaderText = "Acción",
                FlatStyle = FlatStyle.Flat,
                FillWeight = 20
            });

            _dgvVentas.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                if (e.ColumnIndex == _dgvVentas.Columns["colFolio"]!.Index ||
                    e.ColumnIndex == _dgvVentas.Columns["colTotal"]!.Index)
                {
                    e.CellStyle!.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }

                if (e.ColumnIndex == _dgvVentas.Columns["colEstado"]!.Index)
                {
                    string estado = e.Value?.ToString() ?? "";
                    bool confirmada = estado == "Confirmada";
                    e.CellStyle!.ForeColor = confirmada ? ColorTranslator.FromHtml("#1F4E35") : TemaCafeteria.TextoSecundario;
                    e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }

                if (e.ColumnIndex == _dgvVentas.Columns["colAccion"]!.Index)
                {
                    string accion = e.Value?.ToString() ?? "";
                    if (accion == "Cancelar")
                    {
                        e.CellStyle!.ForeColor = ColorTranslator.FromHtml("#C62828");
                        e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    }
                    else
                    {
                        e.CellStyle!.ForeColor = TemaCafeteria.TextoSecundario;
                        e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
                    }
                }
            };

            _dgvVentas.SelectionChanged += (s, e) =>
            {
                if (_dgvVentas.SelectedRows.Count > 0)
                {
                    string folio = _dgvVentas.SelectedRows[0].Cells["colFolio"].Value?.ToString() ?? "";
                    var venta = _ventas.FirstOrDefault(v => v.Folio == folio);
                    if (venta != null) SeleccionarVenta(venta);
                }
            };

            _dgvVentas.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvVentas.Columns["colAccion"]!.Index)
                {
                    string folio = _dgvVentas.Rows[e.RowIndex].Cells["colFolio"].Value?.ToString() ?? "";
                    var venta = _ventas.FirstOrDefault(v => v.Folio == folio);
                    if (venta != null)
                    {
                        SeleccionarVenta(venta);
                        if (venta.Estado == "Confirmada")
                        {
                            _txtMotivoCancelacion.Focus();
                        }
                    }
                }
            };

            _cardResultados.Controls.AddRange(new Control[] { _lblTitResultados, _dgvVentas });
        }

        private void ConstruirColumnaDerecha()
        {
            _cardCancelarVenta = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            _lblPanelDerTitulo = new Label
            {
                Text = "Cancelar Venta",
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 20),
                AutoSize = true
            };

            _lblFolioBadge = new Label
            {
                Text = "FOLIO-8492",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#EFE6DC"),
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true
            };

            // Caja de Detalle de la orden
            _pnlDetalleOrden = new Panel
            {
                Location = new Point(24, 62),
                Size = new Size(400, 148),
                BackColor = ColorTranslator.FromHtml("#FAF8F5"),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblDetTit = new Label
            {
                Text = "Detalle de la orden:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(14, 10),
                AutoSize = true
            };

            _lblDetalleLista = new Label
            {
                Text = "1x Capuchino grande — $58.00",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(14, 34),
                Size = new Size(370, 68)
            };

            Panel sepOrden = new Panel
            {
                Name = "sepOrden",
                Location = new Point(14, 106),
                Height = 1,
                Width = 370,
                BackColor = TemaCafeteria.BordeSuave
            };

            _lblTotalNetoVal = new Label
            {
                Text = "Total neto: $148.00",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(14, 114),
                AutoSize = true
            };

            _pnlDetalleOrden.Controls.AddRange(new Control[] { lblDetTit, _lblDetalleLista, sepOrden, _lblTotalNetoVal });

            // Campo Motivo de cancelación *
            Label lblMotivoTit = new Label
            {
                Text = "Motivo de cancelación *",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 224),
                AutoSize = true
            };

            _txtMotivoCancelacion = new TextBox
            {
                Multiline = true,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(24, 248),
                Height = 84,
                PlaceholderText = "Indique el motivo obligatorio de la cancelación (ej. Error en cobro, producto devuelto por cliente)..."
            };

            Label lblAuditoriaNota = new Label
            {
                Text = "* Este campo es obligatorio para auditoría interna.",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 336),
                AutoSize = true
            };

            // Casilla Restaurar ingredientes al inventario
            _chkRestaurarInsumos = new CheckBox
            {
                Text = "Restaurar ingredientes al inventario",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 368),
                AutoSize = true,
                Checked = true
            };

            _lblNotaRestaurar = new Label
            {
                Text = "Los insumos recuperables (café, leche, panadería) serán devueltos automáticamente al stock activo.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(44, 392),
                Size = new Size(380, 36)
            };

            // Botones inferiores: Cerrar | Confirmar Cancelación
            _btnCerrarPanel = new Button
            {
                Text = "Cerrar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(135, 42),
                Cursor = Cursors.Hand
            };
            _btnCerrarPanel.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
            _btnCerrarPanel.FlatAppearance.BorderSize = 1;
            _btnCerrarPanel.Click += (s, e) =>
            {
                _txtMotivoCancelacion.Clear();
                _txtBuscarFolio.Clear();
            };

            _btnConfirmarCancelacion = new Button
            {
                Text = "Confirmar Cancelación",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#9E1B1B"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(215, 42),
                Cursor = Cursors.Hand
            };
            _btnConfirmarCancelacion.FlatAppearance.BorderSize = 0;
            _btnConfirmarCancelacion.Click += BtnConfirmarCancelacion_Click;

            _cardCancelarVenta.Controls.AddRange(new Control[]
            {
                _lblPanelDerTitulo, _lblFolioBadge,
                _pnlDetalleOrden,
                lblMotivoTit, _txtMotivoCancelacion, lblAuditoriaNota,
                _chkRestaurarInsumos, _lblNotaRestaurar,
                _btnCerrarPanel, _btnConfirmarCancelacion
            });
        }

        private void FiltrarVentas()
        {
            string filtro = _txtBuscarFolio.Text.Trim().ToLowerInvariant();
            _dgvVentas.Rows.Clear();
            int conteo = 0;

            foreach (var v in _ventas)
            {
                if (!string.IsNullOrEmpty(filtro) && !v.Folio.ToLowerInvariant().Contains(filtro))
                    continue;

                string textoAccion = v.Estado == "Confirmada" ? "Cancelar" : "Sin acciones";
                _dgvVentas.Rows.Add(
                    v.Folio,
                    v.FechaHora,
                    v.TotalNeto.ToString("C2", _culturaMxn),
                    v.Estado,
                    textoAccion
                );
                conteo++;
            }

            _lblTitResultados.Text = $"Resultados de ventas ({conteo})";
        }

        private void SeleccionarVenta(VentaHistorialMock venta)
        {
            _ventaSeleccionada = venta;
            _lblFolioBadge.Text = venta.Folio;
            _lblDetalleLista.Text = string.Join("\n", venta.DetalleProductos) + $"\nCajero: {venta.Cajero} ({venta.MetodoPago})";
            _lblTotalNetoVal.Text = $"Total neto: {venta.TotalNeto.ToString("C2", _culturaMxn)}";

            bool esConfirmada = venta.Estado == "Confirmada";
            _lblPanelDerTitulo.Text = esConfirmada ? "Cancelar Venta" : "Venta Cancelada (Solo lectura)";
            _txtMotivoCancelacion.ReadOnly = !esConfirmada;
            _txtMotivoCancelacion.Text = esConfirmada ? "" : venta.MotivoCancelacion;
            _chkRestaurarInsumos.Enabled = esConfirmada;
            _btnConfirmarCancelacion.Enabled = esConfirmada;
            _btnConfirmarCancelacion.BackColor = esConfirmada
                ? ColorTranslator.FromHtml("#9E1B1B")
                : ColorTranslator.FromHtml("#BDBDBD");
        }

        private void BtnConfirmarCancelacion_Click(object? sender, EventArgs e)
        {
            if (_ventaSeleccionada == null || _ventaSeleccionada.Estado != "Confirmada")
                return;

            if (string.IsNullOrWhiteSpace(_txtMotivoCancelacion.Text))
            {
                MessageBox.Show(
                    "El motivo de cancelación es obligatorio para auditoría interna.",
                    "Campo obligatorio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _txtMotivoCancelacion.Focus();
                return;
            }

            // Solicitar NIP de Administrador antes de anular el ticket
            if (!SolicitarNipAdministrador($"Cancelar ticket {_ventaSeleccionada.Folio}"))
                return;

            _ventaSeleccionada.Estado = "Cancelada";
            _ventaSeleccionada.MotivoCancelacion = _txtMotivoCancelacion.Text.Trim();

            string msgInventario = _chkRestaurarInsumos.Checked
                ? "Los insumos recuperables han sido reintegrados al inventario PEPS."
                : "No se reintegraron insumos al stock.";

            FiltrarVentas();
            SeleccionarVenta(_ventaSeleccionada);

            MessageBox.Show(
                $"La venta {_ventaSeleccionada.Folio} ({_ventaSeleccionada.TotalNeto.ToString("C2", _culturaMxn)}) ha sido CANCELADA.\n\n{msgInventario}",
                "Cancelación Confirmada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private bool SolicitarNipAdministrador(string accionConfirmar)
        {
            bool autorizado = false;

            using Form dlgNip = new Form
            {
                Text = "Confirmación de Seguridad",
                Size = new Size(390, 265),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblIcono = new Label
            {
                Text = "🔒  Validación de NIP",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblInstruccion = new Label
            {
                Text = $"Ingresa tu NIP de Administrador para confirmar:\n{accionConfirmar} ",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 48),
                Size = new Size(330, 38)
            };

            TextBox txtNip = new TextBox
            {
                Location = new Point(95, 96),
                Size = new Size(180, 42),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                PasswordChar = '●',
                MaxLength = 4
            };
            txtNip.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };

            Button btnCancelar = new Button
            {
                Text = "Cancelar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = ColorTranslator.FromHtml("#EFECE8"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 162),
                Size = new Size(150, 40),
                Cursor = Cursors.Hand
            };
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Click += (s, e) => dlgNip.Close();

            Button btnConfirmar = new Button
            {
                Text = "Confirmar NIP",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(190, 162),
                Size = new Size(162, 40),
                Cursor = Cursors.Hand
            };
            btnConfirmar.FlatAppearance.BorderSize = 0;
            btnConfirmar.Click += (s, e) =>
            {
                var auth = new AuthNegocio();
                var validacion = auth.ValidarNipAdministrador(txtNip.Text.Trim());
                if (!validacion.Autorizado)
                {
                    MessageBox.Show("NIP incorrecto o sin privilegios de Administrador.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtNip.Clear();
                    txtNip.Focus();
                    return;
                }

                autorizado = true;
                dlgNip.Close();
            };

            dlgNip.AcceptButton = btnConfirmar;
            dlgNip.Shown += (s, e) => txtNip.Focus();
            dlgNip.Controls.AddRange(new Control[] { lblIcono, lblInstruccion, txtNip, btnCancelar, btnConfirmar });
            dlgNip.ShowDialog(this);

            return autorizado;
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 28;
            int topContenido = _pnlTopBar.Height + 18;
            int altoDisponible = Math.Max(480, ClientSize.Height - topContenido - 22);
            int anchoUtil = Math.Max(860, ClientSize.Width - (margenX * 2));
            int gap = 18;

            int anchoIzq = Math.Clamp((int)(anchoUtil * 0.54), 460, 680);
            int anchoDer = anchoUtil - anchoIzq - gap;

            // Columna izquierda: Buscador arriba + Tabla resultados abajo
            int altoBusqueda = 96;
            _cardBusqueda.Bounds = new Rectangle(margenX, topContenido, anchoIzq, altoBusqueda);
            _btnBuscar.Location = new Point(anchoIzq - _btnBuscar.Width - 20, 40);
            _txtBuscarFolio.Width = _btnBuscar.Left - 32;

            int yResultados = topContenido + altoBusqueda + 14;
            int altoResultados = altoDisponible - altoBusqueda - 14;
            _cardResultados.Bounds = new Rectangle(margenX, yResultados, anchoIzq, altoResultados);
            _dgvVentas.Bounds = new Rectangle(14, 48, anchoIzq - 28, altoResultados - 62);

            // Columna derecha: Panel Cancelar Venta
            // Columna derecha: Panel Cancelar Venta
            _cardCancelarVenta.Bounds = new Rectangle(margenX + anchoIzq + gap, topContenido, anchoDer, altoDisponible);
            _lblFolioBadge.Location = new Point(anchoDer - _lblFolioBadge.Width - 24, 22);

            int anchoInternoDer = anchoDer - 48;
            _pnlDetalleOrden.Width = anchoInternoDer;
            _lblDetalleLista.Width = anchoInternoDer - 28;
            if (_pnlDetalleOrden.Controls["sepOrden"] is Panel sep)
                sep.Width = anchoInternoDer - 28;

            // Posicionar botones inferiores
            int yBotones = altoDisponible - 62;
            _btnConfirmarCancelacion.Location = new Point(anchoDer - _btnConfirmarCancelacion.Width - 24, yBotones);
            _btnCerrarPanel.Location = new Point(_btnConfirmarCancelacion.Left - _btnCerrarPanel.Width - 12, yBotones);

            // Anclar la casilla "Restaurar ingredientes" justo arriba de los botones
            int yCheck = yBotones - 72;
            _chkRestaurarInsumos.Location = new Point(24, yCheck);
            _lblNotaRestaurar.Location = new Point(44, yCheck + 24);
            _lblNotaRestaurar.Width = anchoInternoDer - 20;

            // Expandir el cuadro de texto Motivo de cancelación para llenar todo el espacio intermedio
            int yTxtMotivo = 248;
            int altoTxtMotivo = Math.Max(90, yCheck - yTxtMotivo - 34);
            _txtMotivoCancelacion.Bounds = new Rectangle(24, yTxtMotivo, anchoInternoDer, altoTxtMotivo);

            // Reubicar la nota "* Este campo es obligatorio..." justo debajo del cuadro de texto
            foreach (Control c in _cardCancelarVenta.Controls)
            {
                if (c is Label lbl && lbl.Text.StartsWith("* Este campo"))
                {
                    lbl.Location = new Point(24, _txtMotivoCancelacion.Bottom + 6);
                }
            }
        }
    }
}
