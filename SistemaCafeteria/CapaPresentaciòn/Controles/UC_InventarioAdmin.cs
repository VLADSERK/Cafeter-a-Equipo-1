using CapaLogica;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Estilos;
using System;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_InventarioAdmin : UserControl
    {
        private record LoteAdminViewModel(
            string Insumo,
            string Categoria,
            string CodigoLote,
            DateTime FechaRecepcion,
            DateTime FechaCaducidad,
            decimal CantidadInicial,
            decimal CantidadDisponible,
            decimal StockMinimo,
            string Unidad,
            string Proveedor
        );

        private readonly List<LoteAdminViewModel> _lotesAdmin = new();

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;

        // 3 Tarjetas KPI del semáforo (igual que en el inventario operativo)
        private PanelRedondeado _cardOptimo = null!;
        private Label _lblConteoOptimo = null!;
        private PanelRedondeado _cardPreventivo = null!;
        private Label _lblConteoPreventivo = null!;
        private PanelRedondeado _cardCritico = null!;
        private Label _lblConteoCritico = null!;

        // Barra de herramientas (Filtro + Buscador + Nuevo Lote)
        private Panel _pnlToolbar = null!;
        private TextBox _txtBuscar = null!;
        private ComboBox _cmbFiltroSemaforo = null!;
        private Button _btnNuevoLote = null!;

        // Contenedor de tabla PEPS
        private PanelRedondeado _cardTabla = null!;
        private DataGridView _dgvInventario = null!;

        public UC_InventarioAdmin()
        {
         
            CargarDatosIniciales();
            ConfigurarInterfaz();
            ActualizarContadoresKpi();
            FiltrarTabla();
        }

        private void CargarDatosIniciales()
        {
            _lotesAdmin.Clear();

            try
            {
                var invNegocio = new InventarioNegocio();

                // Inserta proveedores, insumos y lotes semilla en MySQL solo si la tabla Insumo está vacía
                invNegocio.AsegurarDatosInicialesInventario();

                // Obtiene todos los lotes activos ordenados por criterio PEPS (caducidad más próxima primero)
                var lotesMysql = invNegocio.ObtenerLotesPeps(soloDisponibles: true);

                foreach (var l in lotesMysql)
                {
                    // Como la tabla Insumo maneja StockMaximo, calculamos un umbral mínimo referencial (20%) para tu ViewModel
                    decimal stockMinimoRef = Math.Round(l.StockMaximoInsumo * 0.20m, 2);
                    DateTime fechaCad = l.FechaCaducidad ?? DateTime.Today.AddYears(1);

                    _lotesAdmin.Add(new LoteAdminViewModel(
                        l.NombreInsumo,
                        "Insumos Almacén",
                        l.CodigoLote,
                        l.FechaRecepcion,
                        fechaCad,
                        l.CantidadInicial,
                        l.CantidadDisponible,
                        stockMinimoRef,
                        l.UnidadAbreviatura,
                        l.NombreProveedor
                    ));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudieron cargar los lotes desde MySQL:\n{ex.Message}",
                    "Error de conexión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Encabezado superior
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
                Text = "ADMINISTRACIÓN / CONTROL DE EXISTENCIAS PEPS",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(34, 14),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Gestión de Inventario",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(32, 32),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Tabla de inventario desglosada por lotes PEPS con detalles de recepción, cantidad disponible, proveedor e historial de movimientos.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(34, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });

            // 2. Tarjetas KPI superiores del semáforo
            _cardOptimo = CrearTarjetaSemaforo(
                "●  ESTADO ÓPTIMO",
                "Stock saludable y vigencia amplia",
                ColorTranslator.FromHtml("#1F4E35"),
                ColorTranslator.FromHtml("#EAF3EE"),
                out _lblConteoOptimo,
                () => AlternarFiltroKpi(1)
            );

            _cardPreventivo = CrearTarjetaSemaforo(
                "▲  ALERTA PREVENTIVA",
                "Stock bajo o próximo a caducar (≤ 7 días)",
                ColorTranslator.FromHtml("#B26A00"),
                ColorTranslator.FromHtml("#FFF4E5"),
                out _lblConteoPreventivo,
                () => AlternarFiltroKpi(2)
            );

            _cardCritico = CrearTarjetaSemaforo(
                "■  NIVEL CRÍTICO",
                "Requiere reabastecimiento inmediato",
                ColorTranslator.FromHtml("#C62828"),
                ColorTranslator.FromHtml("#FFEBEE"),
                out _lblConteoCritico,
                () => AlternarFiltroKpi(3)
            );

            // 3. Barra de herramientas (Filtro semáforo, Buscador y Botón Añadir Lote)
            _pnlToolbar = new Panel { BackColor = Color.Transparent };

            _cmbFiltroSemaforo = new ComboBox
            {
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 6),
                Width = 220
            };
            _cmbFiltroSemaforo.Items.AddRange(new object[]
            {
                "Todos los estados",
                "● Óptimo (Verde)",
                "▲ Preventivo (Amarillo)",
                "■ Crítico (Rojo)"
            });
            _cmbFiltroSemaforo.SelectedIndex = 0;
            _cmbFiltroSemaforo.SelectedIndexChanged += (s, e) => FiltrarTabla();

            Panel pnlBuscar = new Panel
            {
                Name = "pnlBuscar",
                Size = new Size(310, 38),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblLupa = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 10F),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(8, 8),
                AutoSize = true
            };

            _txtBuscar = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                PlaceholderText = "Buscar por insumo, lote o proveedor...",
                Location = new Point(34, 8),
                Width = 264
            };
            _txtBuscar.TextChanged += (s, e) => FiltrarTabla();
            pnlBuscar.Controls.AddRange(new Control[] { lblLupa, _txtBuscar });

            _btnNuevoLote = new Button
            {
                Text = "+ Registrar Lote",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(165, 38),
                Cursor = Cursors.Hand
            };
            _btnNuevoLote.FlatAppearance.BorderSize = 0;
            _btnNuevoLote.Click += BtnNuevoLote_Click;

            _pnlToolbar.Controls.AddRange(new Control[] { _cmbFiltroSemaforo, pnlBuscar, _btnNuevoLote });

            // 4. Tarjeta con DataGridView PEPS
            _cardTabla = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            ConfigurarTablaInventario();
            _cardTabla.Controls.Add(_dgvInventario);

            Controls.AddRange(new Control[]
            {
                _cardOptimo, _cardPreventivo, _cardCritico,
                _pnlToolbar, _cardTabla, _pnlTopBar
            });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private PanelRedondeado CrearTarjetaSemaforo(
            string textoBadge,
            string subtexto,
            Color colorAcento,
            Color fondoBadge,
            out Label lblConteo,
            Action onClick)
        {
            PanelRedondeado card = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };

            Label lblBadge = new Label
            {
                Text = textoBadge,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = colorAcento,
                BackColor = fondoBadge,
                Location = new Point(18, 14),
                Padding = new Padding(8, 3, 8, 3),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            lblConteo = new Label
            {
                Text = "0 lotes",
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(16, 44),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            Label lblDesc = new Label
            {
                Text = subtexto,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(18, 84), // Separación limpia para que no se corte con el número grande
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            void EjecutarFiltro(object? s, EventArgs e) => onClick();
            card.Click += EjecutarFiltro;
            lblBadge.Click += EjecutarFiltro;
            lblConteo.Click += EjecutarFiltro;
            lblDesc.Click += EjecutarFiltro;

            card.Controls.AddRange(new Control[] { lblBadge, lblConteo, lblDesc });
            return card;
        }

        private void AlternarFiltroKpi(int indiceEstado)
        {
            _cmbFiltroSemaforo.SelectedIndex = _cmbFiltroSemaforo.SelectedIndex == indiceEstado ? 0 : indiceEstado;
        }

        private void ActualizarContadoresKpi()
        {
            int optimos = 0, preventivos = 0, criticos = 0;

            foreach (var l in _lotesAdmin)
            {
                string estado = ObtenerEstadoSemaforo(l);
                if (estado == "VERDE") optimos++;
                else if (estado == "AMARILLO") preventivos++;
                else criticos++;
            }

            _lblConteoOptimo.Text = $"{optimos} {(optimos == 1 ? "lote" : "lotes")}";
            _lblConteoPreventivo.Text = $"{preventivos} {(preventivos == 1 ? "lote" : "lotes")}";
            _lblConteoCritico.Text = $"{criticos} {(criticos == 1 ? "lote" : "lotes")}";
        }

        private void ConfigurarTablaInventario()
        {
            _dgvInventario = new DataGridView
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
                ColumnHeadersHeight = 44,
                RowTemplate = { Height = 46 },
                EnableHeadersVisualStyles = false,
                GridColor = TemaCafeteria.BordeSuave,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _dgvInventario.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0)
            };

            _dgvInventario.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#F9F4EE"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(10, 0, 10, 0)
            };

            _dgvInventario.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInsumo", HeaderText = "Insumo", FillWeight = 18 });
            _dgvInventario.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLote", HeaderText = "Lote", FillWeight = 13 });
            _dgvInventario.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFecha", HeaderText = "Fecha/Hora Recepción", FillWeight = 16 });
            _dgvInventario.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInicial", HeaderText = "Cantidad Inicial", FillWeight = 12 });
            _dgvInventario.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDisponible", HeaderText = "Cantidad Disponible", FillWeight = 13 });
            _dgvInventario.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProveedor", HeaderText = "Proveedor", FillWeight = 16 });

            var colHistorial = new DataGridViewLinkColumn
            {
                Name = "colHistorial",
                HeaderText = "Historial de Movimientos",
                Text = "Ver Historial",
                UseColumnTextForLinkValue = true,
                LinkColor = TemaCafeteria.CafePrimario,
                ActiveLinkColor = TemaCafeteria.TextoOscuro,
                TrackVisitedState = false,
                FillWeight = 15
            };
            _dgvInventario.Columns.Add(colHistorial);

            var colSemaforo = new DataGridViewTextBoxColumn
            {
                Name = "colSemaforo",
                HeaderText = "Semáforo",
                FillWeight = 9
            };
            colSemaforo.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colSemaforo.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvInventario.Columns.Add(colSemaforo);

            _dgvInventario.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvInventario.Columns["colSemaforo"]!.Index)
                {
                    e.PaintBackground(e.ClipBounds, true);
                    string estado = e.Value?.ToString() ?? "VERDE";

                    Color colorCirculo = estado switch
                    {
                        "ROJO" => ColorTranslator.FromHtml("#D32F2F"),
                        "AMARILLO" => ColorTranslator.FromHtml("#FBC02D"),
                        _ => ColorTranslator.FromHtml("#2E7D32")
                    };

                    int diametro = 22;
                    int x = e.CellBounds.X + (e.CellBounds.Width - diametro) / 2;
                    int y = e.CellBounds.Y + (e.CellBounds.Height - diametro) / 2;

                    e.Graphics!.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var pen = new Pen(colorCirculo, 3F);
                    e.Graphics.DrawEllipse(pen, x, y, diametro, diametro);

                    e.Handled = true;
                }
            };

            _dgvInventario.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvInventario.Columns["colHistorial"]!.Index)
                {
                    string codigoLote = _dgvInventario.Rows[e.RowIndex].Cells["colLote"].Value?.ToString() ?? "";
                    var lote = _lotesAdmin.FirstOrDefault(x => x.CodigoLote == codigoLote);
                    if (lote != null)
                    {
                        MostrarDetalleHistorialLote(lote);
                    }
                }
            };
        }

        private void FiltrarTabla()
        {
            string busqueda = _txtBuscar.Text.Trim().ToLowerInvariant();
            int filtroSem = _cmbFiltroSemaforo.SelectedIndex;

            _dgvInventario.Rows.Clear();

            foreach (var lote in _lotesAdmin.OrderBy(l => l.Insumo).ThenBy(l => l.FechaCaducidad))
            {
                string estadoSem = ObtenerEstadoSemaforo(lote);

                if (filtroSem == 1 && estadoSem != "VERDE") continue;
                if (filtroSem == 2 && estadoSem != "AMARILLO") continue;
                if (filtroSem == 3 && estadoSem != "ROJO") continue;

                if (!string.IsNullOrEmpty(busqueda))
                {
                    bool coincide = lote.Insumo.ToLowerInvariant().Contains(busqueda) ||
                                    lote.CodigoLote.ToLowerInvariant().Contains(busqueda) ||
                                    lote.Proveedor.ToLowerInvariant().Contains(busqueda);
                    if (!coincide) continue;
                }

                _dgvInventario.Rows.Add(
                    lote.Insumo,
                    lote.CodigoLote,
                    lote.FechaRecepcion.ToString("yyyy-MM-dd HH:mm"),
                    $"{lote.CantidadInicial:N2} {lote.Unidad}",
                    $"{lote.CantidadDisponible:N2} {lote.Unidad}",
                    lote.Proveedor,
                    "Ver Historial",
                    estadoSem
                );
            }
        }

        private static string ObtenerEstadoSemaforo(LoteAdminViewModel lote)
        {
            if (lote.CantidadDisponible <= lote.StockMinimo * 0.5m) return "ROJO";
            if (lote.CantidadDisponible <= lote.StockMinimo) return "AMARILLO";
            return "VERDE";
        }

        private void MostrarDetalleHistorialLote(LoteAdminViewModel lote)
        {
            decimal consumido = lote.CantidadInicial - lote.CantidadDisponible;
            MessageBox.Show(
                $"HISTORIAL DE MOVIMIENTOS — {lote.CodigoLote}\n" +
                $"Insumo: {lote.Insumo} | Proveedor: {lote.Proveedor}\n" +
                $"Caducidad: {lote.FechaCaducidad:dd/MM/yyyy}\n" +
                $"--------------------------------------------------------\n" +
                $"1. [{lote.FechaRecepcion:dd/MM/yyyy HH:mm}] ENTRADA POR RECEPCIÓN: +{lote.CantidadInicial:N2} {lote.Unidad}\n" +
                $"2. [{DateTime.Now.AddDays(-2):dd/MM/yyyy 14:20}] SALIDA POR VENTAS POS: -{consumido:N2} {lote.Unidad}\n" +
                $"--------------------------------------------------------\n" +
                $"Existencia disponible actual: {lote.CantidadDisponible:N2} {lote.Unidad}",
                $"Trazabilidad PEPS ({lote.CodigoLote})",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void BtnNuevoLote_Click(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "Desde este acceso el Administrador puede dar de alta un nuevo insumo maestro o registrar un lote directo con asignación de proveedor y stock mínimo.",
                "Alta Rápida de Lote / Insumo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 32;
            int anchoUtil = Math.Max(760, ClientSize.Width - (margenX * 2));

            // 1. Fila de 3 tarjetas KPI superiores (114 px de alto)
            int yKpi = _pnlTopBar.Height + 16;
            int altoKpi = 114;
            int gapKpi = 18;
            int anchoKpi = (anchoUtil - (gapKpi * 2)) / 3;

            _cardOptimo.Bounds = new Rectangle(margenX, yKpi, anchoKpi, altoKpi);
            _cardPreventivo.Bounds = new Rectangle(margenX + anchoKpi + gapKpi, yKpi, anchoKpi, altoKpi);
            _cardCritico.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi) * 2, yKpi, anchoUtil - ((anchoKpi + gapKpi) * 2), altoKpi);

            // 2. Barra de filtros y búsqueda
            int topToolbar = yKpi + altoKpi + 14;
            _pnlToolbar.Bounds = new Rectangle(margenX, topToolbar, anchoUtil, 46);
            _btnNuevoLote.Location = new Point(anchoUtil - _btnNuevoLote.Width, 4);

            if (_pnlToolbar.Controls["pnlBuscar"] is Panel pnlB)
            {
                pnlB.Location = new Point(_btnNuevoLote.Left - pnlB.Width - 14, 4);
            }

            // 3. Tabla PEPS
            int yTabla = topToolbar + 52;
            int altoTabla = Math.Max(280, ClientSize.Height - yTabla - 22);
            _cardTabla.Bounds = new Rectangle(margenX, yTabla, anchoUtil, altoTabla);
            _dgvInventario.Bounds = new Rectangle(12, 12, anchoUtil - 24, altoTabla - 24);
        }
    }
}