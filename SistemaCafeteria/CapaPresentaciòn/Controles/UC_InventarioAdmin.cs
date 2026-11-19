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

namespace CapaPresentaciòn.Controles.Admin
{
    /// <summary>
    /// Gestión de inventario del Administrador (RF-03, RF-08, RIU-02, RIU-03): insumos con su
    /// semáforo, % de abasto y aviso de reabastecimiento; sus lotes en orden PEPS con recepción,
    /// cantidades, proveedor y el kardex real de cada lote.
    /// </summary>
    public partial class UC_InventarioAdmin : UserControl
    {
        private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
        private static readonly NivelSemaforo[] Niveles = { NivelSemaforo.Verde, NivelSemaforo.Amarillo, NivelSemaforo.Rojo, NivelSemaforo.Blanco };

        private sealed class FilaInventario
        {
            public ResumenInsumoInventario Resumen { get; init; } = null!;
            public Lote? Lote { get; init; }
            public int OrdenPeps { get; init; }   // 0 = lote agotado (ya no entra en el orden de salida)
        }

        private readonly InventarioNegocio _invNegocio = new();
        private readonly ConfiguracionNegocio _configNegocio = new();
        private readonly List<ResumenInsumoInventario> _resumenes = new();
        private ConfiguracionGeneral _config = new();

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;

        // 4 tarjetas KPI del semáforo
        private readonly Dictionary<NivelSemaforo, (PanelRedondeado Card, Label Conteo, Label Descripcion)> _tarjetas = new();

        // Barra de herramientas
        private Panel _pnlToolbar = null!;
        private TextBox _txtBuscar = null!;
        private ComboBox _cmbFiltroSemaforo = null!;
        private CheckBox _chkLotesAgotados = null!;
        private Button _btnNuevoLote = null!;

        // Tabla PEPS
        private PanelRedondeado _cardTabla = null!;
        private DataGridView _dgvInventario = null!;

        public UC_InventarioAdmin()
        {
            ConfigurarInterfaz();

            // Misma regla que las otras pantallas de inventario: carga al abrirse y recarga al volver
            RecargaAutomatica.AlMostrar(this, CargarDatos);
        }

        // =====================================================================
        // CARGA DESDE MYSQL
        // =====================================================================

        private void CargarDatos()
        {
            _resumenes.Clear();

            try
            {
                // Inserta proveedores, insumos y lotes semilla en MySQL solo si la tabla Insumo está vacía
                _invNegocio.AsegurarDatosInicialesInventario();

                _config = _configNegocio.ObtenerConfiguracionActual();
                // Insumos por urgencia (Blanco, Rojo, Amarillo, Verde) y sus lotes en orden PEPS
                _resumenes.AddRange(_invNegocio.ObtenerResumenInventarioCompleto(_chkLotesAgotados.Checked));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudieron cargar los lotes desde MySQL:\n{ex.Message}",
                    "Error de conexión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            ActualizarContadoresKpi();
            FiltrarTabla();
        }

        // =====================================================================
        // INTERFAZ
        // =====================================================================

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
                Text = "Insumos con semáforo y % de abasto; sus lotes en orden PEPS (recepción más antigua primero) con el historial de movimientos de cada lote.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(34, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });

            // 2. Tarjetas KPI del semáforo (clic = filtrar por ese estado)
            for (int i = 0; i < Niveles.Length; i++)
            {
                NivelSemaforo nivel = Niveles[i];
                int indiceCombo = i + 1;
                _tarjetas[nivel] = CrearTarjetaSemaforo(nivel, () => AlternarFiltroKpi(indiceCombo));
            }

            // 3. Barra de herramientas
            _pnlToolbar = new Panel { BackColor = Color.Transparent };

            _cmbFiltroSemaforo = new ComboBox
            {
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 6),
                Width = 210
            };
            _cmbFiltroSemaforo.Items.Add("Todos los estados");
            foreach (var nivel in Niveles)
                _cmbFiltroSemaforo.Items.Add($"● {nivel} · {SemaforoExistencias.Descripcion(nivel)}");
            _cmbFiltroSemaforo.SelectedIndex = 0;
            _cmbFiltroSemaforo.SelectedIndexChanged += (s, e) => FiltrarTabla();

