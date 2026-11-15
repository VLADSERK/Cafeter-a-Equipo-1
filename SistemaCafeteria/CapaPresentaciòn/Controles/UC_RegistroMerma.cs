using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Estilos;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_RegistroMerma : UserControl
    {
        private readonly List<LoteInventarioMock> _lotesDisponibles = new();
        private LoteInventarioMock? _loteSeleccionado;
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
        private Label _lblStockActualVal = null!;
        private Label _lblDescuentoVal = null!;
        private Label _lblStockFinalVal = null!;
        private PanelRedondeado _pnlAlertaAutorizacion = null!;
        private Label _lblTextoAutorizacion = null!;
        private Label _lblSemaforoResultante = null!;

        // Barra inferior de acciones
        private PanelRedondeado _barraAcciones = null!;
        private Button _btnLimpiar = null!;
        private Button _btnRegistrarMerma = null!;

        public UC_RegistroMerma(string responsable = "Cajero / Barista (Caja 01)")
        {
            _responsableTurno = responsable;
            InitializeComponent();
            _lotesDisponibles.AddRange(CatalogoInsumosMock.ObtenerLotesIniciales());
            ConfigurarInterfazMerma();
            ActualizarImpactoInventario();
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
                Text = "0 mermas en el turno",
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
                Text = "Selecciona el insumo y el lote PEPS afectado para descontar existencias y mantener trazabilidad.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(0, 32),
                AutoSize = true
            };
            _cardAuditoria = new PanelRedondeado
            {
                Size = new Size(465, 62), // Ampliamos de 415 a 465 px para que quepa todo el "p. m." con holgura
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
                Text = "Todos los campos con * son obligatorios para auditoría.",
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
            _cmbInsumo.Items.Add("Seleccionar insumo...");
            foreach (var ins in CatalogoInsumosMock.ListaIngredientes)
            {
                _cmbInsumo.Items.Add(ins.Nombre);
            }
            _cmbInsumo.SelectedIndex = 0;
            _cmbInsumo.SelectedIndexChanged += CmbInsumo_SelectedIndexChanged;

            Label lblLote = CrearEtiquetaCampo("Lote PEPS *", 320, 78);
            _cmbLote = new ComboBox
            {
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 102),
                Height = 36,
                Enabled = false
            };
            _cmbLote.Items.Add("Selecciona primero un insumo...");
            _cmbLote.SelectedIndex = 0;
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
            _cmbMotivo.Items.AddRange(new object[]
            {
                "Seleccionar motivo...",
                "Caducidad vencida",
                "Derrame o accidente en barra",
                "Producto dañado en almacén",
                "Error de preparación en receta",
                "Mal estado / Calidad no óptima"
            });
            _cmbMotivo.SelectedIndex = 0;
            _cmbMotivo.SelectedIndexChanged += (s, e) => ActualizarImpactoInventario();

            // Fila 3: Observaciones / Justificación *
            Label lblObs = CrearEtiquetaCampo("Observaciones / Justificación *", 26, 244);
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

            // Caja resumen numérico
            Panel pnlResumen = new Panel
            {
                Name = "pnlResumen",
                Location = new Point(22, 74),
                Size = new Size(310, 142),
                BackColor = ColorTranslator.FromHtml("#FAF8F5")
            };
            pnlResumen.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlResumen.Width - 1, pnlResumen.Height - 1);
            };

            Label l1 = new Label { Text = "Existencia actual en lote:", Font = new Font("Segoe UI", 9F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(14, 16), AutoSize = true };
            _lblStockActualVal = new Label { Name = "lblStockActualVal", Text = "—", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, AutoSize = true };

            Label l2 = new Label { Text = "Cantidad a descontar:", Font = new Font("Segoe UI", 9F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(14, 48), AutoSize = true };
            _lblDescuentoVal = new Label { Name = "lblDescuentoVal", Text = "0.00", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#C62828"), AutoSize = true };

            Panel sep = new Panel { Name = "sepResumen", Location = new Point(14, 82), Height = 1, BackColor = TemaCafeteria.BordeSuave };

            Label l3 = new Label { Text = "Existencia resultante:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, Location = new Point(14, 98), AutoSize = true };
            _lblStockFinalVal = new Label { Name = "lblStockFinalVal", Text = "—", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TemaCafeteria.CafePrimario, AutoSize = true };

            pnlResumen.Controls.AddRange(new Control[] { l1, _lblStockActualVal, l2, _lblDescuentoVal, sep, l3, _lblStockFinalVal });

            // Tarjeta de regla de autorización (altura ampliada a 98 px para que no corte el texto)
            _pnlAlertaAutorizacion = new PanelRedondeado
            {
                Name = "pnlAlertaAutorizacion",
                Location = new Point(22, 232),
                Size = new Size(310, 98),
                RadioBorde = 10,
                BackColor = ColorTranslator.FromHtml("#EDF4F0")
            };

            _lblTextoAutorizacion = new Label
            {
                Text = "ℹ  Selecciona un lote y captura la cantidad para validar si requiere autorización de supervisor.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#264D3B"),
                Location = new Point(12, 12),
                Size = new Size(286, 74)
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

        private void CmbInsumo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _cmbLote.Items.Clear();
            _loteSeleccionado = null;

            if (_cmbInsumo.SelectedIndex <= 0)
            {
                _cmbLote.Items.Add("Selecciona primero un insumo...");
                _cmbLote.SelectedIndex = 0;
                _cmbLote.Enabled = false;
                _lblUnidadSufijo.Text = "kg";
                ActualizarImpactoInventario();
                return;
            }

            string insumoNombre = _cmbInsumo.SelectedItem?.ToString() ?? "";
            var lotesDelInsumo = _lotesDisponibles
                .Where(l => l.Insumo == insumoNombre && l.CantidadActual > 0)
                .OrderBy(l => l.FechaCaducidad)
                .ToList();

            if (lotesDelInsumo.Count == 0)
            {
                _cmbLote.Items.Add("Sin lotes con existencia");
                _cmbLote.SelectedIndex = 0;
                _cmbLote.Enabled = false;
            }
            else
            {
                _cmbLote.Enabled = true;
                foreach (var lote in lotesDelInsumo)
                {
                    _cmbLote.Items.Add($"{lote.CodigoLote} ({lote.CantidadActual:N2} {lote.Unidad} - Cad: {lote.FechaCaducidad:dd/MM/yy})");
                }
                _cmbLote.SelectedIndex = 0;
            }
        }

        private void CmbLote_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cmbInsumo.SelectedIndex <= 0 || !_cmbLote.Enabled || _cmbLote.SelectedIndex < 0)
            {
                _loteSeleccionado = null;
                ActualizarImpactoInventario();
                return;
            }

            string insumoNombre = _cmbInsumo.SelectedItem?.ToString() ?? "";
            var lotesDelInsumo = _lotesDisponibles
                .Where(l => l.Insumo == insumoNombre && l.CantidadActual > 0)
                .OrderBy(l => l.FechaCaducidad)
                .ToList();

            if (_cmbLote.SelectedIndex < lotesDelInsumo.Count)
            {
                _loteSeleccionado = lotesDelInsumo[_cmbLote.SelectedIndex];
                _lblUnidadSufijo.Text = _loteSeleccionado.Unidad;
            }

            ActualizarImpactoInventario();
        }

        private void ActualizarImpactoInventario()
        {
            if (_loteSeleccionado == null)
            {
                _lblStockActualVal.Text = "—";
                _lblDescuentoVal.Text = "0.00";
                _lblStockFinalVal.Text = "—";
                _lblSemaforoResultante.Text = "● Sin cambios";
                _lblSemaforoResultante.ForeColor = TemaCafeteria.TextoSecundario;
                _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#EDF4F0");
                _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#264D3B");
                _lblTextoAutorizacion.Text = "ℹ  Selecciona un lote y captura la cantidad para validar si requiere autorización de supervisor.";
                PosicionarValoresResumen();
                return;
            }

            string u = _loteSeleccionado.Unidad;
            _lblStockActualVal.Text = $"{_loteSeleccionado.CantidadActual:N2} {u}";

            decimal.TryParse(_txtCantidadMerma.Text, out decimal merma);
            if (merma < 0) merma = 0;

            _lblDescuentoVal.Text = $"- {merma:N2} {u}";
            decimal resultante = _loteSeleccionado.CantidadActual - merma;

            if (merma > _loteSeleccionado.CantidadActual)
            {
                _lblStockFinalVal.Text = "Excede lote";
                _lblStockFinalVal.ForeColor = ColorTranslator.FromHtml("#C62828");
                _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#FFEBEE");
                _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#C62828");
                _lblTextoAutorizacion.Text = "⚠  La cantidad mermada no puede ser mayor a la existencia disponible en el lote seleccionado.";
            }
            else
            {
                _lblStockFinalVal.Text = $"{resultante:N2} {u}";
                _lblStockFinalVal.ForeColor = TemaCafeteria.CafePrimario;

                if (resultante <= _loteSeleccionado.StockMinimo * 0.5m)
                {
                    _lblSemaforoResultante.Text = "■  Nivel Crítico";
                    _lblSemaforoResultante.ForeColor = ColorTranslator.FromHtml("#C62828");
                }
                else if (resultante <= _loteSeleccionado.StockMinimo)
                {
                    _lblSemaforoResultante.Text = "▲  Alerta Preventiva";
                    _lblSemaforoResultante.ForeColor = ColorTranslator.FromHtml("#B26A00");
                }
                else
                {
                    _lblSemaforoResultante.Text = "●  Estado Óptimo";
                    _lblSemaforoResultante.ForeColor = ColorTranslator.FromHtml("#1F4E35");
                }

                decimal porcentajeMerma = _loteSeleccionado.CantidadActual > 0 ? (merma / _loteSeleccionado.CantidadActual) : 0;
                if (porcentajeMerma > 0.20m)
                {
                    _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#FFF4E5");
                    _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#B26A00");
                    _lblTextoAutorizacion.Text = $"⚠  Merma mayor al 20% del lote ({porcentajeMerma:P0}). Quedará marcada para revisión del Administrador.";
                }
                else
                {
                    _pnlAlertaAutorizacion.BackColor = ColorTranslator.FromHtml("#EDF4F0");
                    _lblTextoAutorizacion.ForeColor = ColorTranslator.FromHtml("#264D3B");
                    _lblTextoAutorizacion.Text = "✓  Dentro del rango operativo permitido. El descuento se aplicará de inmediato al lote PEPS.";
                }
            }

            PosicionarValoresResumen();
        }

        private void PosicionarValoresResumen()
        {
            if (_cardImpacto.Controls["pnlResumen"] is Panel pnlRes)
            {
                _lblStockActualVal.Location = new Point(pnlRes.Width - _lblStockActualVal.Width - 14, 16);
                _lblDescuentoVal.Location = new Point(pnlRes.Width - _lblDescuentoVal.Width - 14, 48);
                _lblStockFinalVal.Location = new Point(pnlRes.Width - _lblStockFinalVal.Width - 14, 96);
            }
        }

        private void LimpiarFormulario()
        {
            _cmbInsumo.SelectedIndex = 0;
            _txtCantidadMerma.Clear();
            _cmbMotivo.SelectedIndex = 0;
            _txtObservaciones.Clear();
            _lblFechaReporteValor.Text = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
            ActualizarImpactoInventario();
        }

        private void BtnRegistrarMerma_Click(object? sender, EventArgs e)
        {
            if (_loteSeleccionado == null)
            {
                MessageBox.Show("Selecciona un insumo y un lote PEPS válido.", "Campos obligatorios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_txtCantidadMerma.Text, out decimal cantMerma) || cantMerma <= 0)
            {
                MessageBox.Show("Ingresa una cantidad mermada mayor a cero.", "Validación de cantidad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cantMerma > _loteSeleccionado.CantidadActual)
            {
                MessageBox.Show("La cantidad mermada no puede superar la existencia disponible del lote.", "Control PEPS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_cmbMotivo.SelectedIndex <= 0)
            {
                MessageBox.Show("Selecciona el motivo de la merma.", "Motivo obligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtObservaciones.Text))
            {
                MessageBox.Show("Escribe una breve observación o justificación de la incidencia.", "Auditoría de merma", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fechaRegistro = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
            _lblFechaReporteValor.Text = fechaRegistro;

            int idx = _lotesDisponibles.FindIndex(l => l.CodigoLote == _loteSeleccionado.CodigoLote);
            decimal nuevoStock = _loteSeleccionado.CantidadActual - cantMerma;
            if (idx >= 0)
            {
                _lotesDisponibles[idx] = _loteSeleccionado with { CantidadActual = nuevoStock };
            }

            _mermasRegistradasTurno++;
            _lblBadgeMermasTurno.Text = $"{_mermasRegistradasTurno} {(_mermasRegistradasTurno == 1 ? "merma" : "mermas")} en el turno";

            MessageBox.Show(
                $"Merma registrada correctamente.\n\n" +
                $"• Responsable: {_responsableTurno}\n" +
                $"• Fecha y hora: {fechaRegistro}\n" +
                $"• Lote afectado: {_loteSeleccionado.CodigoLote}\n" +
                $"• Insumo: {_loteSeleccionado.Insumo}\n" +
                $"• Cantidad descontada: {cantMerma:N2} {_loteSeleccionado.Unidad}\n" +
                $"• Nueva existencia en lote: {nuevoStock:N2} {_loteSeleccionado.Unidad}\n" +
                $"• Motivo: {_cmbMotivo.SelectedItem}",
                "Inventario Actualizado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

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
            int altoTarjetas = Math.Max(400, ClientSize.Height - yTarjetas - altoBarraAcciones - (separacion * 2));

            int anchoCardImpacto = Math.Clamp((int)(anchoUtil * 0.35), 320, 395);
            int anchoCardForm = anchoUtil - anchoCardImpacto - separacion;

            _cardFormulario.Bounds = new Rectangle(margenX, yTarjetas, anchoCardForm, altoTarjetas);
            _cardImpacto.Bounds = new Rectangle(margenX + anchoCardForm + separacion, yTarjetas, anchoCardImpacto, altoTarjetas);

            // Ajustar controles internos del formulario izquierdo
            int colGap = 22;
            int anchoColumna = (anchoCardForm - 52 - colGap) / 2;
            int xCol2 = 26 + anchoColumna + colGap;

            _cmbInsumo.Width = anchoColumna;
            _cmbLote.Left = xCol2;
            _cmbLote.Width = anchoColumna;

            foreach (Control c in _cardFormulario.Controls)
            {
                if (c is Label lbl && (lbl.Text.StartsWith("Lote PEPS") || lbl.Text.StartsWith("Motivo de merma")))
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

            // Ajustar controles internos de la tarjeta derecha
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

            // Barra inferior de acciones
            int yBarra = yTarjetas + altoTarjetas + separacion;
            _barraAcciones.Bounds = new Rectangle(margenX, yBarra, anchoUtil, altoBarraAcciones);
            _btnRegistrarMerma.Location = new Point(anchoUtil - _btnRegistrarMerma.Width - 16, (altoBarraAcciones - _btnRegistrarMerma.Height) / 2);
            _btnLimpiar.Location = new Point(_btnRegistrarMerma.Left - _btnLimpiar.Width - 12, (altoBarraAcciones - _btnLimpiar.Height) / 2);
        }
    }
}