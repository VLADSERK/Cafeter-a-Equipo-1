using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CapaEntidades;
using CapaLogica;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Estilos;
using CapaPresentaciòn.Formularios;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_RegistroMerma : UserControl
    {
        private readonly InventarioNegocio _invNegocio = new();
        private readonly MermaNegocio _mermaNegocio = new();

        private List<ResumenInsumoInventario> _resumenInventario = new();
        private List<Lote> _lotesDelInsumoActual = new();
        private Lote? _loteSeleccionado;          // null = lote no identificado → descuento PEPS (RIU-05)
        private Insumo? _insumoSeleccionado;
        private ConfiguracionGeneral _configActual = new();
        private int _mermasRegistradasTurno = 0;
        private readonly string _responsableTurno;

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblBadgeMermasTurno = null!;

        // Sub-encabezado + Tarjeta de auditoría (Quién y Cuándo)
        private Panel _pnlSubHeader = null!;
        private Label _lblSubtitulo = null!;
        private Label _lblDescripcion = null!;
        private PanelRedondeado _cardAuditoria = null!;
        private Label _lblResponsableValor = null!;
        private Label _lblFechaReporteValor = null!;

        // Tarjeta izquierda: Formulario de merma
        private PanelRedondeado _cardFormulario = null!;
        private ComboBox _cmbInsumo = null!;
        private ComboBox _cmbLote = null!;
        private TextBox _txtCantidadMerma = null!;
        private Label _lblUnidadSufijo = null!;
        private ComboBox _cmbMotivo = null!;
        private TextBox _txtObservaciones = null!;

        // Tarjeta derecha: Impacto PEPS y autorización
        private PanelRedondeado _cardImpacto = null!;
        private Label _lblStockActualTit = null!;
        private Label _lblStockActualVal = null!;
        private Label _lblDescuentoVal = null!;
        private Label _lblCostoMermaVal = null!;
        private Label _lblStockFinalVal = null!;
        private PanelRedondeado _pnlAlertaAutorizacion = null!;
        private Label _lblTextoAutorizacion = null!;
        private Label _lblSemaforoResultante = null!;

        // Barra inferior de acciones
        private PanelRedondeado _barraAcciones = null!;
        private Button _btnLimpiar = null!;
        private Button _btnRegistrarMerma = null!;

        public UC_RegistroMerma(string? responsable = null)
        {
            _responsableTurno = !string.IsNullOrWhiteSpace(responsable)
                ? responsable
                : (SesionActual.HaySesionActiva ? $"{SesionActual.NombreUsuario} ({SesionActual.Rol})" : "Cajero / Barista");

            InitializeComponent();
            ConfigurarInterfazMerma();
            ActualizarImpactoInventario();

            // Existencias frescas de MySQL al abrir y al volver a la pantalla (misma fuente que los inventarios)
            RecargaAutomatica.AlMostrar(this, () =>
            {
                CargarDatosDesdeMysql();
                LimpiarFormulario();
            });
        }

        private void ConfigurarInterfazMerma()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra superior blanca
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
                Text = "OPERACIONES / CONTROL DE INVENTARIO",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(36, 16),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Registro de merma",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(34, 34),
                AutoSize = true
            };

            _lblBadgeMermasTurno = new Label
            {
                Text = "0 mermas hoy",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#B26A00"),
                BackColor = ColorTranslator.FromHtml("#FFF4E5"),
                Padding = new Padding(12, 6, 12, 6),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblBadgeMermasTurno });

            // 2. Sub-encabezado + Tarjeta de Quién reporta y Fecha
            _pnlSubHeader = new Panel { BackColor = Color.Transparent };

            _lblSubtitulo = new Label
            {
                Text = "Reportar pérdida o desperdicio de insumo",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(0, 4),
                AutoSize = true
            };

            _lblDescripcion = new Label
            {
                Text = "Selecciona el insumo y, si lo conoces, el lote afectado; si no, se descontará en orden PEPS (recepción más antigua primero).",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(0, 32),
                AutoSize = true
            };

            _cardAuditoria = new PanelRedondeado
            {
                Size = new Size(465, 62),
                RadioBorde = 12,
                BackColor = Color.White
            };

            Label lblRespTit = new Label
            {
                Text = "Reportado por",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(16, 10),
                AutoSize = true
            };

            _lblResponsableValor = new Label
            {
                Text = _responsableTurno,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(16, 28),
                AutoSize = true
            };

            Panel sepAuditoria = new Panel
            {
                Location = new Point(242, 12),
                Size = new Size(1, 38),
                BackColor = TemaCafeteria.BordeSuave
            };

            Label lblFechaTit = new Label
            {
                Text = "Fecha del reporte",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(256, 10),
                AutoSize = true
            };

            _lblFechaReporteValor = new Label
            {
                Text = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt"),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(256, 28),
                AutoSize = true
            };

            _cardAuditoria.Controls.AddRange(new Control[]
            {
                lblRespTit, _lblResponsableValor,
                sepAuditoria,
                lblFechaTit, _lblFechaReporteValor
            });
            _pnlSubHeader.Controls.AddRange(new Control[] { _lblSubtitulo, _lblDescripcion, _cardAuditoria });

            ConstruirTarjetaFormulario();
            ConstruirTarjetaImpacto();
            ConstruirBarraAcciones();

            Controls.AddRange(new Control[]
            {
                _pnlSubHeader,
                _cardFormulario,
                _cardImpacto,
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
                ForeColor = ColorTranslator.FromHtml("#B26A00"),
                BackColor = ColorTranslator.FromHtml("#FFF4E5"),
                Size = new Size(34, 34),
                Location = new Point(26, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblCardTitulo = new Label
            {
                Text = "Detalle de la incidencia",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(72, 18),
                AutoSize = true
            };

            Label lblCardSub = new Label
            {
                Text = "Los campos con * son obligatorios para auditoría.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(72, 40),
                AutoSize = true
            };

            // Fila 1: Insumo * | Lote PEPS *
            Label lblInsumo = CrearEtiquetaCampo("Insumo afectado *", 26, 78);
            _cmbInsumo = new ComboBox
            {
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(26, 102),
                Height = 36
            };
            _cmbInsumo.SelectedIndexChanged += CmbInsumo_SelectedIndexChanged;

            Label lblLote = CrearEtiquetaCampo("Lote afectado (si se conoce)", 320, 78);
            _cmbLote = new ComboBox
            {
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 102),
                Height = 36,
                Enabled = false
            };
            _cmbLote.SelectedIndexChanged += CmbLote_SelectedIndexChanged;

            // Fila 2: Cantidad mermada * | Motivo *
            Label lblCantidad = CrearEtiquetaCampo("Cantidad mermada *", 26, 158);
            Panel pnlCantidad = new Panel
            {
                Name = "pnlCantidad",
                Location = new Point(26, 182),
                Height = 44,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _txtCantidadMerma = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                PlaceholderText = "0.00",
                Location = new Point(12, 11),
                Height = 24
            };
            _txtCantidadMerma.TextChanged += (s, e) => ActualizarImpactoInventario();

            _lblUnidadSufijo = new Label
            {
                Text = "kg",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };
            pnlCantidad.Controls.AddRange(new Control[] { _txtCantidadMerma, _lblUnidadSufijo });

            Label lblMotivo = CrearEtiquetaCampo("Motivo de merma *", 320, 158);
            _cmbMotivo = new ComboBox
            {
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 182),
                Height = 36
            };
            _cmbMotivo.Items.Add("Seleccionar motivo...");
            _cmbMotivo.Items.AddRange(MermaNegocio.Motivos);
            _cmbMotivo.SelectedIndex = 0;
            _cmbMotivo.SelectedIndexChanged += (s, e) => ActualizarImpactoInventario();

            // Fila 3: Observaciones / Justificación *
            Label lblObs = CrearEtiquetaCampo("Descripción de la incidencia (obligatoria con el motivo \"Otros\")", 26, 244);
            _txtObservaciones = new TextBox
            {
                Name = "txtObservaciones",
                Font = new Font("Segoe UI", 10F),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                Multiline = true,
                Location = new Point(26, 268),
                Height = 84,
                PlaceholderText = "Describe brevemente qué ocurrió con el insumo (ej. envase roto al abrir, vencimiento en refrigeración...)"
            };

            _cardFormulario.Controls.AddRange(new Control[]
            {
                badge01, lblCardTitulo, lblCardSub,
                lblInsumo, _cmbInsumo,
                lblLote, _cmbLote,
                lblCantidad, pnlCantidad,
                lblMotivo, _cmbMotivo,
                lblObs, _txtObservaciones
            });
        }

        private void ConstruirTarjetaImpacto()
        {
            _cardImpacto = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label badge02 = new Label
            {
                Text = "02",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#EFE6DC"),
                Size = new Size(34, 34),
                Location = new Point(22, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblImpTitulo = new Label
            {
                Text = "Impacto en lote PEPS",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(66, 18),
                AutoSize = true
            };

            Label lblImpSub = new Label
            {
                Text = "Cálculo previo al descuento",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(66, 40),
                AutoSize = true
            };

            // Caja resumen numérico (ampliada a 168 px para incluir el Costo Estimado)
            Panel pnlResumen = new Panel
            {
                Name = "pnlResumen",
                Location = new Point(22, 74),
                Size = new Size(310, 168),
                BackColor = ColorTranslator.FromHtml("#FAF8F5")
            };
            pnlResumen.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlResumen.Width - 1, pnlResumen.Height - 1);
            };

            Label l1 = _lblStockActualTit = new Label { Text = "Existencia actual en lote:", Font = new Font("Segoe UI", 9F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(14, 14), AutoSize = true };
            _lblStockActualVal = new Label { Name = "lblStockActualVal", Text = "—", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, AutoSize = true };

            Label l2 = new Label { Text = "Cantidad a descontar:", Font = new Font("Segoe UI", 9F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(14, 44), AutoSize = true };
            _lblDescuentoVal = new Label { Name = "lblDescuentoVal", Text = "0.00", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#C62828"), AutoSize = true };

            Label lCosto = new Label { Text = "Costo estimado merma:", Font = new Font("Segoe UI", 9F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(14, 74), AutoSize = true };
            _lblCostoMermaVal = new Label { Name = "lblCostoMermaVal", Text = "$ 0.00", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, AutoSize = true };

            Panel sep = new Panel { Name = "sepResumen", Location = new Point(14, 108), Height = 1, BackColor = TemaCafeteria.BordeSuave };

            Label l3 = new Label { Text = "Existencia resultante:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, Location = new Point(14, 124), AutoSize = true };
            _lblStockFinalVal = new Label { Name = "lblStockFinalVal", Text = "—", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TemaCafeteria.CafePrimario, AutoSize = true };

            pnlResumen.Controls.AddRange(new Control[] { l1, _lblStockActualVal, l2, _lblDescuentoVal, lCosto, _lblCostoMermaVal, sep, l3, _lblStockFinalVal });

            _pnlAlertaAutorizacion = new PanelRedondeado
            {
                Name = "pnlAlertaAutorizacion",
                Location = new Point(22, 254),
                Size = new Size(310, 96),
                RadioBorde = 10,
                BackColor = ColorTranslator.FromHtml("#EDF4F0")
            };

            _lblTextoAutorizacion = new Label
            {
                Text = "ℹ  Selecciona un lote y captura la cantidad para validar si requiere autorización de supervisor.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#264D3B"),
                Location = new Point(12, 12),
                Size = new Size(286, 72)
            };
            _pnlAlertaAutorizacion.Controls.Add(_lblTextoAutorizacion);

            Label lblSemTit = new Label { Name = "lblSemTit", Text = "Nuevo estado semáforo", Font = new Font("Segoe UI", 8.5F), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };
            _lblSemaforoResultante = new Label { Name = "lblSemaforoResultante", Text = "● Sin cambios", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };

            _cardImpacto.Controls.AddRange(new Control[]
            {
                badge02, lblImpTitulo, lblImpSub,
                pnlResumen, _pnlAlertaAutorizacion,
                lblSemTit, _lblSemaforoResultante
            });
        }

        private void ConstruirBarraAcciones()
        {
            _barraAcciones = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = ColorTranslator.FromHtml("#2E1E16")
            };

            _btnLimpiar = new Button
            {
                Text = "Limpiar formulario",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(195, 42),
                Cursor = Cursors.Hand
            };
            _btnLimpiar.FlatAppearance.BorderSize = 0;
            _btnLimpiar.Click += (s, e) => LimpiarFormulario();

            _btnRegistrarMerma = new Button
            {
                Text = "✓  Registrar merma y descontar lote",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#B26A00"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(320, 42),
                Cursor = Cursors.Hand
            };
            _btnRegistrarMerma.FlatAppearance.BorderSize = 0;
            _btnRegistrarMerma.Click += BtnRegistrarMerma_Click;

            _barraAcciones.Controls.AddRange(new Control[] { _btnLimpiar, _btnRegistrarMerma });
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
        // CARGA Y SINCRONIZACIÓN CON MYSQL
        // =========================================================================

        private void CargarDatosDesdeMysql()
        {
            try
            {
                _configActual = _mermaNegocio.ObtenerConfiguracionActual();
                _resumenInventario = _invNegocio.ObtenerResumenInventarioCompleto();
                _mermasRegistradasTurno = _mermaNegocio.ObtenerConteoMermasHoyUsuarioActual();
                _lblBadgeMermasTurno.Text = $"{_mermasRegistradasTurno} {(_mermasRegistradasTurno == 1 ? "merma" : "mermas")} hoy";

                var listaCombo = new List<Insumo>
                {
                    new Insumo { IdInsumo = 0, Nombre = "Seleccionar insumo..." }
                };
                listaCombo.AddRange(_resumenInventario.Select(r => r.Insumo));

                _cmbInsumo.DataSource = null;
                _cmbInsumo.DisplayMember = "Nombre";
                _cmbInsumo.ValueMember = "IdInsumo";
                _cmbInsumo.DataSource = listaCombo;
                _cmbInsumo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar los insumos y lotes desde MySQL:\n{ex.Message}",
                    "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CmbInsumo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _cmbLote.Items.Clear();
            _lotesDelInsumoActual.Clear();
            _loteSeleccionado = null;
            _insumoSeleccionado = null;

            if (_cmbInsumo.SelectedItem is not Insumo insumoSel || insumoSel.IdInsumo <= 0)
            {
                _cmbLote.Items.Add("Selecciona primero un insumo...");
                _cmbLote.SelectedIndex = 0;
                _cmbLote.Enabled = false;
                _lblUnidadSufijo.Text = "ud";
                ActualizarImpactoInventario();
                return;
            }

            _insumoSeleccionado = insumoSel;
            _lblUnidadSufijo.Text = insumoSel.UnidadAbreviatura;

            var resumenInsumo = _resumenInventario.FirstOrDefault(r => r.Insumo.IdInsumo == insumoSel.IdInsumo);
            if (resumenInsumo != null)
            {
                _lotesDelInsumoActual = resumenInsumo.LotesActivos
                    .Where(l => l.CantidadDisponible > 0)
                    .ToList();
            }

            if (_lotesDelInsumoActual.Count == 0)
            {
                _cmbLote.Items.Add("Sin lotes con existencia");
                _cmbLote.SelectedIndex = 0;
                _cmbLote.Enabled = false;
            }
            else
            {
                _cmbLote.Enabled = true;
                // RIU-05: el lote se elige "cuando se conozca"; por omisión se descuenta por PEPS
                _cmbLote.Items.Add("No identificado · descontar por PEPS");
                foreach (var lote in _lotesDelInsumoActual)
                {
                    string cadTexto = lote.FechaCaducidad.HasValue ? lote.FechaCaducidad.Value.ToString("dd/MM/yy") : "S/C";
                    _cmbLote.Items.Add($"{lote.CodigoLote} ({FormatoInventario.Cantidad(lote.CantidadDisponible, lote.UnidadAbreviatura)} - Cad: {cadTexto})");
                }
                _cmbLote.SelectedIndex = 0;
            }

            ActualizarImpactoInventario();
        }

        private void CmbLote_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int indiceLote = _cmbLote.SelectedIndex - 1;   // 0 = "No identificado (PEPS)"
            _loteSeleccionado = _cmbLote.Enabled && indiceLote >= 0 && indiceLote < _lotesDelInsumoActual.Count
                ? _lotesDelInsumoActual[indiceLote]
                : null;

            ActualizarImpactoInventario();
        }

        private void ActualizarImpactoInventario()
        {
            _lblStockActualTit.Text = _loteSeleccionado != null ? "Existencia actual en lote:" : "Existencia del insumo (PEPS):";

            if (_insumoSeleccionado == null || _lotesDelInsumoActual.Count == 0)
            {
                _lblStockActualVal.Text = _insumoSeleccionado == null ? "—" : $"0 {_insumoSeleccionado.UnidadAbreviatura}";
                _lblDescuentoVal.Text = "0.00";
                _lblCostoMermaVal.Text = "$ 0.00";
                _lblStockFinalVal.Text = "—";
                _lblStockFinalVal.ForeColor = TemaCafeteria.CafePrimario;
                _lblSemaforoResultante.Text = "● Sin cambios";
                _lblSemaforoResultante.ForeColor = TemaCafeteria.TextoSecundario;
                _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#EDF4F0");
                _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#264D3B");
                _lblTextoAutorizacion.Text = _insumoSeleccionado == null
                    ? $"ℹ  Selecciona el insumo y captura la cantidad. Límite sin autorización: ${_configActual.LimiteEconomicoMerma:N2} MXN."
                    : "ℹ  Este insumo no tiene existencias, no hay nada que registrar como merma.";
                PosicionarValoresResumen();
                return;
            }

            string u = _insumoSeleccionado.UnidadAbreviatura;
            decimal merma = EntradaNumerica.LeerCantidadOCero(_txtCantidadMerma.Text);

            // Mismo cálculo que MermaNegocio: lote elegido o reparto PEPS, Half-Up a 2 decimales (RNF-08)
            var estimacion = MermaNegocio.Estimar(_lotesDelInsumoActual, _loteSeleccionado, merma);
            decimal costoEstimado = estimacion.CostoEstimado;

            _lblStockActualVal.Text = FormatoInventario.Cantidad(estimacion.Disponible, u);
            _lblDescuentoVal.Text = $"- {FormatoInventario.Cantidad(merma, u)}";
            _lblCostoMermaVal.Text = $"$ {costoEstimado:N2}";

            string destino = _loteSeleccionado != null
                ? $"del lote {_loteSeleccionado.CodigoLote}"
                : estimacion.Asignacion.Count == 0
                    ? "por PEPS"
                    : "por PEPS: " + string.Join(", ", estimacion.Asignacion.Select(a => $"{a.Lote.CodigoLote} ({FormatoInventario.Numero(a.Cantidad)})"));

            if (!estimacion.Alcanza)
            {
                _lblStockFinalVal.Text = _loteSeleccionado != null ? "Excede lote" : "Excede existencia";
                _lblStockFinalVal.ForeColor = ColorTranslator.FromHtml("#C62828");
                _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#FFEBEE");
                _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#C62828");
                _lblTextoAutorizacion.Text = _loteSeleccionado != null
                    ? "⚠  La cantidad mermada no puede ser mayor a la existencia disponible en el lote seleccionado."
                    : "⚠  La cantidad mermada no puede ser mayor a la existencia total del insumo.";
            }
            else
            {
                _lblStockFinalVal.Text = FormatoInventario.Cantidad(estimacion.Disponible - merma, u);
                _lblStockFinalVal.ForeColor = TemaCafeteria.CafePrimario;

                // Calcular nuevo semáforo global del insumo usando la configuración de MySQL
                var resumenIns = _resumenInventario.FirstOrDefault(r => r.Insumo.IdInsumo == _insumoSeleccionado.IdInsumo);
                decimal stockGlobalActual = resumenIns?.Insumo.StockActualTotal ?? estimacion.Disponible;
                decimal stockMaximo = resumenIns?.Insumo.StockMaximo ?? _insumoSeleccionado.StockMaximo;
                decimal stockGlobalResultante = Math.Max(0, stockGlobalActual - merma);

                // Mismo semáforo que el inventario (RF-08): estado textual, porcentaje y aviso de reabastecimiento
                var semaforoNuevo = SemaforoExistencias.Evaluar(stockGlobalResultante, stockMaximo, _configActual);
                string aviso = semaforoNuevo.AvisoReabastecimiento is string a ? $"  ·  {a}" : "";
                _lblSemaforoResultante.Text = $"●  {semaforoNuevo.Etiqueta} ({semaforoNuevo.Porcentaje:0.0} %){aviso}";
                _lblSemaforoResultante.ForeColor = SemaforoVisual.Acento(semaforoNuevo.Nivel);

                // Evaluar regla económica configurada en ConfiguracionGeneral (RF-09)
                if (MermaNegocio.RequiereAutorizacion(costoEstimado, _configActual))
                {
                    _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#FFF4E5");
                    _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#B26A00");
                    _lblTextoAutorizacion.Text = $"🔒  Costo (${costoEstimado:N2}) supera el límite de ${_configActual.LimiteEconomicoMerma:N2}. Se pedirá de inmediato el NIP del Administrador; si no se autoriza, quedará PENDIENTE sin descontar. Descuento {destino}.";
                    _btnRegistrarMerma.Text = "🔒  Registrar y solicitar autorización";
                }
                else
                {
                    _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#EDF4F0");
                    _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#264D3B");
                    _lblTextoAutorizacion.Text = $"✓  Dentro del límite (${_configActual.LimiteEconomicoMerma:N2}). Se autorizará y descontará de inmediato {destino}.";
                    _btnRegistrarMerma.Text = "✓  Registrar merma y descontar";
                }
            }

            PosicionarValoresResumen();
        }

        private void PosicionarValoresResumen()
        {
            if (_cardImpacto.Controls["pnlResumen"] is Panel pnlRes)
            {
                _lblStockActualVal.Location = new Point(pnlRes.Width - _lblStockActualVal.Width - 14, 14);
                _lblDescuentoVal.Location = new Point(pnlRes.Width - _lblDescuentoVal.Width - 14, 44);
                _lblCostoMermaVal.Location = new Point(pnlRes.Width - _lblCostoMermaVal.Width - 14, 74);
                _lblStockFinalVal.Location = new Point(pnlRes.Width - _lblStockFinalVal.Width - 14, 122);
            }
        }

        private void LimpiarFormulario()
        {
            if (_cmbInsumo.Items.Count > 0) _cmbInsumo.SelectedIndex = 0;
            _txtCantidadMerma.Clear();
            _cmbMotivo.SelectedIndex = 0;
            _txtObservaciones.Clear();
            _lblFechaReporteValor.Text = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
            ActualizarImpactoInventario();
        }

        private void BtnRegistrarMerma_Click(object? sender, EventArgs e)
        {
            if (_insumoSeleccionado == null)
            {
                MessageBox.Show("Selecciona el insumo afectado.", "Campos obligatorios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbInsumo.Focus();
                return;
            }

            if (_lotesDelInsumoActual.Count == 0)
            {
                MessageBox.Show("Este insumo no tiene existencias; no hay nada que registrar como merma.", "Sin existencias", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EntradaNumerica.TryLeerCantidad(_txtCantidadMerma.Text, out decimal cantMerma, out string errorCantidad) || cantMerma <= 0)
            {
                string mensaje = errorCantidad.Length > 0 ? $"Cantidad mermada: {errorCantidad}" : "Ingresa una cantidad mermada mayor a cero.";
                MessageBox.Show(mensaje, "Validación de cantidad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtCantidadMerma.Focus();
                return;
            }

            if (_cmbMotivo.SelectedIndex <= 0)
            {
                MessageBox.Show("Selecciona el motivo de la merma.", "Motivo obligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string motivo = _cmbMotivo.SelectedItem?.ToString() ?? "";

            // Validaciones previas para no pedir el NIP de una merma que de todos modos se rechazaría
            var estimacion = MermaNegocio.Estimar(_lotesDelInsumoActual, _loteSeleccionado, cantMerma);
            if (!estimacion.Alcanza)
            {
                MessageBox.Show(_loteSeleccionado != null
                        ? "La cantidad mermada no puede superar la existencia disponible del lote."
                        : "La cantidad mermada no puede superar la existencia total del insumo.",
                    "Validación de cantidad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtCantidadMerma.Focus();
                return;
            }

            if (motivo == MermaNegocio.MotivoOtros && string.IsNullOrWhiteSpace(_txtObservaciones.Text))
            {
                MessageBox.Show("Con el motivo \"Otros\" es obligatorio describir qué ocurrió con el insumo.", "Descripción obligatoria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtObservaciones.Focus();
                return;
            }

            // RF-09 / RIU-05: si el costo supera el límite, se pide de inmediato el NIP del Administrador
            string? nipAdministrador = null;
            decimal costoEstimado = estimacion.CostoEstimado;
            if (MermaNegocio.RequiereAutorizacion(costoEstimado, _configActual))
            {
                string loteTexto = _loteSeleccionado != null ? $"lote {_loteSeleccionado.CodigoLote}" : "lote no identificado (PEPS)";
                nipAdministrador = DialogoNipAdministrador.Solicitar(
                    FindForm() ?? (IWin32Window)this,
                    "Merma sobre el límite",
                    $"{_insumoSeleccionado.Nombre} · {loteTexto}\n" +
                    $"Cantidad: {FormatoInventario.Cantidad(cantMerma, _insumoSeleccionado.UnidadAbreviatura)} · Costo estimado: ${costoEstimado:N2} " +
                    $"(límite ${_configActual.LimiteEconomicoMerma:N2}).\n\n" +
                    "Si no se autoriza ahora, la merma quedará PENDIENTE y el inventario no se descontará.",
                    "No autorizar ahora");
            }

            var resultado = _mermaNegocio.RegistrarMerma(_insumoSeleccionado.IdInsumo, _loteSeleccionado, cantMerma, motivo, _txtObservaciones.Text, nipAdministrador);

            if (!resultado.Exito)
            {
                MessageBox.Show(resultado.Mensaje, "No se pudo registrar la merma", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                resultado.Mensaje,
                resultado.EstadoFinal == "Autorizada" ? "Merma autorizada y descontada" : "Merma pendiente de autorización",
                MessageBoxButtons.OK,
                resultado.EstadoFinal == "Autorizada" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            CargarDatosDesdeMysql();
            LimpiarFormulario();
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            _lblBadgeMermasTurno.Location = new Point(_pnlTopBar.Width - _lblBadgeMermasTurno.Width - 34, 26);

            int margenX = Math.Max(28, (int)(ClientSize.Width * 0.035));
            int anchoUtil = Math.Max(780, ClientSize.Width - (margenX * 2));
            int topContenido = _pnlTopBar.Height + 16;

            _pnlSubHeader.Bounds = new Rectangle(margenX, topContenido, anchoUtil, 66);
            _cardAuditoria.Location = new Point(anchoUtil - _cardAuditoria.Width, 0);

            int altoBarraAcciones = 66;
            int separacion = 16;
            int yTarjetas = topContenido + _pnlSubHeader.Height + 12;
            int altoTarjetas = Math.Max(420, ClientSize.Height - yTarjetas - altoBarraAcciones - (separacion * 2));

            int anchoCardImpacto = Math.Clamp((int)(anchoUtil * 0.35), 320, 395);
            int anchoCardForm = anchoUtil - anchoCardImpacto - separacion;

            _cardFormulario.Bounds = new Rectangle(margenX, yTarjetas, anchoCardForm, altoTarjetas);
            _cardImpacto.Bounds = new Rectangle(margenX + anchoCardForm + separacion, yTarjetas, anchoCardImpacto, altoTarjetas);

            int colGap = 22;
            int anchoColumna = (anchoCardForm - 52 - colGap) / 2;
            int xCol2 = 26 + anchoColumna + colGap;

            _cmbInsumo.Width = anchoColumna;
            _cmbLote.Left = xCol2;
            _cmbLote.Width = anchoColumna;

            foreach (Control c in _cardFormulario.Controls)
            {
                if (c is Label lbl && (lbl.Text.StartsWith("Lote afectado") || lbl.Text.StartsWith("Motivo de merma")))
                {
                    lbl.Left = xCol2;
                }
            }

            if (_cardFormulario.Controls["pnlCantidad"] is Panel pnlCant)
            {
                pnlCant.Width = anchoColumna;
                _txtCantidadMerma.Width = anchoColumna - 56;
                _lblUnidadSufijo.Location = new Point(anchoColumna - 38, 12);
            }

            _cmbMotivo.Left = xCol2;
            _cmbMotivo.Width = anchoColumna;

            _txtObservaciones.Width = anchoCardForm - 52;
            _txtObservaciones.Height = Math.Max(78, altoTarjetas - 292);

            int anchoInternoDer = anchoCardImpacto - 44;
            if (_cardImpacto.Controls["pnlResumen"] is Panel pnlRes)
            {
                pnlRes.Width = anchoInternoDer;
                if (pnlRes.Controls["sepResumen"] is Panel sep) sep.Width = anchoInternoDer - 28;
            }

            _pnlAlertaAutorizacion.Width = anchoInternoDer;
            _lblTextoAutorizacion.Width = anchoInternoDer - 24;

            int yPie = altoTarjetas - 56;
            if (_cardImpacto.Controls["lblSemTit"] is Label st) st.Location = new Point(22, yPie);
            _lblSemaforoResultante.Location = new Point(22, yPie + 20);

            PosicionarValoresResumen();

            int yBarra = yTarjetas + altoTarjetas + separacion;
            _barraAcciones.Bounds = new Rectangle(margenX, yBarra, anchoUtil, altoBarraAcciones);
            _btnRegistrarMerma.Location = new Point(anchoUtil - _btnRegistrarMerma.Width - 16, (altoBarraAcciones - _btnRegistrarMerma.Height) / 2);
            _btnLimpiar.Location = new Point(_btnRegistrarMerma.Left - _btnLimpiar.Width - 12, (altoBarraAcciones - _btnLimpiar.Height) / 2);
        }
    }
}