            _chkLotesAgotados = new CheckBox
            {
                Text = "Incluir lotes agotados",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(226, 10),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _chkLotesAgotados.CheckedChanged += (s, e) => CargarDatos();

            var pnlBuscar = new Panel
            {
                Name = "pnlBuscar",
                Size = new Size(310, 38),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblLupa = new Label
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

            _pnlToolbar.Controls.AddRange(new Control[] { _cmbFiltroSemaforo, _chkLotesAgotados, pnlBuscar, _btnNuevoLote });

            // 4. Tarjeta con la tabla PEPS
            _cardTabla = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            ConfigurarTablaInventario();
            _cardTabla.Controls.Add(_dgvInventario);

            Controls.AddRange(_tarjetas.Values.Select(t => (Control)t.Card).ToArray());
            Controls.AddRange(new Control[] { _pnlToolbar, _cardTabla, _pnlTopBar });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private (PanelRedondeado Card, Label Conteo, Label Descripcion) CrearTarjetaSemaforo(NivelSemaforo nivel, Action onClick)
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

            var lblConteo = new Label
            {
                Text = "0 insumos",
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(14, 44),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            var lblDesc = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(16, 84),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            void EjecutarFiltro(object? s, EventArgs e) => onClick();
            card.Click += EjecutarFiltro;
            lblBadge.Click += EjecutarFiltro;
            lblConteo.Click += EjecutarFiltro;
            lblDesc.Click += EjecutarFiltro;

            card.Controls.AddRange(new Control[] { lblBadge, lblConteo, lblDesc });
            return (card, lblConteo, lblDesc);
        }

        private void AlternarFiltroKpi(int indiceEstado)
        {
            _cmbFiltroSemaforo.SelectedIndex = _cmbFiltroSemaforo.SelectedIndex == indiceEstado ? 0 : indiceEstado;
        }

        private void ActualizarContadoresKpi()
        {
            foreach (var (nivel, tarjeta) in _tarjetas)
            {
                int n = _resumenes.Count(r => r.Semaforo == nivel);
                tarjeta.Conteo.Text = $"{n} {(n == 1 ? "insumo" : "insumos")}";

                string rango = nivel switch
                {
                    NivelSemaforo.Verde => $"≥ {_config.VerdeMinimo:0.##} % del stock máximo",
                    NivelSemaforo.Amarillo => $"{_config.AmarilloMinimo:0.##} % – {_config.AmarilloMaximo:0.##} %",
                    NivelSemaforo.Rojo => $"≤ {_config.RojoMaximo:0.##} % (con existencia)",
                    _ => "Sin existencias (0)"
                };
                string? aviso = SemaforoExistencias.AvisoReabastecimiento(nivel);
                tarjeta.Descripcion.Text = aviso == null ? rango : $"{rango} · {aviso}";
            }
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
                ColumnHeadersHeight = 46,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowTemplate = { Height = 44 },
                EnableHeadersVisualStyles = false
                // Sin bordes de celda: las líneas por insumo/lote las dibuja TablaInventario.PintarSeparadores
            };

            _dgvInventario.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0),
                WrapMode = DataGridViewTriState.True
            };

            _dgvInventario.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#F9F4EE"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(6, 0, 6, 0)
            };

            AgregarColumna("colInsumo", "Insumo", 14);
            AgregarColumna("colTotal", "Existencia total", 10, true);
            AgregarColumna("colPorcentaje", "% abasto", 7, true);
            AgregarColumna("colSemaforo", "Semáforo", 9);
            AgregarColumna("colReabasto", "Reabastecimiento", 12);
            AgregarColumna("colLote", "Lote (orden PEPS)", 14);
            AgregarColumna("colFecha", "Fecha/Hora recepción", 12);
            AgregarColumna("colInicial", "Cant. inicial", 9, true);
            AgregarColumna("colDisponible", "Cant. disponible", 9, true);
            AgregarColumna("colProveedor", "Proveedor", 13);

