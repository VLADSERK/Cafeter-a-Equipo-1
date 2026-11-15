using CapaPresentaciòn.Estilos;
using CapaPresentacion.Estilos;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_RecepcionMercancia : UserControl
    {
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;

        private Panel _pnlSubHeader = null!;
        private Label _lblSubtitulo = null!;
        private Label _lblDescripcion = null!;
        private PanelRedondeado _cardFechaOperativa = null!;
        private Label _lblFechaOperativaValor = null!;

        private PanelRedondeado _cardFormulario = null!;
        private ComboBox _cmbInsumo = null!;
        private ComboBox _cmbProveedor = null!;
        private TextBox _txtCantidad = null!;
        private Label _lblUnidadSufijo = null!;
        private ComboBox _cmbUnidad = null!;
        private TextBox _txtCostoUnitario = null!;
        private Label _lblCostoEstimadoValor = null!;
        private DateTimePicker _dtpRecepcion = null!;
        private DateTimePicker _dtpCaducidad = null!;

        private PanelRedondeado _cardLote = null!;
        private Label _lblCodigoLote = null!;
        private PictureBox _picCodigoBarras = null!;

        private PanelRedondeado _barraAcciones = null!;
        private Button _btnCancelar = null!;
        private Button _btnConfirmar = null!;

        public UC_RecepcionMercancia()
        {
            InitializeComponent();
            ConfigurarInterfazRecepcion();
            ActualizarVistaPreviaLote();
        }

        private void ConfigurarInterfazRecepcion()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra blanca superior limpia
            _pnlTopBar = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Top,
                Height = 82
            };
            _pnlTopBar.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, _pnlTopBar.Height - 1, _pnlTopBar.Width, _pnlTopBar.Height - 1);
            };

            _lblRuta = new Label
            {
                Text = "OPERACIONES / INVENTARIO",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(36, 16),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Recepción de Mercancía",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(34, 34),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo });

            // 2. Sub-encabezado y Fecha Operativa
            _pnlSubHeader = new Panel { BackColor = Color.Transparent };

            _lblSubtitulo = new Label
            {
                Text = "Registrar nueva recepción",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(0, 4),
                AutoSize = true
            };

            _lblDescripcion = new Label
            {
                Text = "Captura los datos del insumo recibido para actualizar las existencias bajo control PEPS.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(0, 32),
                AutoSize = true
            };

            _cardFechaOperativa = new PanelRedondeado
            {
                Size = new Size(255, 56),
                RadioBorde = 12,
                BackColor = Color.White
            };

            Label lblTituloFechaOp = new Label
            {
                Text = "Fecha operativa",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(16, 8),
                AutoSize = true
            };

            _lblFechaOperativaValor = new Label
            {
                Text = DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy"),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(16, 26),
                AutoSize = true
            };

            _cardFechaOperativa.Controls.AddRange(new Control[] { lblTituloFechaOp, _lblFechaOperativaValor });
            _pnlSubHeader.Controls.AddRange(new Control[] { _lblSubtitulo, _lblDescripcion, _cardFechaOperativa });

            ConstruirTarjetaFormulario();
            ConstruirTarjetaLote();
            ConstruirBarraAcciones();

            Controls.AddRange(new Control[]
            {
                _pnlSubHeader,
                _cardFormulario,
                _cardLote,
                _barraAcciones,
                _pnlTopBar
            });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private void ConstruirTarjetaFormulario()
        {
            _cardFormulario = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label badge01 = new Label
            {
                Text = "01",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#EFE6DC"),
                Size = new Size(34, 34),
                Location = new Point(26, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblCardTitulo = new Label
            {
                Text = "Información de la recepción",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(72, 18),
                AutoSize = true
            };

            Label lblCardSub = new Label
            {
                Text = "Los campos marcados con * son obligatorios.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(72, 40),
                AutoSize = true
            };

            // Fila 1: Insumo * | Proveedor *
            Label lblInsumo = CrearEtiquetaCampo("Insumo *", 26, 78);
            _cmbInsumo = new ComboBox
            {
                Name = "cmbInsumo",
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(26, 102),
                Height = 36
            };

            _cmbInsumo.Items.Add("Seleccionar insumo...");
            foreach (var insumo in CatalogoInsumosMock.ListaIngredientes)
            {
                _cmbInsumo.Items.Add(insumo.Nombre);
            }
            _cmbInsumo.SelectedIndex = 0;

            // Al elegir un ingrediente, selecciona automáticamente su unidad de medida correcta
            _cmbInsumo.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbInsumo.SelectedIndex <= 0) return;
                string nombreSel = _cmbInsumo.SelectedItem?.ToString() ?? "";
                var insumo = CatalogoInsumosMock.ListaIngredientes.Find(i => i.Nombre == nombreSel);
                if (insumo != null)
                {
                    for (int idx = 0; idx < _cmbUnidad.Items.Count; idx++)
                    {
                        if (_cmbUnidad.Items[idx]?.ToString()?.Contains($"({insumo.UnidadDefault})") == true)
                        {
                            _cmbUnidad.SelectedIndex = idx;
                            break;
                        }
                    }
                }
            };

            Label lblProveedor = CrearEtiquetaCampo("Proveedor *", 320, 78);
            _cmbProveedor = new ComboBox
            {
                Name = "cmbProveedor",
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 102),
                Height = 36
            };
            _cmbProveedor.Items.AddRange(new object[]
            {
                "Seleccionar proveedor...",
                "Tostadores del Sur S.A.",
                "Lácteos La Pradera",
                "Insumos Barista México",
                "Distribuidora Dulce Aroma"
            });
            _cmbProveedor.SelectedIndex = 0;

            // Fila 2: Cantidad inicial * | Unidad de medida
            Label lblCantidad = CrearEtiquetaCampo("Cantidad inicial *", 26, 158);
            Panel pnlCantidad = new Panel
            {
                Name = "pnlCantidad",
                Location = new Point(26, 182),
                Height = 42,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _txtCantidad = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                PlaceholderText = "0.00",
                Location = new Point(12, 10),
                Height = 24
            };
            _txtCantidad.TextChanged += (s, e) => CalcularCostoEstimado();

            _lblUnidadSufijo = new Label
            {
                Text = "kg",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };
            pnlCantidad.Controls.AddRange(new Control[] { _txtCantidad, _lblUnidadSufijo });

            Label lblUnidad = CrearEtiquetaCampo("Unidad de medida", 320, 158);
            _cmbUnidad = new ComboBox
            {
                Name = "cmbUnidad",
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 182),
                Height = 36
            };
            _cmbUnidad.Items.AddRange(new object[]
            {
                "Kilogramos (kg)",
                "Gramos (g)",
                "Litros (L)",
                "Mililitros (ml)",
                "Piezas (pz)"
            });
            _cmbUnidad.SelectedIndex = 0;
            _cmbUnidad.SelectedIndexChanged += (s, e) =>
            {
                string sel = _cmbUnidad.SelectedItem?.ToString() ?? "";
                if (sel.Contains("(kg)")) _lblUnidadSufijo.Text = "kg";
                else if (sel.Contains("(g)")) _lblUnidadSufijo.Text = "g";
                else if (sel.Contains("(L)")) _lblUnidadSufijo.Text = "L";
                else if (sel.Contains("(ml)")) _lblUnidadSufijo.Text = "ml";
                else _lblUnidadSufijo.Text = "pz";
            };

            // Fila 3: Costo unitario * | Costo estimado
            Label lblCostoUnit = CrearEtiquetaCampo("Costo unitario *", 26, 242);
            Panel pnlCostoUnit = new Panel
            {
                Name = "pnlCostoUnit",
                Location = new Point(26, 266),
                Height = 42,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblSigno1 = new Label
            {
                Text = "$",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(10, 10),
                AutoSize = true
            };

            _txtCostoUnitario = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                PlaceholderText = "0.00",
                Location = new Point(30, 10),
                Height = 24
            };
            _txtCostoUnitario.TextChanged += (s, e) => CalcularCostoEstimado();

            Label lblMxn1 = new Label
            {
                Name = "lblMxn1",
                Text = "MXN",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };
            pnlCostoUnit.Controls.AddRange(new Control[] { lblSigno1, _txtCostoUnitario, lblMxn1 });

            Label lblCostoEst = CrearEtiquetaCampo("Costo estimado", 320, 242);
            Panel pnlCostoEst = new Panel
            {
                Name = "pnlCostoEst",
                Location = new Point(320, 266),
                Height = 42,
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                BorderStyle = BorderStyle.FixedSingle
            };

            _lblCostoEstimadoValor = new Label
            {
                Text = "$ 0.00",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(12, 10),
                AutoSize = true
            };
            Label lblMxn2 = new Label
            {
                Name = "lblMxn2",
                Text = "MXN",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };
            pnlCostoEst.Controls.AddRange(new Control[] { _lblCostoEstimadoValor, lblMxn2 });

            // Fila 4: Fecha de recepción * | Fecha de caducidad *
            Label lblFechaRec = CrearEtiquetaCampo("Fecha de recepción *", 26, 326);
            _dtpRecepcion = new DateTimePicker
            {
                Name = "dtpRecepcion",
                Font = new Font("Segoe UI", 10.5F),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now,
                Location = new Point(26, 350),
                Height = 36
            };
            _dtpRecepcion.ValueChanged += (s, e) => ActualizarVistaPreviaLote();

            Label lblFechaCad = CrearEtiquetaCampo("Fecha de caducidad *", 320, 326);
            _dtpCaducidad = new DateTimePicker
            {
                Name = "dtpCaducidad",
                Font = new Font("Segoe UI", 10.5F),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddMonths(3),
                Location = new Point(320, 350),
                Height = 36
            };

            _cardFormulario.Controls.AddRange(new Control[]
            {
                badge01, lblCardTitulo, lblCardSub,
                lblInsumo, _cmbInsumo,
                lblProveedor, _cmbProveedor,
                lblCantidad, pnlCantidad,
                lblUnidad, _cmbUnidad,
                lblCostoUnit, pnlCostoUnit,
                lblCostoEst, pnlCostoEst,
                lblFechaRec, _dtpRecepcion,
                lblFechaCad, _dtpCaducidad
            });
        }

        private void ConstruirTarjetaLote()
        {
            _cardLote = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label badgeBarcode = new Label
            {
                Text = "|||",
                Font = new Font("Consolas", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#1F4E35"),
                BackColor = ColorTranslator.FromHtml("#E6F0EC"),
                Size = new Size(34, 34),
                Location = new Point(22, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblLoteTitulo = new Label
            {
                Text = "Lote automático",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(66, 18),
                AutoSize = true
            };

            Label lblLoteSub = new Label
            {
                Text = "Vista previa del identificador",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(66, 40),
                AutoSize = true
            };

            Panel pnlBarcodeBox = new Panel
            {
                Name = "pnlBarcodeBox",
                Location = new Point(22, 74),
                Size = new Size(310, 155),
                BackColor = ColorTranslator.FromHtml("#FAF8F5")
            };
            pnlBarcodeBox.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1.5f) { DashStyle = DashStyle.Dash };
                e.Graphics.DrawRectangle(pen, 1, 1, pnlBarcodeBox.Width - 3, pnlBarcodeBox.Height - 3);
            };

            Label lblIdTitulo = new Label
            {
                Text = "IDENTIFICADOR DE LOTE",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(10, 14),
                Size = new Size(290, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _lblCodigoLote = new Label
            {
                Text = "LOT-INS-20261115-001",
                Font = new Font("Consolas", 13.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(10, 34),
                Size = new Size(290, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _picCodigoBarras = new PictureBox
            {
                Location = new Point(45, 66),
                Size = new Size(220, 56),
                BackColor = Color.Transparent
            };
            _picCodigoBarras.Paint += DibujarCodigoBarrasSimulado;

            pnlBarcodeBox.Controls.AddRange(new Control[] { lblIdTitulo, _lblCodigoLote, _picCodigoBarras });

            PanelRedondeado pnlNota = new PanelRedondeado
            {
                Name = "pnlNota",
                Location = new Point(22, 246),
                Size = new Size(310, 88),
                RadioBorde = 10,
                BackColor = ColorTranslator.FromHtml("#EDF4F0")
            };

            Label lblNota = new Label
            {
                Text = "ℹ  El lote y su código de barras se generarán al confirmar la recepción. Podrás imprimir la etiqueta después del registro.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#264D3B"),
                Location = new Point(12, 10),
                Size = new Size(286, 68)
            };
            pnlNota.Controls.Add(lblNota);

            Label lblEstadoTit = new Label { Name = "lblEstadoTit", Text = "Estado inicial", Font = new Font("Segoe UI", 8.5F), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };
            Label lblEstadoVal = new Label { Name = "lblEstadoVal", Text = "● Disponible", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.SemaforoVerde, AutoSize = true };

            Label lblUbicTit = new Label { Name = "lblUbicTit", Text = "Ubicación", Font = new Font("Segoe UI", 8.5F), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };
            Label lblUbicVal = new Label { Name = "lblUbicVal", Text = "Almacén principal", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, AutoSize = true };

            _cardLote.Controls.AddRange(new Control[]
            {
                badgeBarcode, lblLoteTitulo, lblLoteSub,
                pnlBarcodeBox, pnlNota,
                lblEstadoTit, lblEstadoVal, lblUbicTit, lblUbicVal
            });
        }

        private void ConstruirBarraAcciones()
        {
            _barraAcciones = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = ColorTranslator.FromHtml("#2E1E16")
            };

            _btnCancelar = new Button
            {
                Text = "Cancelar / Limpiar",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(205, 42),
                Cursor = Cursors.Hand
            };
            _btnCancelar.FlatAppearance.BorderSize = 0;
            _btnCancelar.Click += (s, e) => LimpiarFormulario();

            _btnConfirmar = new Button
            {
                Text = "✓  Confirmar y Actualizar Existencias",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1F4E35"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(335, 42),
                Cursor = Cursors.Hand
            };
            _btnConfirmar.FlatAppearance.BorderSize = 0;
            _btnConfirmar.Click += BtnConfirmar_Click;

            _barraAcciones.Controls.AddRange(new Control[] { _btnCancelar, _btnConfirmar });
        }

        private Label CrearEtiquetaCampo(string texto, int x, int y)
        {
            return new Label
            {
                Text = texto,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private void CalcularCostoEstimado()
        {
            if (decimal.TryParse(_txtCantidad.Text, out decimal cantidad) &&
                decimal.TryParse(_txtCostoUnitario.Text, out decimal costoUnit))
            {
                decimal total = cantidad * costoUnit;
                _lblCostoEstimadoValor.Text = $"$ {total:N2}";
            }
            else
            {
                _lblCostoEstimadoValor.Text = "$ 0.00";
            }
        }

        private void ActualizarVistaPreviaLote()
        {
            string fechaCodigo = _dtpRecepcion.Value.ToString("yyyyMMdd");
            _lblCodigoLote.Text = $"LOT-INS-{fechaCodigo}-001";
            _picCodigoBarras.Invalidate();
        }

        private void DibujarCodigoBarrasSimulado(object? sender, PaintEventArgs e)
        {
            int[] patronAnchos = { 3, 1, 2, 4, 1, 2, 1, 3, 2, 1, 4, 1, 2, 3, 1, 2, 2, 1, 3, 1, 2, 4, 1, 2, 1, 3, 2, 1, 3, 2 };
            int x = 8;
            using var brocha = new SolidBrush(TemaCafeteria.TextoOscuro);

            for (int i = 0; i < patronAnchos.Length && x < _picCodigoBarras.Width - 10; i++)
            {
                int ancho = patronAnchos[i];
                if (i % 2 == 0)
                {
                    e.Graphics.FillRectangle(brocha, x, 2, ancho, _picCodigoBarras.Height - 6);
                }
                x += ancho + 2;
            }
        }

        private void LimpiarFormulario()
        {
            _cmbInsumo.SelectedIndex = 0;
            _cmbProveedor.SelectedIndex = 0;
            _txtCantidad.Clear();
            _cmbUnidad.SelectedIndex = 0;
            _txtCostoUnitario.Clear();
            _lblCostoEstimadoValor.Text = "$ 0.00";
            _dtpRecepcion.Value = DateTime.Now;
            _dtpCaducidad.Value = DateTime.Now.AddMonths(3);
            ActualizarVistaPreviaLote();
        }

        private void BtnConfirmar_Click(object? sender, EventArgs e)
        {
            if (_cmbInsumo.SelectedIndex <= 0 || _cmbProveedor.SelectedIndex <= 0)
            {
                MessageBox.Show("Selecciona un insumo y un proveedor válidos.", "Campos obligatorios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_txtCantidad.Text, out decimal cant) || cant <= 0 ||
                !decimal.TryParse(_txtCostoUnitario.Text, out decimal costo) || costo <= 0)
            {
                MessageBox.Show("Ingresa una cantidad inicial y un costo unitario mayores a cero.", "Validación de datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_dtpCaducidad.Value.Date <= _dtpRecepcion.Value.Date)
            {
                MessageBox.Show("La fecha de caducidad debe ser posterior a la fecha de recepción.", "Control PEPS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                $"Recepción registrada correctamente.\n\n" +
                $"• Lote generado: {_lblCodigoLote.Text}\n" +
                $"• Insumo: {_cmbInsumo.SelectedItem}\n" +
                $"• Cantidad: {cant} {_lblUnidadSufijo.Text}\n" +
                $"• Costo Total: {_lblCostoEstimadoValor.Text} MXN",
                "Existencias Actualizadas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LimpiarFormulario();
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = Math.Max(28, (int)(ClientSize.Width * 0.035));
            int anchoUtil = Math.Max(780, ClientSize.Width - (margenX * 2));
            int topContenido = _pnlTopBar.Height + 16;

            _pnlSubHeader.Bounds = new Rectangle(margenX, topContenido, anchoUtil, 62);
            _cardFechaOperativa.Location = new Point(anchoUtil - _cardFechaOperativa.Width, 2);

            int altoBarraAcciones = 66;
            int separacion = 16;
            int yTarjetas = topContenido + _pnlSubHeader.Height + 12;
            int altoTarjetas = Math.Max(415, ClientSize.Height - yTarjetas - altoBarraAcciones - (separacion * 2));

            int anchoCardLote = Math.Clamp((int)(anchoUtil * 0.34), 315, 395);
            int anchoCardForm = anchoUtil - anchoCardLote - separacion;

            _cardFormulario.Bounds = new Rectangle(margenX, yTarjetas, anchoCardForm, altoTarjetas);
            _cardLote.Bounds = new Rectangle(margenX + anchoCardForm + separacion, yTarjetas, anchoCardLote, altoTarjetas);

            int colGap = 22;
            int anchoColumna = (anchoCardForm - 52 - colGap) / 2;
            int xCol2 = 26 + anchoColumna + colGap;

            AjustarControlesInternosFormulario(anchoColumna, xCol2);
            AjustarControlesInternosLote(anchoCardLote, altoTarjetas);

            int yBarra = yTarjetas + altoTarjetas + separacion;
            _barraAcciones.Bounds = new Rectangle(margenX, yBarra, anchoUtil, altoBarraAcciones);
            _btnConfirmar.Location = new Point(anchoUtil - _btnConfirmar.Width - 16, (altoBarraAcciones - _btnConfirmar.Height) / 2);
            _btnCancelar.Location = new Point(_btnConfirmar.Left - _btnCancelar.Width - 12, (altoBarraAcciones - _btnCancelar.Height) / 2);
        }

        private void AjustarControlesInternosFormulario(int anchoColumna, int xCol2)
        {
            _cmbInsumo.Width = anchoColumna;
            _cmbProveedor.Left = xCol2;
            _cmbProveedor.Width = anchoColumna;

            foreach (Control c in _cardFormulario.Controls)
            {
                if (c is Label lbl && (lbl.Text.StartsWith("Proveedor") || lbl.Text.StartsWith("Unidad") || lbl.Text.StartsWith("Costo estimado") || lbl.Text.StartsWith("Fecha de caducidad")))
                {
                    lbl.Left = xCol2;
                }
            }

            if (_cardFormulario.Controls["pnlCantidad"] is Panel pnlCant)
            {
                pnlCant.Width = anchoColumna;
                _txtCantidad.Width = anchoColumna - 56;
                _lblUnidadSufijo.Location = new Point(anchoColumna - 38, 11);
            }

            _cmbUnidad.Left = xCol2;
            _cmbUnidad.Width = anchoColumna;

            if (_cardFormulario.Controls["pnlCostoUnit"] is Panel pnlCostoU)
            {
                pnlCostoU.Width = anchoColumna;
                _txtCostoUnitario.Width = anchoColumna - 84;
                if (pnlCostoU.Controls["lblMxn1"] is Label l1) l1.Location = new Point(anchoColumna - 46, 12);
            }

            if (_cardFormulario.Controls["pnlCostoEst"] is Panel pnlCostoE)
            {
                pnlCostoE.Left = xCol2;
                pnlCostoE.Width = anchoColumna;
                if (pnlCostoE.Controls["lblMxn2"] is Label l2) l2.Location = new Point(anchoColumna - 46, 12);
            }

            _dtpRecepcion.Width = anchoColumna;
            _dtpCaducidad.Left = xCol2;
            _dtpCaducidad.Width = anchoColumna;
        }

        private void AjustarControlesInternosLote(int anchoCardLote, int altoTarjetas)
        {
            int anchoInterno = anchoCardLote - 44;
            if (_cardLote.Controls["pnlBarcodeBox"] is Panel box)
            {
                box.Width = anchoInterno;
                foreach (Control c in box.Controls)
                {
                    if (c is Label) c.Width = anchoInterno - 20;
                }
                _picCodigoBarras.Left = (anchoInterno - _picCodigoBarras.Width) / 2;
            }

            if (_cardLote.Controls["pnlNota"] is Panel nota)
            {
                nota.Width = anchoInterno;
                if (nota.Controls.Count > 0) nota.Controls[0].Width = anchoInterno - 24;
            }

            int yPie = altoTarjetas - 56;
            if (_cardLote.Controls["lblEstadoTit"] is Label et) et.Location = new Point(22, yPie);
            if (_cardLote.Controls["lblEstadoVal"] is Label ev) ev.Location = new Point(22, yPie + 20);

            if (_cardLote.Controls["lblUbicTit"] is Label ut && _cardLote.Controls["lblUbicVal"] is Label uv)
            {
                int anchoBloqueUbic = Math.Max(ut.Width, uv.Width);
                int xUbic = anchoCardLote - anchoBloqueUbic - 22;
                ut.Location = new Point(xUbic, yPie);
                uv.Location = new Point(xUbic, yPie + 20);
            }
        }
    }
}