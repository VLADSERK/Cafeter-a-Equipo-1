using CapaEntidades;
using CapaLogica;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Formularios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace CapaPresentaciòn.Controles
{
    /// <summary>
    /// Consulta de inventario y semáforo para el cajero/barista (RF-03, RF-08, RIU-02, RIU-03).
    /// Cada insumo muestra su existencia total, % de abasto, estado (Verde/Amarillo/Rojo/Blanco)
    /// y aviso de reabastecimiento; debajo, sus lotes en orden PEPS con acceso al kardex.
    /// </summary>
    public partial class UC_Inventario : UserControl
    {
        private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

        private readonly InventarioNegocio _invNegocio = new();
        private readonly ConfiguracionNegocio _configNegocio = new();

        /// <summary>Una fila de la tabla: un lote (o el insumo sin lotes cuando está en Blanco).</summary>
        private sealed class FilaInventario
        {
            public ResumenInsumoInventario Resumen { get; init; } = null!;
            public Lote? Lote { get; init; }
            public int OrdenPeps { get; init; }   // 1 = el siguiente lote que se consumirá
        }

        private readonly List<ResumenInsumoInventario> _resumenes = new();
        private ConfiguracionGeneral _config = new();
        private NivelSemaforo? _filtroEstado;     // null = Todos

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblBadgeTotalLotes = null!;

        // Tarjetas KPI del semáforo (una por estado)
        private Panel _pnlKpis = null!;
        private readonly Dictionary<NivelSemaforo, (PanelRedondeado Card, Label Contador, Label Descripcion)> _tarjetas = new();

        // Barra de búsqueda y filtros
        private PanelRedondeado _pnlFiltros = null!;
        private TextBox _txtBuscar = null!;
        private readonly List<(Button Boton, NivelSemaforo? Estado)> _botonesFiltro = new();

        // Tabla de existencias
        private PanelRedondeado _cardTabla = null!;
        private DataGridView _dgvInventario = null!;

        public UC_Inventario()
        {
            InitializeComponent();
            ConfigurarInterfazInventario();

            // Carga al abrirse y recarga al volver a mostrarse. Antes solo escuchaba VisibleChanged,
            // que no se dispara al incrustar una pantalla nueva: por eso la tabla aparecía vacía.
            RecargaAutomatica.AlMostrar(this, CargarInventarioDesdeMysql);
        }

        public void CargarInventarioDesdeMysql()
        {
            try
            {
                _config = _configNegocio.ObtenerConfiguracionActual();
                _resumenes.Clear();
                // Ya vienen ordenados por urgencia (Blanco, Rojo, Amarillo, Verde) y con los lotes en orden PEPS
                _resumenes.AddRange(_invNegocio.ObtenerResumenInventarioCompleto());

                ActualizarContadoresKpi();
                AplicarFiltrosYRenderizar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo cargar el inventario desde MySQL:\n{ex.Message}",
                    "Error de conexión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================================
        // CONSTRUCCIÓN DE LA INTERFAZ
        // =====================================================================

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
                Text = "0 lotes activos (PEPS)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#F4ECE4"),
                Padding = new Padding(12, 6, 12, 6),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblBadgeTotalLotes });

            ConstruirTarjetasKpi();
            ConstruirBarraFiltros();
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

            foreach (NivelSemaforo nivel in new[] { NivelSemaforo.Verde, NivelSemaforo.Amarillo, NivelSemaforo.Rojo, NivelSemaforo.Blanco })
            {
                var tarjeta = CrearTarjetaSemaforo(nivel);
                _tarjetas[nivel] = tarjeta;
                _pnlKpis.Controls.Add(tarjeta.Card);
            }
        }

        private (PanelRedondeado Card, Label Contador, Label Descripcion) CrearTarjetaSemaforo(NivelSemaforo nivel)
        {
            var card = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };

            var lblBadge = new Label
            {
                Text = $"●  {nivel.ToString().ToUpperInvariant()} · {SemaforoExistencias.Descripcion(nivel).ToUpperInvariant()}",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = SemaforoVisual.Acento(nivel),
                BackColor = SemaforoVisual.FondoSuave(nivel),
                Location = new Point(16, 14),
                Padding = new Padding(8, 3, 8, 3),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            var lblNumero = new Label
            {
                Text = "0 insumos",
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(16, 42),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            var lblDesc = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(18, 78),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            void Filtrar(object? s, EventArgs e) => CambiarFiltro(_filtroEstado == nivel ? null : nivel);
            card.Click += Filtrar;
            lblBadge.Click += Filtrar;
            lblNumero.Click += Filtrar;
            lblDesc.Click += Filtrar;

            card.Controls.AddRange(new Control[] { lblBadge, lblNumero, lblDesc });
            return (card, lblNumero, lblDesc);
        }

        private void ConstruirBarraFiltros()
        {
            _pnlFiltros = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White
            };

            var pnlCajaBuscar = new Panel
            {
                Name = "pnlCajaBuscar",
                Location = new Point(18, 13),
                Height = 38,
                BackColor = TemaCafeteria.FondoCrema,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblLupa = new Label
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
                PlaceholderText = "Buscar por insumo, categoría o código de lote...",
                Location = new Point(34, 8),
                Height = 22
            };
            _txtBuscar.TextChanged += (s, e) => AplicarFiltrosYRenderizar();
            pnlCajaBuscar.Controls.AddRange(new Control[] { lblLupa, _txtBuscar });
            _pnlFiltros.Controls.Add(pnlCajaBuscar);

            _botonesFiltro.Add((CrearBotonFiltro("Todos", null), null));
            foreach (NivelSemaforo nivel in new[] { NivelSemaforo.Verde, NivelSemaforo.Amarillo, NivelSemaforo.Rojo, NivelSemaforo.Blanco })
                _botonesFiltro.Add((CrearBotonFiltro($"● {nivel}", nivel), nivel));

            foreach (var (btn, _) in _botonesFiltro)
                _pnlFiltros.Controls.Add(btn);

            ActualizarEstiloBotonesFiltro();
        }

        private Button CrearBotonFiltro(string texto, NivelSemaforo? estado)
        {
            var btn = new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
            btn.Click += (s, e) => CambiarFiltro(estado);
            return btn;
        }

        private void CambiarFiltro(NivelSemaforo? estado)
        {
            _filtroEstado = estado;
            ActualizarEstiloBotonesFiltro();
            AplicarFiltrosYRenderizar();
        }

        private void ActualizarEstiloBotonesFiltro()
        {
            foreach (var (btn, estado) in _botonesFiltro)
            {
                bool activo = estado == _filtroEstado;
                btn.BackColor = activo ? TemaCafeteria.CafePrimario : Color.White;
                btn.ForeColor = activo
                    ? Color.White
                    : (estado.HasValue ? SemaforoVisual.Acento(estado.Value) : TemaCafeteria.TextoOscuro);
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
                RowTemplate = { Height = 42 }
            };

            _dgvInventario.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F7F3EE"),
                ForeColor = TemaCafeteria.TextoSecundario,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Padding = new Padding(6, 0, 6, 0),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.True
            };
            _dgvInventario.ColumnHeadersHeight = 44;
            _dgvInventario.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvInventario.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _dgvInventario.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#F4ECE4"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(6, 0, 6, 0)
            };

            AgregarColumna("colInsumo", "INSUMO", 17);
            AgregarColumna("colTotal", "EXISTENCIA TOTAL", 11, true);
            AgregarColumna("colPorcentaje", "% ABASTO", 8, true);
            AgregarColumna("colSemaforo", "SEMÁFORO", 10);
            AgregarColumna("colReabasto", "REABASTECIMIENTO", 14);
            AgregarColumna("colLote", "LOTE (ORDEN PEPS)", 16);
            AgregarColumna("colRecepcion", "RECEPCIÓN", 12);
            AgregarColumna("colRestante", "RESTANTE EN LOTE", 11, true);
            AgregarColumna("colCaducidad", "CADUCIDAD", 9);

            var colKardex = new DataGridViewLinkColumn
            {
                Name = "colKardex",
                HeaderText = "MOVIMIENTOS",
                FillWeight = 10,
                LinkColor = TemaCafeteria.CafePrimario,
                ActiveLinkColor = TemaCafeteria.TextoOscuro,
                TrackVisitedState = false,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _dgvInventario.Columns.Add(colKardex);

            _dgvInventario.CellPainting += DgvInventario_CellPainting;
            _dgvInventario.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvInventario.Columns["colKardex"]!.Index)
                    AbrirKardex(e.RowIndex);
            };
            _dgvInventario.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) AbrirKardex(e.RowIndex);
            };

            _cardTabla.Controls.Add(_dgvInventario);
        }

        private void AgregarColumna(string nombre, string titulo, float peso, bool derecha = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = nombre,
                HeaderText = titulo,
                FillWeight = peso,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (derecha)
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            _dgvInventario.Columns.Add(col);
        }

        // =====================================================================
        // DATOS EN PANTALLA
        // =====================================================================

        private void ActualizarContadoresKpi()
        {
            foreach (var (nivel, tarjeta) in _tarjetas)
            {
                int n = _resumenes.Count(r => r.Semaforo == nivel);
                tarjeta.Contador.Text = $"{n} {(n == 1 ? "insumo" : "insumos")}";
                tarjeta.Descripcion.Text = DescripcionUmbral(nivel);
            }

            int lotes = _resumenes.Sum(r => r.LotesActivos.Count(l => l.CantidadDisponible > 0));
            _lblBadgeTotalLotes.Text = $"{lotes} {(lotes == 1 ? "lote activo" : "lotes activos")} (PEPS) · {_resumenes.Count} insumos";
            _lblBadgeTotalLotes.Location = new Point(_pnlTopBar.Width - _lblBadgeTotalLotes.Width - 34, 26);
        }

        /// <summary>Rango del estado según la configuración vigente y su aviso de reabastecimiento.</summary>
        private string DescripcionUmbral(NivelSemaforo nivel)
        {
            string rango = nivel switch
            {
                NivelSemaforo.Verde => $"≥ {_config.VerdeMinimo:0.##} % del stock máximo",
                NivelSemaforo.Amarillo => $"{_config.AmarilloMinimo:0.##} % – {_config.AmarilloMaximo:0.##} %",
                NivelSemaforo.Rojo => $"≤ {_config.RojoMaximo:0.##} % (con existencia)",
                _ => "Sin existencias (0)"
            };
            string? aviso = SemaforoExistencias.AvisoReabastecimiento(nivel);
            return aviso == null ? rango : $"{rango} · {aviso}";
        }

        private void AplicarFiltrosYRenderizar()
        {
            string busqueda = Busqueda.Normalizar(_txtBuscar.Text);

            var filas = new List<FilaInventario>();
            foreach (var res in _resumenes)
            {
                if (_filtroEstado.HasValue && res.Semaforo != _filtroEstado.Value)
                    continue;

                bool coincideInsumo = Busqueda.Coincide(busqueda, res.Insumo.Nombre, res.Insumo.Categoria);

                // Lotes con existencia en orden PEPS (fecha y hora de recepción más antigua primero)
                var lotes = res.LotesActivos.Where(l => l.CantidadDisponible > 0).ToList();

                if (lotes.Count == 0)
                {
                    if (coincideInsumo) filas.Add(new FilaInventario { Resumen = res });
                    continue;
                }

                for (int i = 0; i < lotes.Count; i++)
                {
                    if (coincideInsumo || Busqueda.Coincide(busqueda, lotes[i].CodigoLote, lotes[i].CodigoBarras))
                        filas.Add(new FilaInventario { Resumen = res, Lote = lotes[i], OrdenPeps = i + 1 });
                }
            }

            _dgvInventario.SuspendLayout();
            _dgvInventario.Rows.Clear();

            ResumenInsumoInventario? anterior = null;
            foreach (var fila in filas)
            {
                var res = fila.Resumen;
                var ins = res.Insumo;
                string u = ins.UnidadAbreviatura;

                // Los datos del insumo se muestran solo en su primera fila para leer la tabla por grupos
                bool primeraDelInsumo = !ReferenceEquals(anterior, res);
                anterior = res;

                int idx = _dgvInventario.Rows.Add(
                    primeraDelInsumo ? ins.Nombre : "",
                    primeraDelInsumo ? $"{Cant(ins.StockActualTotal)} {u}" : "",
                    primeraDelInsumo ? $"{res.PorcentajeStock.ToString("0.0", Mx)} %" : "",
                    primeraDelInsumo ? res.EtiquetaEstado : "",
                    primeraDelInsumo ? (res.AvisoReabastecimiento ?? "—") : "",
                    fila.Lote == null ? "Sin lotes con existencia" : $"{fila.OrdenPeps}. {fila.Lote.CodigoLote}",
                    fila.Lote?.FechaRecepcion.ToString("dd/MM/yyyy HH:mm", Mx) ?? "—",
                    fila.Lote == null ? "—" : $"{Cant(fila.Lote.CantidadDisponible)} {u}",
                    fila.Lote == null ? "—" : (fila.Lote.FechaCaducidad?.ToString("dd/MM/yyyy", Mx) ?? "Sin caducidad"),
                    fila.Lote == null ? "" : "Ver kardex");

                var row = _dgvInventario.Rows[idx];
                row.Tag = fila;

                if (primeraDelInsumo)
                {
                    row.Cells["colInsumo"].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    if (res.AvisoReabastecimiento != null)
                    {
                        var c = row.Cells["colReabasto"];
                        c.Style.ForeColor = SemaforoVisual.Acento(res.Semaforo);
                        c.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    }
                    else
                    {
                        row.Cells["colReabasto"].Style.ForeColor = TemaCafeteria.TextoSecundario;
                    }
                }

                var celdaLote = row.Cells["colLote"];
                celdaLote.Style.Font = new Font("Consolas", 9.5F, FontStyle.Bold);
                if (fila.Lote == null)
                    celdaLote.Style.ForeColor = SemaforoVisual.Acento(NivelSemaforo.Blanco);
                else if (fila.OrdenPeps == 1)
                    celdaLote.ToolTipText = "Siguiente lote en salir (PEPS: recepción más antigua)";
            }

            _dgvInventario.ResumeLayout();
        }

        private void DgvInventario_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _dgvInventario.Columns["colSemaforo"]!.Index)
                return;

            if (_dgvInventario.Rows[e.RowIndex].Tag is FilaInventario fila && !string.IsNullOrEmpty(e.Value as string))
                SemaforoVisual.PintarCelda(e, fila.Resumen.Semaforo);
        }

        private void AbrirKardex(int rowIndex)
        {
            if (_dgvInventario.Rows[rowIndex].Tag is FilaInventario { Lote: { } lote })
                FrmKardexLote.Mostrar(FindForm() ?? (IWin32Window)this, lote);
        }

        private static string Cant(decimal valor) => FormatoInventario.Numero(valor);

        // =====================================================================
        // DISEÑO RESPONSIVO
        // =====================================================================

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            _lblBadgeTotalLotes.Location = new Point(_pnlTopBar.Width - _lblBadgeTotalLotes.Width - 34, 26);

            int margenX = Math.Max(28, (int)(ClientSize.Width * 0.03));
            int anchoUtil = Math.Max(780, ClientSize.Width - (margenX * 2));
            int yActual = _pnlTopBar.Height + 18;

            // 1. Fila de 4 tarjetas KPI (Verde, Amarillo, Rojo, Blanco)
            int altoKpi = 108;
            int gapKpi = 14;
            _pnlKpis.Bounds = new Rectangle(margenX, yActual, anchoUtil, altoKpi);

            var orden = new[] { NivelSemaforo.Verde, NivelSemaforo.Amarillo, NivelSemaforo.Rojo, NivelSemaforo.Blanco };
            int anchoCardKpi = (anchoUtil - (gapKpi * 3)) / 4;
            for (int i = 0; i < orden.Length; i++)
            {
                int x = i * (anchoCardKpi + gapKpi);
                int ancho = i == orden.Length - 1 ? anchoUtil - x : anchoCardKpi;
                _tarjetas[orden[i]].Card.Bounds = new Rectangle(x, 0, ancho, altoKpi);
            }

            yActual += altoKpi + 16;

            // 2. Barra de búsqueda y botones de filtro
            int altoFiltros = 64;
            _pnlFiltros.Bounds = new Rectangle(margenX, yActual, anchoUtil, altoFiltros);

            const int anchoBoton = 100, gapBoton = 8;
            int anchoBotones = (anchoBoton * _botonesFiltro.Count) + (gapBoton * (_botonesFiltro.Count - 1));
            int xBotones = anchoUtil - anchoBotones - 18;
            for (int i = 0; i < _botonesFiltro.Count; i++)
                _botonesFiltro[i].Boton.Location = new Point(xBotones + i * (anchoBoton + gapBoton), 14);

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
