using CapaEntidades;
using CapaLogica;
using CapaPresentaciòn.Estilos;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_RecepcionMercancia : UserControl
    {
        private readonly InventarioNegocio _invNegocio = new();
        private readonly CultureInfo _culturaMxn = CultureInfo.GetCultureInfo("es-MX");

        private List<Insumo> _insumosDisponibles = new();
        private List<Proveedor> _proveedoresDisponibles = new();
        private List<UnidadMedida> _unidadesDisponibles = new();

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

        private CheckBox _chkSinCaducidad = null!;

        private PanelRedondeado _cardLote = null!;
        private TextBox _txtCodigoBarras = null!;
        private Label _lblCodigoLote = null!;

        /// <summary>Se dispara tras registrar una recepción (la ventana del Administrador recarga su inventario).</summary>
        public event EventHandler? RecepcionRegistrada;
        private PictureBox _picCodigoBarras = null!;

        private PanelRedondeado _barraAcciones = null!;
        private Button _btnCancelar = null!;
        private Button _btnConfirmar = null!;

        // RIU-04: errores de captura señalados junto a cada campo (sin MessageBox uno por uno)
        private Panel _pnlCantidad = null!;
        private Panel _pnlCostoUnit = null!;
        private Label _lblResumenValidacion = null!;
        private readonly Dictionary<Control, Label> _erroresCampo = new();
        private readonly ToolTip _toolTip = new();

        // Evita regenerar la vista previa del lote (consulta a MySQL) en cada tecla del buscador
        private int _idInsumoVistaPrevia = -1;
        private DateTime _fechaVistaPrevia = DateTime.MinValue;

        public UC_RecepcionMercancia()
        {
            InitializeComponent();
            Disposed += (s, e) => _toolTip.Dispose();
            ConfigurarInterfazRecepcion();
            _lblResumenValidacion.TextChanged += (s, e) => _toolTip.SetToolTip(_lblResumenValidacion, _lblResumenValidacion.Text);
            InicializarLogicaMysql();
        }

        /// <summary>Texto de la ruta superior (la ventana del Administrador la cambia a su contexto).</summary>
        public string Ruta
        {
            get => _lblRuta.Text;
            set => _lblRuta.Text = value;
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
                Text = DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", _culturaMxn),
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
            // Orden alfabético y búsqueda escribiendo (autocompleta con los nombres de la lista)
            _cmbInsumo = new ComboBox
            {
                Name = "cmbInsumo",
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                MaxDropDownItems = 12,
                Location = new Point(26, 102),
                Height = 36
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

            // Fila 2: Cantidad inicial * | Unidad de medida
            Label lblCantidad = CrearEtiquetaCampo("Cantidad inicial *", 26, 158);
            _pnlCantidad = new Panel
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

            _lblUnidadSufijo = new Label
            {
                Text = "kg",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };
            _pnlCantidad.Controls.AddRange(new Control[] { _txtCantidad, _lblUnidadSufijo });

            Label lblUnidad = CrearEtiquetaCampo("Unidad de medida", 320, 158);
            _cmbUnidad = new ComboBox
            {
                Name = "cmbUnidad",
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 182),
                Height = 36
            };

            // Fila 3: Costo unitario * | Costo estimado
            Label lblCostoUnit = CrearEtiquetaCampo("Costo unitario *", 26, 242);
            _pnlCostoUnit = new Panel
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

            Label lblMxn1 = new Label
            {
                Name = "lblMxn1",
                Text = "MXN",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };
            _pnlCostoUnit.Controls.AddRange(new Control[] { lblSigno1, _txtCostoUnitario, lblMxn1 });

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

            Label lblFechaCad = CrearEtiquetaCampo("Fecha de caducidad", 320, 326);
            _dtpCaducidad = new DateTimePicker
            {
                Name = "dtpCaducidad",
                Font = new Font("Segoe UI", 10.5F),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddMonths(3),
                Location = new Point(320, 350),
                Height = 36
            };

            // RF-01: la caducidad se captura "cuando corresponda" (vasos, popotes, etc. no caducan)
            _chkSinCaducidad = new CheckBox
            {
                Text = "No aplica",
                Font = new Font("Segoe UI", 9F),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true,
                Location = new Point(520, 325),
                Cursor = Cursors.Hand
            };
            _chkSinCaducidad.CheckedChanged += (s, e) => _dtpCaducidad.Enabled = !_chkSinCaducidad.Checked;

            // La fecha de recepción no puede ser futura (la lógica también lo valida)
            _dtpRecepcion.MaxDate = DateTime.Today.AddDays(1).AddTicks(-1);
            _dtpRecepcion.MinDate = DateTime.Today.AddDays(-InventarioNegocio.DiasMaximosRecepcionAtrasada);

            _cardFormulario.Controls.AddRange(new Control[]
            {
                badge01, lblCardTitulo, lblCardSub,
                lblInsumo, _cmbInsumo,
                lblProveedor, _cmbProveedor,
                lblCantidad, _pnlCantidad,
                lblUnidad, _cmbUnidad,
                lblCostoUnit, _pnlCostoUnit,
                lblCostoEst, pnlCostoEst,
                lblFechaRec, _dtpRecepcion,
                lblFechaCad, _dtpCaducidad, _chkSinCaducidad
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
                Text = "LOT-INS-20261116-001",
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

            pnlBarcodeBox.Controls.AddRange(new Control[] { lblIdTitulo, _lblCodigoLote, _picCodigoBarras });

            // Código de barras del empaque (opcional): si se deja vacío, se genera uno único al confirmar (RF-01)
            Label lblBarrasTit = new Label
            {
                Name = "lblBarrasTit",
                Text = "Código de barras del empaque (opcional)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(22, 240),
                AutoSize = true
            };

            _txtCodigoBarras = new TextBox
            {
                Name = "txtCodigoBarras",
                Font = new Font("Consolas", 10.5F),
                ForeColor = TemaCafeteria.TextoOscuro,
                PlaceholderText = "Vacío = se genera automáticamente",
                MaxLength = 100,
                Location = new Point(22, 260),
                Width = 310
            };

            PanelRedondeado pnlNota = new PanelRedondeado
            {
                Name = "pnlNota",
                Location = new Point(22, 296),
                Size = new Size(310, 56),
                RadioBorde = 10,
                BackColor = ColorTranslator.FromHtml("#EDF4F0")
            };

            Label lblNota = new Label
            {
                Text = "ℹ  Al confirmar se asigna el lote y un código de barras único (EAN-13 interno) si no capturas uno.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#264D3B"),
                Location = new Point(12, 7),
                Size = new Size(286, 42)
            };
            pnlNota.Controls.Add(lblNota);

            Label lblEstadoTit = new Label { Name = "lblEstadoTit", Text = "Estado inicial", Font = new Font("Segoe UI", 8.5F), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };
            Label lblEstadoVal = new Label { Name = "lblEstadoVal", Text = "● Disponible", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.SemaforoVerde, AutoSize = true };

            Label lblUbicTit = new Label { Name = "lblUbicTit", Text = "Ubicación", Font = new Font("Segoe UI", 8.5F), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };
            Label lblUbicVal = new Label { Name = "lblUbicVal", Text = "Almacén principal", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, AutoSize = true };

            _cardLote.Controls.AddRange(new Control[]
            {
                badgeBarcode, lblLoteTitulo, lblLoteSub,
                pnlBarcodeBox, lblBarrasTit, _txtCodigoBarras, pnlNota,
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

            // Resumen de validación (RIU-04 / RIU-08): cuántos campos hay que corregir o el error de guardado
            _lblResumenValidacion = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#FFCDD2"),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(18, 6),
                Text = string.Empty
            };

            _barraAcciones.Controls.AddRange(new Control[] { _lblResumenValidacion, _btnCancelar, _btnConfirmar });
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

        // =========================================================================
        // LÓGICA CONECTADA A MYSQL (CAPA NEGOCIO / CAPA DATOS)
        // =========================================================================

        private void InicializarLogicaMysql()
        {
            CargarCatalogosDesdeMysql();

            _cmbInsumo.SelectedIndexChanged += (s, e) => ActualizarVistaPreviaLote();
            _cmbInsumo.TextChanged += (s, e) =>
            {
                LimpiarErrorCampo(_cmbInsumo);
                ActualizarVistaPreviaLote();
            };
            _cmbInsumo.Leave += (s, e) => NormalizarInsumoEscrito();
            _cmbProveedor.SelectedIndexChanged += (s, e) => LimpiarErrorCampo(_cmbProveedor);

            _dtpRecepcion.ValueChanged += (s, e) =>
            {
                _lblFechaOperativaValor.Text = _dtpRecepcion.Value.ToString("dd 'de' MMMM 'de' yyyy", _culturaMxn);
                LimpiarErrorCampo(_dtpCaducidad);
                ActualizarVistaPreviaLote();
            };
            _dtpCaducidad.ValueChanged += (s, e) => LimpiarErrorCampo(_dtpCaducidad);
            _chkSinCaducidad.CheckedChanged += (s, e) => LimpiarErrorCampo(_dtpCaducidad);

            _txtCantidad.TextChanged += (s, e) =>
            {
                LimpiarErrorCampo(_pnlCantidad);
                RecalcularCostoEstimado();
            };
            _txtCostoUnitario.TextChanged += (s, e) =>
            {
                LimpiarErrorCampo(_pnlCostoUnit);
                RecalcularCostoEstimado();
            };
            _txtCodigoBarras.TextChanged += (s, e) => LimpiarErrorCampo(_txtCodigoBarras);

            _picCodigoBarras.Paint += PicCodigoBarras_Paint;

            _btnCancelar.Click += (s, e) => LimpiarFormulario();
            _btnConfirmar.Click += BtnConfirmar_Click;

            ActualizarVistaPreviaLote();
            RecalcularCostoEstimado();
        }

        private void CargarCatalogosDesdeMysql()
        {
            try
            {
                _invNegocio.AsegurarDatosInicialesInventario();

                // Orden alfabético estable (no por urgencia del semáforo)
                _insumosDisponibles = _invNegocio.ObtenerInsumosParaRecepcion();

                _proveedoresDisponibles = _invNegocio.ObtenerProveedores();
                _unidadesDisponibles = _invNegocio.ObtenerUnidades();

                _cmbInsumo.DataSource = null;
                _cmbInsumo.DisplayMember = "Nombre";
                _cmbInsumo.ValueMember = "IdInsumo";
                _cmbInsumo.DataSource = _insumosDisponibles;
                _cmbInsumo.SelectedIndex = -1;
                _cmbInsumo.Text = string.Empty;

                _cmbProveedor.DataSource = null;
                _cmbProveedor.DisplayMember = "Nombre";
                _cmbProveedor.ValueMember = "IdProveedor";
                _cmbProveedor.DataSource = _proveedoresDisponibles;
                _cmbProveedor.SelectedIndex = -1;

                _cmbUnidad.DataSource = null;
                _cmbUnidad.DisplayMember = "Nombre";
                _cmbUnidad.ValueMember = "IdUnidad";
                _cmbUnidad.DataSource = _unidadesDisponibles;
                _cmbUnidad.SelectedIndex = -1;
                _cmbUnidad.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudieron cargar los catálogos de MySQL:\n{ex.Message}",
                    "Error de conexión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Insumo elegido en el combo: el seleccionado de la lista o el que coincida exactamente
        /// con el texto escrito (sin distinguir mayúsculas ni acentos). Null si no existe.
        /// </summary>
        private Insumo? ResolverInsumo()
        {
            string texto = _cmbInsumo.Text.Trim();
            if (texto.Length == 0)
                return null;

            bool Coincide(Insumo i) => _culturaMxn.CompareInfo.Compare(
                i.Nombre.Trim(), texto, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

            if (_cmbInsumo.SelectedItem is Insumo seleccionado && Coincide(seleccionado))
                return seleccionado;

            return _insumosDisponibles.FirstOrDefault(Coincide);
        }

        /// <summary>Al salir del buscador, si lo escrito coincide con un insumo, se selecciona de la lista.</summary>
        private void NormalizarInsumoEscrito()
        {
            var insumo = ResolverInsumo();
            if (insumo != null && !ReferenceEquals(_cmbInsumo.SelectedItem, insumo))
                _cmbInsumo.SelectedItem = insumo;
        }

        private void ActualizarVistaPreviaLote()
        {
            var insumoSel = ResolverInsumo();

            // La vista previa consulta MySQL: solo se regenera si cambió el insumo o la fecha
            int idInsumo = insumoSel?.IdInsumo ?? 0;
            DateTime fecha = _dtpRecepcion.Value.Date;
            if (idInsumo == _idInsumoVistaPrevia && fecha == _fechaVistaPrevia)
                return;
            _idInsumoVistaPrevia = idInsumo;
            _fechaVistaPrevia = fecha;

            if (insumoSel != null)
            {
                _lblUnidadSufijo.Text = insumoSel.UnidadAbreviatura;
                var unidadMatch = _unidadesDisponibles.FirstOrDefault(u => u.IdUnidad == insumoSel.IdUnidad);
                if (unidadMatch != null)
                    _cmbUnidad.SelectedItem = unidadMatch;
            }
            else
            {
                _lblUnidadSufijo.Text = "ud";
                _cmbUnidad.SelectedIndex = -1;
            }

            try
            {
                _lblCodigoLote.Text = _invNegocio.GenerarVistaPreviaCodigoLote(insumoSel, _dtpRecepcion.Value);
            }
            catch (Exception)
            {
                // Solo es una vista previa: el código definitivo se genera al confirmar
                _lblCodigoLote.Text = $"LOT-INS-{_dtpRecepcion.Value:yyyyMMdd}-001";
            }
            _picCodigoBarras.Invalidate();
        }

        private void RecalcularCostoEstimado()
        {
            decimal cantidad = EntradaNumerica.LeerCantidadOCero(_txtCantidad.Text);
            decimal costoUnitario = EntradaNumerica.LeerImporteOCero(_txtCostoUnitario.Text);

            decimal totalEstimado = Redondeo.Importe(cantidad * costoUnitario);
            _lblCostoEstimadoValor.Text = $"$ {totalEstimado:N2}";
        }

        private void PicCodigoBarras_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.Clear(Color.White);

            string codigo = string.IsNullOrWhiteSpace(_lblCodigoLote.Text)
                ? "LOT-INS-000"
                : _lblCodigoLote.Text.Trim();

            int anchoDisp = _picCodigoBarras.Width - 24;
            int altoBarra = _picCodigoBarras.Height - 10;
            int x = 12;

            using var brocha = new SolidBrush(TemaCafeteria.TextoOscuro);

            // Barras de guarda inicial
            g.FillRectangle(brocha, x, 5, 2, altoBarra); x += 4;
            g.FillRectangle(brocha, x, 5, 1, altoBarra); x += 4;

            foreach (char c in codigo)
            {
                int valor = c;
                for (int bit = 0; bit < 4; bit++)
                {
                    if (x >= anchoDisp) break;
                    int grosor = ((valor >> bit) & 1) == 1 ? 3 : 1;
                    g.FillRectangle(brocha, x, 5, grosor, altoBarra);
                    x += grosor + (((valor >> (bit + 1)) & 1) == 1 ? 3 : 2);
                }
            }

            // Barras de guarda final
            if (x + 6 <= _picCodigoBarras.Width - 6)
            {
                g.FillRectangle(brocha, x, 5, 1, altoBarra); x += 3;
                g.FillRectangle(brocha, x, 5, 2, altoBarra);
            }
        }

        private void LimpiarFormulario()
        {
            _cmbInsumo.SelectedIndex = -1;
            _cmbInsumo.Text = string.Empty;
            _cmbProveedor.SelectedIndex = -1;
            _cmbUnidad.SelectedIndex = -1;
            _txtCantidad.Clear();
            _txtCostoUnitario.Clear();
            _lblCostoEstimadoValor.Text = "$ 0.00";
            _dtpRecepcion.Value = DateTime.Now;
            _dtpCaducidad.Value = DateTime.Now.AddMonths(3);
            _chkSinCaducidad.Checked = false;
            _txtCodigoBarras.Clear();
            LimpiarTodosLosErrores();
            _idInsumoVistaPrevia = -1;
            ActualizarVistaPreviaLote();
        }

        // =========================================================================
        // VALIDACIÓN EN PANTALLA (RIU-04): todos los errores a la vez, junto a su campo
        // =========================================================================

        /// <summary>
        /// Revisa todos los campos y marca cada uno que tenga error con un mensaje debajo.
        /// Devuelve false si hay al menos un error; no guarda nada. La lógica vuelve a validar al confirmar.
        /// </summary>
        private bool ValidarFormulario(
            out Insumo? insumo, out Proveedor? proveedor,
            out decimal cantidad, out decimal costo, out DateTime? caducidad)
        {
            LimpiarTodosLosErrores();
            var conError = new List<Control>();

            void Error(Control campo, string mensaje)
            {
                MarcarErrorCampo(campo, mensaje);
                conError.Add(campo);
            }

            insumo = ResolverInsumo();
            if (insumo == null)
                Error(_cmbInsumo, string.IsNullOrWhiteSpace(_cmbInsumo.Text)
                    ? "Selecciona o escribe el insumo."
                    : "No existe un insumo con ese nombre; elige uno de la lista.");

            proveedor = _cmbProveedor.SelectedItem as Proveedor;
            if (proveedor == null)
                Error(_cmbProveedor, "Selecciona el proveedor.");

            if (string.IsNullOrWhiteSpace(_txtCantidad.Text))
            {
                cantidad = 0m;
                Error(_pnlCantidad, "Captura la cantidad recibida.");
            }
            else if (!EntradaNumerica.TryLeerCantidad(_txtCantidad.Text, out cantidad, out string errorCantidad))
                Error(_pnlCantidad, errorCantidad);
            else if (cantidad <= 0)
                Error(_pnlCantidad, "La cantidad inicial debe ser mayor a cero.");

            if (string.IsNullOrWhiteSpace(_txtCostoUnitario.Text))
            {
                costo = 0m;
                Error(_pnlCostoUnit, "Captura el costo unitario.");
            }
            else if (!EntradaNumerica.TryLeerImporte(_txtCostoUnitario.Text, out costo, out string errorCosto))
                Error(_pnlCostoUnit, errorCosto.Replace("El importe", "El costo unitario"));
            else if (costo <= 0)
                Error(_pnlCostoUnit, "El costo unitario debe ser mayor a cero.");

            caducidad = _chkSinCaducidad.Checked ? null : _dtpCaducidad.Value.Date;
            if (caducidad.HasValue && caducidad.Value <= _dtpRecepcion.Value.Date)
                Error(_dtpCaducidad, "Debe ser posterior a la recepción (o marca \"No aplica\").");

            string codigoBarras = _txtCodigoBarras.Text.Trim();
            if (codigoBarras.Length > 0 && !codigoBarras.All(char.IsLetterOrDigit))
                Error(_txtCodigoBarras, "Solo admite letras y números.");

            if (conError.Count == 0)
                return true;

            _lblResumenValidacion.Text = conError.Count == 1
                ? "⚠  Corrige el campo marcado en rojo antes de confirmar."
                : $"⚠  Corrige los {conError.Count} campos marcados en rojo antes de confirmar.";

            // Foco en el primer campo con error (dentro de los paneles, en su caja de texto)
            Control primero = conError[0];
            if (primero == _pnlCantidad) _txtCantidad.Focus();
            else if (primero == _pnlCostoUnit) _txtCostoUnitario.Focus();
            else primero.Focus();

            return false;
        }

        private void MarcarErrorCampo(Control campo, string mensaje)
        {
            if (!_erroresCampo.TryGetValue(campo, out var lbl))
            {
                lbl = new Label
                {
                    Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                    ForeColor = TemaCafeteria.ErrorRojo,
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    AutoEllipsis = true,
                    Height = 15
                };
                campo.Parent?.Controls.Add(lbl);
                _erroresCampo[campo] = lbl;
            }

            lbl.Text = "⚠ " + mensaje;
            lbl.Visible = true;
            UbicarErrorCampo(campo, lbl);
            lbl.BringToFront();
        }

        private void LimpiarErrorCampo(Control campo)
        {
            if (_erroresCampo.TryGetValue(campo, out var lbl))
                lbl.Visible = false;

            // Al corregir el último campo marcado desaparece también el resumen
            if (!_erroresCampo.Values.Any(l => l.Visible))
                _lblResumenValidacion.Text = string.Empty;
        }

        private void LimpiarTodosLosErrores()
        {
            foreach (var lbl in _erroresCampo.Values)
                lbl.Visible = false;
            _lblResumenValidacion.Text = string.Empty;
        }

        private void ReubicarErroresCampo()
        {
            foreach (var (campo, lbl) in _erroresCampo)
                UbicarErrorCampo(campo, lbl);
        }

        private static void UbicarErrorCampo(Control campo, Label lbl)
        {
            lbl.Location = new Point(campo.Left, campo.Bottom + 1);
            lbl.Width = Math.Max(120, campo.Width);
        }

        private void BtnConfirmar_Click(object? sender, EventArgs e)
        {
            if (!ValidarFormulario(out var insumo, out var proveedor, out decimal cant, out decimal costo, out DateTime? caducidad)
                || insumo == null || proveedor == null)
                return;

            Insumo insumoSel = insumo;
            Proveedor provSel = proveedor;

            // El código de lote y, si no se capturó, el de barras los genera la lógica al confirmar,
            // verificando que no se repitan (RF-01). La vista previa solo orienta al usuario.
            var resultado = _invNegocio.RegistrarRecepcion(
                idProveedor: provSel.IdProveedor,
                idInsumo: insumoSel.IdInsumo,
                cantidadRecibida: cant,
                costoUnitario: costo,
                fechaRecepcion: _dtpRecepcion.Value,
                fechaCaducidad: caducidad,
                codigoLoteManual: null,
                codigoBarrasManual: string.IsNullOrWhiteSpace(_txtCodigoBarras.Text) ? null : _txtCodigoBarras.Text.Trim(),
                observaciones: $"Recepción de {provSel.Nombre} ({cant} {insumoSel.UnidadAbreviatura})"
            );

            if (!resultado.Exito)
            {
                // Error de la lógica o de MySQL: se muestra en la barra, sin ventana emergente
                if (resultado.Mensaje.Contains("código de barras", StringComparison.OrdinalIgnoreCase))
                    MarcarErrorCampo(_txtCodigoBarras, "Revisa este código de barras.");
                else if (resultado.Mensaje.Contains("costo unitario", StringComparison.OrdinalIgnoreCase))
                    MarcarErrorCampo(_pnlCostoUnit, resultado.Mensaje);
                else if (resultado.Mensaje.Contains("cantidad", StringComparison.OrdinalIgnoreCase))
                    MarcarErrorCampo(_pnlCantidad, resultado.Mensaje);
                else if (resultado.Mensaje.Contains("caducidad", StringComparison.OrdinalIgnoreCase))
                    MarcarErrorCampo(_dtpCaducidad, resultado.Mensaje);

                _lblResumenValidacion.Text = "⚠  No se registró: " + resultado.Mensaje.Replace("\r", " ").Replace("\n", " ");
                return;
            }

            RecepcionRegistrada?.Invoke(this, EventArgs.Empty);

            MessageBox.Show(
                $"{resultado.Mensaje}\n\n" +
                $"• Insumo: {insumoSel.Nombre}\n" +
                $"• Proveedor: {provSel.Nombre}\n" +
                $"• Cantidad: {cant} {insumoSel.UnidadAbreviatura}\n" +
                $"• Costo Total: {_lblCostoEstimadoValor.Text} MXN",
                "Existencias Actualizadas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

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
            _lblResumenValidacion.Size = new Size(Math.Max(80, _btnCancelar.Left - 30), altoBarraAcciones - 12);

            ReubicarErroresCampo();
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
            _chkSinCaducidad.Location = new Point(xCol2 + anchoColumna - _chkSinCaducidad.Width, 325);
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

            _txtCodigoBarras.Width = anchoInterno;

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