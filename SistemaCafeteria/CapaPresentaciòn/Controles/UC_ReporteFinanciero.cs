using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CapaEntidades;
using CapaLogica;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Reportes;

namespace CapaPresentaciòn.Controles.Admin
{
    /// <summary>
    /// Pantalla 13: Reporte financiero (RF-10, RIU-06). Datos reales de MySQL a través de ReporteNegocio.
    /// El selector "Ver" muestra el resumen consolidado o el detalle de cada reporte (5.1–5.4).
    /// </summary>
    public partial class UC_ReporteFinanciero : UserControl
    {
        private const string VistaResumen = "Resumen consolidado";

        private static readonly (string Texto, SeccionReporte? Seccion)[] Vistas =
        {
            (VistaResumen, null),
            ("5.1 Ventas", SeccionReporte.Ventas),
            ("5.2 Consumo de insumos", SeccionReporte.Consumo),
            ("5.3 Existencias", SeccionReporte.Existencias),
            ("5.4 Mermas", SeccionReporte.Mermas)
        };

        private readonly ReporteNegocio _negocio = new();
        private ReporteFinanciero? _reporte;
        private TablaReporte? _tablaActual;

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;

        // Filtro de periodo (Fecha inicial, Fecha final, Generar)
        private PanelRedondeado _cardFiltroFechas = null!;
        private DateTimePicker _dtpFechaInicial = null!;
        private DateTimePicker _dtpFechaFinal = null!;
        private Button _btnGenerar = null!;
        private Label _lblEstadoPeriodo = null!;

        // 4 Tarjetas KPI
        private PanelRedondeado _cardKpiVentas = null!;
        private Label _lblKpiVentasVal = null!;
        private Label _lblKpiVentasSub = null!;
        private PanelRedondeado _cardKpiConsumo = null!;
        private Label _lblKpiConsumoVal = null!;
        private Label _lblKpiConsumoSub = null!;
        private PanelRedondeado _cardKpiExistencias = null!;
        private Label _lblKpiExistenciasVal = null!;
        private Label _lblKpiExistenciasSub = null!;
        private PanelRedondeado _cardKpiMermas = null!;
        private Label _lblKpiMermasVal = null!;
        private Label _lblKpiMermasSub = null!;

        // Tabla (resumen o detalle) + exportación a PDF
        private PanelRedondeado _cardTabla = null!;
        private Label _lblTablaTitulo = null!;
        private Label _lblVer = null!;
        private ComboBox _cmbVista = null!;
        private Button _btnExportarTodoPdf = null!;
        private DataGridView _dgvReporte = null!;
        private Label _lblPieTabla = null!;

        public UC_ReporteFinanciero()
        {
            ConfigurarInterfaz();

            Load += (s, e) =>
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
                    return;

                GenerarReporte(mostrarErrores: true);
            };
        }

        // =====================================================================
        // DATOS
        // =====================================================================

