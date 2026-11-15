using CapaPresentacion.Estilos;
using CapaPresentaciòn.Estilos;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_Inventario : UserControl
    {
       

        private readonly List<LoteInventarioMock> _lotesMock = new();
        private string _filtroEstado = "Todos";

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblBadgeTotalLotes = null!;

        // Tarjetas KPI del semáforo
        private Panel _pnlKpis = null!;
        private PanelRedondeado _cardOptimo = null!;
        private PanelRedondeado _cardPreventivo = null!;
        private PanelRedondeado _cardCritico = null!;
        private Label _lblCountOptimo = null!;
        private Label _lblCountPreventivo = null!;
        private Label _lblCountCritico = null!;

        // Barra de búsqueda y filtros
        private PanelRedondeado _pnlFiltros = null!;
        private TextBox _txtBuscar = null!;
        private Button _btnFiltroTodos = null!;
        private Button _btnFiltroOptimo = null!;
        private Button _btnFiltroPreventivo = null!;
        private Button _btnFiltroCritico = null!;

        // Tabla de existencias
        private PanelRedondeado _cardTabla = null!;
        private DataGridView _dgvInventario = null!;

        public UC_Inventario()
        {
            InitializeComponent();
            InicializarDatosMock();
            ConfigurarInterfazInventario();
            ActualizarContadoresKpi();
            AplicarFiltrosYRenderizar();
        }

     
           private void InicializarDatosMock()
        {
            _lotesMock.Clear();
            _lotesMock.AddRange(CatalogoInsumosMock.ObtenerLotesIniciales());
        }
        

        private void ConfigurarInterfazInventario()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra blanca superior
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
                Text = "Consulta de inventario y semáforo",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(34, 34),
                AutoSize = true
            };

            _lblBadgeTotalLotes = new Label
            {
                Text = "8 lotes activos (PEPS)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#F4ECE4"),
                Padding = new Padding(12, 6, 12, 6),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblBadgeTotalLotes });

            // 2. Tarjetas KPI superiores (Semáforo de existencias)
            ConstruirTarjetasKpi();

            // 3. Barra de búsqueda y botones de filtro
            ConstruirBarraFiltros();

            // 4. Tarjeta con DataGridView estilizado
            ConstruirTablaInventario();

            Controls.AddRange(new Control[]
            {
                _pnlKpis,
                _pnlFiltros,
                _cardTabla,
                _pnlTopBar
            });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private void ConstruirTarjetasKpi()
        {
            _pnlKpis = new Panel { BackColor = Color.Transparent };

            (_cardOptimo, _lblCountOptimo) = CrearTarjetaSemaforo(
                "●  ESTADO ÓPTIMO",
                "Stock saludable y vigencia amplia",
                ColorTranslator.FromHtml("#1F4E35"),
                ColorTranslator.FromHtml("#EAF3EE"));

            (_cardPreventivo, _lblCountPreventivo) = CrearTarjetaSemaforo(
                "▲  ALERTA PREVENTIVA",
                "Stock bajo o próximo a caducar (≤ 7 días)",
                ColorTranslator.FromHtml("#B26A00"),
                ColorTranslator.FromHtml("#FFF4E5"));

            (_cardCritico, _lblCountCritico) = CrearTarjetaSemaforo(
                "■  NIVEL CRÍTICO",
                "Requiere reabastecimiento inmediato",
                ColorTranslator.FromHtml("#C62828"),
                ColorTranslator.FromHtml("#FFEBEE"));

            _pnlKpis.Controls.AddRange(new Control[] { _cardOptimo, _cardPreventivo, _cardCritico });
        }

        private (PanelRedondeado card, Label lblContador) CrearTarjetaSemaforo(string titulo, string subtitulo, Color colorAcento, Color colorFondoBadge)
        {
            PanelRedondeado card = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White
            };

            Label lblBadge = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = colorAcento,
                BackColor = colorFondoBadge,
                Location = new Point(18, 16),
                Padding = new Padding(8, 3, 8, 3),
                AutoSize = true
            };

            Label lblNumero = new Label
            {
                Text = "0 lotes",
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(18, 46),
                AutoSize = true
            };

            Label lblDesc = new Label
            {
                Text = subtitulo,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(18, 78),
                AutoSize = true
            };

            card.Controls.AddRange(new Control[] { lblBadge, lblNumero, lblDesc });
            return (card, lblNumero);
        }

        private void ConstruirBarraFiltros()
        {
            _pnlFiltros = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White
            };

            Panel pnlCajaBuscar = new Panel
            {
                Name = "pnlCajaBuscar",
                Location = new Point(18, 13),
                Height = 38,
                BackColor = TemaCafeteria.FondoCrema,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblLupa = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 10F),
                Location = new Point(8, 7),
                AutoSize = true
            };

            _txtBuscar = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5F),
                BackColor = TemaCafeteria.FondoCrema,
                ForeColor = TemaCafeteria.TextoOscuro,
                PlaceholderText = "Buscar por insumo o código de lote...",
                Location = new Point(34, 8),
                Height = 22
            };
            _txtBuscar.TextChanged += (s, e) => AplicarFiltrosYRenderizar();
            pnlCajaBuscar.Controls.AddRange(new Control[] { lblLupa, _txtBuscar });

            _btnFiltroTodos = CrearBotonFiltro("Todos", "Todos");
            _btnFiltroOptimo = CrearBotonFiltro("🟢 Óptimo", "Optimo");
            _btnFiltroPreventivo = CrearBotonFiltro("🟡 Preventivo", "Preventivo");
            _btnFiltroCritico = CrearBotonFiltro("🔴 Crítico", "Critico");

            _pnlFiltros.Controls.AddRange(new Control[]
            {
                pnlCajaBuscar,
                _btnFiltroTodos, _btnFiltroOptimo, _btnFiltroPreventivo, _btnFiltroCritico
            });

            ActualizarEstiloBotonesFiltro();
        }

        private Button CrearBotonFiltro(string texto, string valorFiltro)
        {
            Button btn = new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(112, 36),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
            btn.Click += (s, e) =>
            {
                _filtroEstado = valorFiltro;
                ActualizarEstiloBotonesFiltro();
                AplicarFiltrosYRenderizar();
            };
            return btn;
        }

        private void ActualizarEstiloBotonesFiltro()
        {
            var mapa = new (Button btn, string estado)[]
            {
                (_btnFiltroTodos, "Todos"),
                (_btnFiltroOptimo, "Optimo"),
                (_btnFiltroPreventivo, "Preventivo"),
                (_btnFiltroCritico, "Critico")
            };

            foreach (var (btn, estado) in mapa)
            {
                bool activo = estado == _filtroEstado;
                btn.BackColor = activo ? TemaCafeteria.CafePrimario : Color.White;
                btn.ForeColor = activo ? Color.White : TemaCafeteria.TextoOscuro;
            }
        }

        private void ConstruirTablaInventario()
        {
            _cardTabla = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White,
                Padding = new Padding(18)
            };

            _dgvInventario = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = TemaCafeteria.BordeSuave,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                RowTemplate = { Height = 44 }
            };

            _dgvInventario.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F7F3EE"),
                ForeColor = TemaCafeteria.TextoSecundario,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Padding = new Padding(8, 0, 8, 0),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            _dgvInventario.ColumnHeadersHeight = 40;
            _dgvInventario.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _dgvInventario.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#F4ECE4"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(8, 0, 8, 0)
            };

            _dgvInventario.Columns.Add("colLote", "CÓDIGO DE LOTE (PEPS)");
            _dgvInventario.Columns.Add("colInsumo", "INSUMO");
            _dgvInventario.Columns.Add("colCategoria", "CATEGORÍA");
            _dgvInventario.Columns.Add("colExistencia", "EXISTENCIA ACTUAL");
            _dgvInventario.Columns.Add("colMinimo", "STOCK MÍNIMO");
            _dgvInventario.Columns.Add("colCaducidad", "CADUCIDAD");
            _dgvInventario.Columns.Add("colEstado", "SEMÁFORO");

            _dgvInventario.Columns["colLote"].FillWeight = 18;
            _dgvInventario.Columns["colInsumo"].FillWeight = 24;
            _dgvInventario.Columns["colCategoria"].FillWeight = 14;
            _dgvInventario.Columns["colExistencia"].FillWeight = 13;
            _dgvInventario.Columns["colMinimo"].FillWeight = 11;
            _dgvInventario.Columns["colCaducidad"].FillWeight = 12;
            _dgvInventario.Columns["colEstado"].FillWeight = 13;

            _dgvInventario.CellFormatting += DgvInventario_CellFormatting;

            _cardTabla.Controls.Add(_dgvInventario);
        }

        private void DgvInventario_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_dgvInventario.Columns[e.ColumnIndex].Name == "colEstado" && e.Value is string estadoTexto)
            {
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                if (estadoTexto.Contains("Óptimo"))
                    e.CellStyle.ForeColor = ColorTranslator.FromHtml("#1F4E35");
                else if (estadoTexto.Contains("Preventivo"))
                    e.CellStyle.ForeColor = ColorTranslator.FromHtml("#B26A00");
                else if (estadoTexto.Contains("Crítico"))
                    e.CellStyle.ForeColor = ColorTranslator.FromHtml("#C62828");
            }
            else if (_dgvInventario.Columns[e.ColumnIndex].Name == "colLote")
            {
                e.CellStyle.Font = new Font("Consolas", 9.5F, FontStyle.Bold);
            }
        }

        private void ActualizarContadoresKpi()
        {
            int optimos = _lotesMock.Count(l => l.EstadoSemaforo == "Optimo");
            int preventivos = _lotesMock.Count(l => l.EstadoSemaforo == "Preventivo");
            int criticos = _lotesMock.Count(l => l.EstadoSemaforo == "Critico");

            _lblCountOptimo.Text = $"{optimos} {(optimos == 1 ? "lote" : "lotes")}";
            _lblCountPreventivo.Text = $"{preventivos} {(preventivos == 1 ? "lote" : "lotes")}";
            _lblCountCritico.Text = $"{criticos} {(criticos == 1 ? "lote" : "lotes")}";
            _lblBadgeTotalLotes.Text = $"{_lotesMock.Count} lotes activos (PEPS)";
        }

        private void AplicarFiltrosYRenderizar()
        {
            string busqueda = _txtBuscar.Text.Trim().ToLowerInvariant();

            var filtrados = _lotesMock
                .Where(l => _filtroEstado == "Todos" || l.EstadoSemaforo == _filtroEstado)
                .Where(l => string.IsNullOrEmpty(busqueda) ||
                            l.Insumo.ToLowerInvariant().Contains(busqueda) ||
                            l.CodigoLote.ToLowerInvariant().Contains(busqueda) ||
                            l.Categoria.ToLowerInvariant().Contains(busqueda))
                .OrderBy(l => l.FechaCaducidad) // Ordenamiento PEPS (el más próximo a caducar primero)
                .ToList();

            _dgvInventario.Rows.Clear();
            foreach (var lote in filtrados)
            {
                string etiquetaSemaforo = lote.EstadoSemaforo switch
                {
                    "Optimo" => "●  Óptimo",
                    "Preventivo" => "▲  Preventivo",
                    _ => "■  Crítico"
                };

                _dgvInventario.Rows.Add(
                    lote.CodigoLote,
                    lote.Insumo,
                    lote.Categoria,
                    $"{lote.CantidadActual:N2} {lote.Unidad}",
                    $"{lote.StockMinimo:N2} {lote.Unidad}",
                    lote.FechaCaducidad.ToString("dd/MM/yyyy"),
                    etiquetaSemaforo
                );
            }
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            _lblBadgeTotalLotes.Location = new Point(_pnlTopBar.Width - _lblBadgeTotalLotes.Width - 34, 26);

            int margenX = Math.Max(28, (int)(ClientSize.Width * 0.03));
            int anchoUtil = Math.Max(780, ClientSize.Width - (margenX * 2));
            int yActual = _pnlTopBar.Height + 18;

            // 1. Fila de 3 tarjetas KPI
            int altoKpi = 108;
            int gapKpi = 16;
            _pnlKpis.Bounds = new Rectangle(margenX, yActual, anchoUtil, altoKpi);

            int anchoCardKpi = (anchoUtil - (gapKpi * 2)) / 3;
            _cardOptimo.Bounds = new Rectangle(0, 0, anchoCardKpi, altoKpi);
            _cardPreventivo.Bounds = new Rectangle(anchoCardKpi + gapKpi, 0, anchoCardKpi, altoKpi);
            _cardCritico.Bounds = new Rectangle((anchoCardKpi + gapKpi) * 2, 0, anchoUtil - ((anchoCardKpi + gapKpi) * 2), altoKpi);

            yActual += altoKpi + 16;

            // 2. Barra de búsqueda y botones de filtro
            int altoFiltros = 64;
            _pnlFiltros.Bounds = new Rectangle(margenX, yActual, anchoUtil, altoFiltros);

            int anchoBotones = (112 * 4) + (8 * 3);
            int xBotones = anchoUtil - anchoBotones - 18;
            _btnFiltroTodos.Location = new Point(xBotones, 14);
            _btnFiltroOptimo.Location = new Point(xBotones + 120, 14);
            _btnFiltroPreventivo.Location = new Point(xBotones + 240, 14);
            _btnFiltroCritico.Location = new Point(xBotones + 360, 14);

            if (_pnlFiltros.Controls["pnlCajaBuscar"] is Panel pnlBusq)
            {
                int anchoBusq = Math.Max(220, xBotones - 36);
                pnlBusq.Width = anchoBusq;
                _txtBuscar.Width = anchoBusq - 44;
            }

            yActual += altoFiltros + 16;

            // 3. Tabla principal de existencias
            int altoTabla = Math.Max(260, ClientSize.Height - yActual - 22);
            _cardTabla.Bounds = new Rectangle(margenX, yActual, anchoUtil, altoTabla);
        }
    }
}