            var colHistorial = new DataGridViewLinkColumn
            {
                Name = "colHistorial",
                HeaderText = "Historial de movimientos",
                LinkColor = TemaCafeteria.CafePrimario,
                ActiveLinkColor = TemaCafeteria.TextoOscuro,
                TrackVisitedState = false,
                FillWeight = 11,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _dgvInventario.Columns.Add(colHistorial);

            // Insumo, aviso de reabastecimiento y proveedor parten renglón en vez de cortarse con "..."
            TablaInventario.Configurar(_dgvInventario, "colInsumo", "colReabasto", "colProveedor");

            _dgvInventario.RowPostPaint += (s, e) =>
                TablaInventario.PintarSeparadores(_dgvInventario, e, MismoInsumo, "colLote");

            _dgvInventario.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvInventario.Columns["colSemaforo"]!.Index &&
                    _dgvInventario.Rows[e.RowIndex].Tag is FilaInventario fila && !string.IsNullOrEmpty(e.Value as string))
                {
                    SemaforoVisual.PintarCelda(e, fila.Resumen.Semaforo);
                }
            };

            _dgvInventario.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvInventario.Columns["colHistorial"]!.Index)
                    AbrirKardex(e.RowIndex);
            };
            _dgvInventario.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) AbrirKardex(e.RowIndex);
            };
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

        private void FiltrarTabla()
        {
            string busqueda = Busqueda.Normalizar(_txtBuscar.Text);
            int filtroSem = _cmbFiltroSemaforo.SelectedIndex;
            NivelSemaforo? filtro = filtroSem >= 1 ? Niveles[filtroSem - 1] : null;

            var filas = new List<FilaInventario>();
            foreach (var res in _resumenes)
            {
                if (filtro.HasValue && res.Semaforo != filtro.Value)
                    continue;

                bool coincideInsumo = Busqueda.Coincide(busqueda, res.Insumo.Nombre, res.Insumo.Categoria);

                if (res.LotesActivos.Count == 0)
                {
                    if (coincideInsumo) filas.Add(new FilaInventario { Resumen = res });
                    continue;
                }

                // Lotes ya en orden PEPS; el número indica el orden de salida de los que tienen existencia
                int orden = 0;
                foreach (var lote in res.LotesActivos)
                {
                    int ordenLote = lote.CantidadDisponible > 0 ? ++orden : 0;
                    bool coincide = coincideInsumo ||
                                    Busqueda.Coincide(busqueda, lote.CodigoLote, lote.CodigoBarras, lote.NombreProveedor);
                    if (coincide)
                        filas.Add(new FilaInventario { Resumen = res, Lote = lote, OrdenPeps = ordenLote });
                }
            }

            _dgvInventario.SuspendLayout();
            _dgvInventario.Rows.Clear();

            ResumenInsumoInventario? anterior = null;
            foreach (var fila in filas)
            {
                var res = fila.Resumen;
                var ins = res.Insumo;
                var lote = fila.Lote;
                string u = ins.UnidadAbreviatura;

                bool primeraDelInsumo = !ReferenceEquals(anterior, res);
                anterior = res;

                string textoLote = lote == null
                    ? "Sin lotes con existencia"
                    : fila.OrdenPeps > 0 ? $"{fila.OrdenPeps}. {lote.CodigoLote}" : $"{lote.CodigoLote} (agotado)";

                int idx = _dgvInventario.Rows.Add(
                    primeraDelInsumo ? ins.Nombre : "",
                    primeraDelInsumo ? $"{Cant(ins.StockActualTotal)} {u}" : "",
                    primeraDelInsumo ? $"{res.PorcentajeStock.ToString("0.0", Mx)} %" : "",
                    primeraDelInsumo ? res.EtiquetaEstado : "",
                    primeraDelInsumo ? (res.AvisoReabastecimiento ?? "—") : "",
                    textoLote,
                    lote?.FechaRecepcion.ToString("dd/MM/yyyy HH:mm", Mx) ?? "—",
                    lote == null ? "—" : $"{Cant(lote.CantidadInicial)} {u}",
                    lote == null ? "—" : $"{Cant(lote.CantidadDisponible)} {u}",
                    lote?.NombreProveedor ?? "—",
                    lote == null ? "" : "Ver historial");

                var row = _dgvInventario.Rows[idx];
                row.Tag = fila;

                if (primeraDelInsumo)
                {
                    row.Cells["colInsumo"].Style.Font = TablaInventario.FuenteInsumo;
                    var celdaAviso = row.Cells["colReabasto"];
                    if (res.AvisoReabastecimiento != null)
                    {
                        celdaAviso.Style.ForeColor = SemaforoVisual.Acento(res.Semaforo);
                        celdaAviso.Style.Font = TablaInventario.FuenteAviso;
                    }
                    else
                    {
                        celdaAviso.Style.ForeColor = TemaCafeteria.TextoSecundario;
                    }
                }

                if (lote == null || fila.OrdenPeps == 0)
                {
                    row.Cells["colLote"].Style.ForeColor = TemaCafeteria.TextoSecundario;
                    row.Cells["colDisponible"].Style.ForeColor = TemaCafeteria.TextoSecundario;
                }
                else if (fila.OrdenPeps == 1)
                {
                    row.Cells["colLote"].ToolTipText = "Siguiente lote en salir (PEPS: recepción más antigua)";
                }
            }

            TablaInventario.AjustarAnchosMinimos(_dgvInventario,
                new Dictionary<string, int> { ["colSemaforo"] = TablaInventario.AnchoCeldaSemaforo() });

            _dgvInventario.ResumeLayout();
        }

        /// <summary>True si las dos filas son lotes del mismo insumo (para dibujar el grupo).</summary>
        private bool MismoInsumo(int filaA, int filaB)
            => _dgvInventario.Rows[filaA].Tag is FilaInventario a
               && _dgvInventario.Rows[filaB].Tag is FilaInventario b
               && ReferenceEquals(a.Resumen, b.Resumen);

        private void AbrirKardex(int rowIndex)
        {
            if (_dgvInventario.Rows[rowIndex].Tag is FilaInventario { Lote: { } lote })
                FrmKardexLote.Mostrar(FindForm() ?? (IWin32Window)this, lote);
        }

        private static string Cant(decimal valor) => FormatoInventario.Numero(valor);

        /// <summary>
        /// RF-01: abre la misma pantalla de recepción de mercancía que usa el cajero, en una ventana
        /// modal. Al registrar un lote se recarga el inventario para verlo de inmediato.
        /// </summary>
        private void BtnNuevoLote_Click(object? sender, EventArgs e)
        {
            var permiso = Permisos.Verificar(Operacion.RegistrarRecepcion);
            if (!permiso.Exito)
            {
                MessageBox.Show(permiso.Mensaje, "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool huboCambios = false;
            using var ventana = new Form
            {
                Text = "Registrar lote · Recepción de mercancía",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1240, 800),
                MinimumSize = new Size(1000, 700),
                ShowInTaskbar = false,
                MinimizeBox = false,
                BackColor = TemaCafeteria.FondoCrema
            };

            var recepcion = new UC_RecepcionMercancia
            {
                Dock = DockStyle.Fill,
                Ruta = "ADMINISTRACIÓN / INVENTARIO / REGISTRAR LOTE"
            };
            recepcion.RecepcionRegistrada += (s, ev) => huboCambios = true;
            ventana.Controls.Add(recepcion);
            ventana.ShowDialog(FindForm());

            if (huboCambios)
                CargarDatos();
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 32;
            int anchoUtil = Math.Max(760, ClientSize.Width - (margenX * 2));

            // 1. Fila de 4 tarjetas KPI (Verde, Amarillo, Rojo, Blanco)
            int yKpi = _pnlTopBar.Height + 16;
            int altoKpi = 120;   // cabe la descripción en dos renglones
            int gapKpi = 14;
            int anchoKpi = (anchoUtil - (gapKpi * 3)) / 4;

            for (int i = 0; i < Niveles.Length; i++)
            {
                int x = margenX + i * (anchoKpi + gapKpi);
                int ancho = i == Niveles.Length - 1 ? margenX + anchoUtil - x : anchoKpi;
                _tarjetas[Niveles[i]].Card.Bounds = new Rectangle(x, yKpi, ancho, altoKpi);
                // La descripción ("≥ 50 % del stock máximo · ...") parte renglón dentro de la tarjeta
                _tarjetas[Niveles[i]].Descripcion.MaximumSize = new Size(Math.Max(60, ancho - 34), 0);
            }

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