        private void GenerarReporte(bool mostrarErrores)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                var resultado = _negocio.GenerarReporte(_dtpFechaInicial.Value.Date, _dtpFechaFinal.Value.Date);
                if (!resultado.Exito || resultado.Reporte == null)
                {
                    if (mostrarErrores)
                        MessageBox.Show(resultado.Mensaje, "No se pudo generar el reporte", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _reporte = resultado.Reporte;
                MostrarIndicadores(_reporte);
                MostrarVistaSeleccionada();

                _lblEstadoPeriodo.Text = $"Periodo {_reporte.Desde:dd/MM/yyyy} – {_reporte.Hasta:dd/MM/yyyy} · generado {_reporte.GeneradoEn:HH:mm}";
                _btnExportarTodoPdf.Enabled = true;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void MostrarIndicadores(ReporteFinanciero r)
        {
            _lblKpiVentasVal.Text = TablasReporte.Dinero(r.VentasNetas);
            _lblKpiVentasSub.Text = r.VentasCanceladas > 0
                ? $"Excluye {r.VentasCanceladas} cancelada(s) por {TablasReporte.Dinero(r.ImporteCancelado)}"
                : "Excluye cancelaciones";

            _lblKpiConsumoVal.Text = TablasReporte.Dinero(r.ConsumoTotal);
            _lblKpiConsumoSub.Text = $"{r.InsumosConsumidos} insumos utilizados";

            _lblKpiExistenciasVal.Text = $"{r.InsumosConExistencia} insumos";
            _lblKpiExistenciasSub.Text = $"Valorizado en {TablasReporte.Dinero(r.ExistenciasValor)}";

            _lblKpiMermasVal.Text = TablasReporte.Dinero(r.MermasAutorizadasImporte);
            _lblKpiMermasSub.Text = r.MermasPendientes > 0
                ? $"{r.MermasAutorizadas} autorizadas · {r.MermasPendientes} pendientes"
                : $"{r.MermasAutorizadas} registros autorizados";
        }

        private void MostrarVistaSeleccionada()
        {
            if (_reporte == null)
                return;

            int indice = Math.Max(0, _cmbVista.SelectedIndex);
            var seccion = Vistas[indice].Seccion;
            var tabla = seccion.HasValue
                ? TablasReporte.Detalle(seccion.Value, _reporte)
                : TablasReporte.Resumen(_reporte);

            LlenarTabla(tabla, esResumen: !seccion.HasValue);
        }

        // =====================================================================
        // TABLA
        // =====================================================================

        private void LlenarTabla(TablaReporte tabla, bool esResumen)
        {
            _tablaActual = tabla;
            _lblTablaTitulo.Text = tabla.Titulo;

            _dgvReporte.SuspendLayout();
            _dgvReporte.Rows.Clear();
            _dgvReporte.Columns.Clear();

            for (int i = 0; i < tabla.Columnas.Count; i++)
            {
                var c = tabla.Columnas[i];
                var col = new DataGridViewTextBoxColumn
                {
                    Name = $"col{i}",
                    HeaderText = c.Titulo.ToUpperInvariant(),
                    FillWeight = c.Peso,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (c.AlinearDerecha)
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                _dgvReporte.Columns.Add(col);
            }

            if (esResumen)
            {
                _dgvReporte.Columns.Add(new DataGridViewLinkColumn
                {
                    Name = "colAccion",
                    HeaderText = "ACCIÓN",
                    Text = "Descargar PDF",
                    UseColumnTextForLinkValue = true,
                    LinkColor = ColorTranslator.FromHtml("#1E64D8"),
                    ActiveLinkColor = TemaCafeteria.CafePrimario,
                    TrackVisitedState = false,
                    FillWeight = 11,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            foreach (var fila in tabla.Filas)
                _dgvReporte.Rows.Add(fila);

            if (tabla.Filas.Count > 0 && tabla.Totales != null)
            {
                int idx = _dgvReporte.Rows.Add(tabla.Totales);
                var filaTotal = _dgvReporte.Rows[idx];
                filaTotal.Tag = "total";
                filaTotal.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#EFE6DC");
                filaTotal.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EFE6DC");
                filaTotal.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            }

            _dgvReporte.RowTemplate.Height = esResumen ? 50 : 36;
            foreach (DataGridViewRow r in _dgvReporte.Rows)
                r.Height = esResumen ? 50 : 36;

            _dgvReporte.ResumeLayout();
            _dgvReporte.ClearSelection();

            _lblPieTabla.Text = tabla.Filas.Count == 0
                ? "Sin registros en el periodo seleccionado."
                : esResumen
                    ? $"{tabla.Descripcion}   ·   Haz clic en una categoría para ver su detalle."
                    : $"{tabla.Descripcion}   ·   {tabla.Filas.Count} registro(s).";
        }

        // =====================================================================
        // EXPORTACIÓN A PDF (RIU-06)
        // =====================================================================

        private void ExportarReporteAPdf(SeccionReporte? soloSeccion)
        {
            if (_reporte == null)
            {
                MessageBox.Show("Primero genera el reporte del periodo.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string nombreSugerido = soloSeccion == null
                ? $"Reporte_Financiero_{_reporte.Desde:yyyyMMdd}_{_reporte.Hasta:yyyyMMdd}.pdf"
                : $"Reporte_{TablasReporte.NombreSeccion(soloSeccion.Value).Replace(" ", "_")}_{_reporte.Desde:yyyyMMdd}_{_reporte.Hasta:yyyyMMdd}.pdf";

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = nombreSugerido,
                Title = "Guardar reporte financiero en PDF"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var secciones = soloSeccion == null ? null : new List<SeccionReporte> { soloSeccion.Value };
                bool listo = GeneradorPdfReporte.Exportar(_reporte, sfd.FileName, secciones);
                Cursor = Cursors.Default;

                if (!listo)
                {
                    MessageBox.Show(
                        $"Windows todavía está escribiendo el archivo:\n{sfd.FileName}\n\nRevisa la carpeta en unos segundos.",
                        "Exportación en proceso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var resp = MessageBox.Show(
                    $"El reporte PDF se generó correctamente en:\n{sfd.FileName}\n\n¿Deseas abrir el documento ahora?",
                    "Exportación PDF exitosa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (resp == DialogResult.Yes && File.Exists(sfd.FileName))
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"No se pudo generar el PDF:\n{ex.Message}", "Error al exportar PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =====================================================================
        // INTERFAZ
        // =====================================================================

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
                Text = "Sucursal Centro / Reportes / Reporte financiero",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(32, 14),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Reporte Financiero y Analítico",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(30, 32),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Selecciona un periodo de tiempo para generar y exportar reportes de ventas, consumo, existencias y mermas (RF-10, RIU-06)",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(32, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });

            // 2. Tarjeta de selección de fechas
            _cardFiltroFechas = new PanelRedondeado
            {
                Size = new Size(540, 74),
                RadioBorde = 14,
                BackColor = Color.White
            };

            Label lblFecIni = new Label
            {
                Text = "FECHA INICIAL",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(20, 12),
                AutoSize = true
            };

            _dtpFechaInicial = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(20, 32),
                Width = 165,
                Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };

            Label lblFecFin = new Label
            {
                Text = "FECHA FINAL",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(205, 12),
                AutoSize = true
            };

            _dtpFechaFinal = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(205, 32),
                Width = 165,
                Value = DateTime.Today
            };

            _btnGenerar = new Button
            {
                Text = "Generar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1E64D8"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(390, 28),
                Size = new Size(125, 34),
                Cursor = Cursors.Hand
            };
            _btnGenerar.FlatAppearance.BorderSize = 0;
            _btnGenerar.Click += (s, e) =>
            {
                // RIU-06: rango inválido → mensaje de error y no se consulta
                if (_dtpFechaInicial.Value.Date > _dtpFechaFinal.Value.Date)
                {
                    MessageBox.Show("La fecha inicial no puede ser mayor a la fecha final.", "Rango inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GenerarReporte(mostrarErrores: true);
            };

            _cardFiltroFechas.Controls.AddRange(new Control[] { lblFecIni, _dtpFechaInicial, lblFecFin, _dtpFechaFinal, _btnGenerar });

            _lblEstadoPeriodo = new Label
            {
                Text = "Generando reporte…",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            // 3. Las 4 tarjetas KPI superiores
            _cardKpiVentas = CrearTarjetaKpi("Ventas netas", TemaCafeteria.TextoOscuro, ColorTranslator.FromHtml("#1F4E35"),
                out _lblKpiVentasVal, out _lblKpiVentasSub);
            _cardKpiConsumo = CrearTarjetaKpi("Consumo real", TemaCafeteria.TextoOscuro, TemaCafeteria.TextoSecundario,
                out _lblKpiConsumoVal, out _lblKpiConsumoSub);
            _cardKpiExistencias = CrearTarjetaKpi("Existencias al cierre", TemaCafeteria.TextoOscuro, TemaCafeteria.TextoSecundario,
                out _lblKpiExistenciasVal, out _lblKpiExistenciasSub);
            _cardKpiMermas = CrearTarjetaKpi("Mermas y pérdidas", ColorTranslator.FromHtml("#B26A00"), ColorTranslator.FromHtml("#B26A00"),
                out _lblKpiMermasVal, out _lblKpiMermasSub);

            // 4. Tarjeta inferior con tabla y exportación a PDF
            _cardTabla = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            _lblTablaTitulo = new Label
            {
                Text = "Detalle consolidado del periodo seleccionado",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(22, 20),
                AutoSize = true
            };

            _lblVer = new Label
            {
                Text = "Ver:",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            _cmbVista = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 215
            };
            foreach (var v in Vistas)
                _cmbVista.Items.Add(v.Texto);
            _cmbVista.SelectedIndex = 0;
            _cmbVista.SelectedIndexChanged += (s, e) => MostrarVistaSeleccionada();

            _btnExportarTodoPdf = new Button
            {
                Text = "Exportar reporte completo a PDF",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1F242D"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(255, 36),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnExportarTodoPdf.FlatAppearance.BorderSize = 0;
            _btnExportarTodoPdf.Click += (s, e) => ExportarReporteAPdf(soloSeccion: null);

            ConfigurarTablaReporte();

            _lblPieTabla = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = false,
                AutoEllipsis = true
            };

            _cardTabla.Controls.AddRange(new Control[] { _lblTablaTitulo, _lblVer, _cmbVista, _btnExportarTodoPdf, _dgvReporte, _lblPieTabla });

            Controls.AddRange(new Control[]
            {
                _cardFiltroFechas, _lblEstadoPeriodo,
                _cardKpiVentas, _cardKpiConsumo, _cardKpiExistencias, _cardKpiMermas,
                _cardTabla,
                _pnlTopBar
            });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private PanelRedondeado CrearTarjetaKpi(string titulo, Color colorValor, Color colorSubtexto, out Label lblValor, out Label lblSub)
        {
            PanelRedondeado card = new PanelRedondeado
            {
                RadioBorde = 14,
                BackColor = Color.White
            };

            Label lblTit = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(18, 16),
                AutoSize = true
            };

            lblValor = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI", 17.5F, FontStyle.Bold),
                ForeColor = colorValor,
                Location = new Point(16, 42),
                AutoSize = true
            };

            lblSub = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = colorSubtexto,
                Location = new Point(18, 82),
                AutoSize = true
            };

            card.Controls.AddRange(new Control[] { lblTit, lblValor, lblSub });
            return card;
        }

        private void ConfigurarTablaReporte()
        {
            _dgvReporte = new DataGridView
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
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false,
                GridColor = TemaCafeteria.BordeSuave,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _dgvReporte.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoSecundario,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0)
            };

            _dgvReporte.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#FAF6F1"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(8, 0, 8, 0)
            };

            _dgvReporte.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || _tablaActual == null || e.CellStyle == null)
                    return;
                if (_dgvReporte.Rows[e.RowIndex].Tag as string == "total")
                    return;

                if (e.ColumnIndex == 0)
                    e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);

                if (e.ColumnIndex == _tablaActual.ColumnaEstado)
                {
                    string est = e.Value?.ToString() ?? string.Empty;
                    e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    e.CellStyle.ForeColor = est switch
                    {
                        "Cancelada" or "Rechazada" or "Rojo" => ColorTranslator.FromHtml("#C62828"),
                        "Pendiente" or "Amarillo" or "Autorizado" => ColorTranslator.FromHtml("#B26A00"),
                        "Actualizado" => ColorTranslator.FromHtml("#1E64D8"),
                        "Blanco" => TemaCafeteria.TextoSecundario,
                        _ => ColorTranslator.FromHtml("#1F4E35")
                    };
                }
            };

            _dgvReporte.CellContentClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _cmbVista.SelectedIndex != 0)
                    return;

                if (_dgvReporte.Columns[e.ColumnIndex].Name == "colAccion" && e.RowIndex < Vistas.Length - 1)
                    ExportarReporteAPdf(Vistas[e.RowIndex + 1].Seccion);
            };

            // En el resumen, un clic en la fila abre el detalle de esa categoría
            _dgvReporte.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _cmbVista.SelectedIndex != 0 || e.RowIndex >= Vistas.Length - 1)
                    return;
                if (_dgvReporte.Columns[e.ColumnIndex].Name == "colAccion")
                    return;

                // Se aplaza: reconstruir la tabla dentro de su propio evento de clic provoca una llamada reentrante
                int vista = e.RowIndex + 1;
                BeginInvoke(new Action(() => _cmbVista.SelectedIndex = vista));
            };
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 32;
            int anchoUtil = Math.Max(820, ClientSize.Width - (margenX * 2));

            // 1. Tarjeta filtro de fechas + estado del periodo
            int yFiltro = _pnlTopBar.Height + 16;
            _cardFiltroFechas.Location = new Point(margenX, yFiltro);
            _lblEstadoPeriodo.Location = new Point(margenX + _cardFiltroFechas.Width + 18, yFiltro + 28);

            // 2. Fila de 4 tarjetas KPI
            int yKpi = yFiltro + _cardFiltroFechas.Height + 16;
            int altoKpi = 112;
            int gapKpi = 16;
            int anchoKpi = (anchoUtil - (gapKpi * 3)) / 4;

            _cardKpiVentas.Bounds = new Rectangle(margenX, yKpi, anchoKpi, altoKpi);
            _cardKpiConsumo.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi), yKpi, anchoKpi, altoKpi);
            _cardKpiExistencias.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi) * 2, yKpi, anchoKpi, altoKpi);
            _cardKpiMermas.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi) * 3, yKpi, anchoUtil - ((anchoKpi + gapKpi) * 3), altoKpi);

            // 3. Tarjeta inferior con la tabla
            int yTabla = yKpi + altoKpi + 18;
            int altoTablaCard = Math.Max(300, ClientSize.Height - yTabla - 22);
            _cardTabla.Bounds = new Rectangle(margenX, yTabla, anchoUtil, altoTablaCard);

            _btnExportarTodoPdf.Location = new Point(anchoUtil - _btnExportarTodoPdf.Width - 22, 14);
            _cmbVista.Location = new Point(_btnExportarTodoPdf.Left - _cmbVista.Width - 14, 19);
            _lblVer.Location = new Point(_cmbVista.Left - _lblVer.Width - 6, 22);

            int altoPie = 30;
            _dgvReporte.Bounds = new Rectangle(16, 62, anchoUtil - 32, altoTablaCard - 62 - altoPie - 10);
            _lblPieTabla.Bounds = new Rectangle(20, altoTablaCard - altoPie - 6, anchoUtil - 40, altoPie);
        }
    }
}
