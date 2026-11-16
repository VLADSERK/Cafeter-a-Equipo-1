using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_ReporteFinanciero : UserControl
    {
        private record FilaReporteFinanciero(
            string Categoria,
            string Descripcion,
            string VolumenUnidades,
            decimal CostoValorTotal,
            string Estado
        );

        private readonly List<FilaReporteFinanciero> _filasReporte = new();
        private readonly CultureInfo _culturaMxn = CultureInfo.GetCultureInfo("es-MX");

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

        // 4 Tarjetas KPI
        private PanelRedondeado _cardKpiVentas = null!;
        private Label _lblKpiVentasVal = null!;
        private PanelRedondeado _cardKpiConsumo = null!;
        private Label _lblKpiConsumoVal = null!;
        private PanelRedondeado _cardKpiExistencias = null!;
        private Label _lblKpiExistenciasVal = null!;
        private Label _lblKpiExistenciasSub = null!;
        private PanelRedondeado _cardKpiMermas = null!;
        private Label _lblKpiMermasVal = null!;

        // Tabla consolidada + botón Exportar reporte completo a PDF
        private PanelRedondeado _cardTabla = null!;
        private Label _lblTablaTitulo = null!;
        private Button _btnExportarTodoPdf = null!;
        private DataGridView _dgvReporte = null!;

        public UC_ReporteFinanciero()
        {
           
            ConfigurarInterfaz();
            CargarDatosPeriodo();
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

            // 2. Tarjeta de selección de fechas (FECHA INICIAL | FECHA FINAL | Generar)
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
                Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
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
                Value = DateTime.Now
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
                if (_dtpFechaInicial.Value.Date > _dtpFechaFinal.Value.Date)
                {
                    MessageBox.Show("La fecha inicial no puede ser mayor a la fecha final.", "Rango inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                CargarDatosPeriodo();
                MessageBox.Show(
                    $"Reporte actualizado para el periodo del {_dtpFechaInicial.Value:dd/MM/yyyy} al {_dtpFechaFinal.Value:dd/MM/yyyy}.",
                    "Periodo Generado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };

            _cardFiltroFechas.Controls.AddRange(new Control[] { lblFecIni, _dtpFechaInicial, lblFecFin, _dtpFechaFinal, _btnGenerar });

            // 3. Las 4 tarjetas KPI superiores
            _cardKpiVentas = CrearTarjetaKpi(
                "Ventas netas",
                "$45,820.00",
                "Excluye cancelaciones",
                TemaCafeteria.TextoOscuro,
                ColorTranslator.FromHtml("#1F4E35"),
                out _lblKpiVentasVal,
                out _
            );

            _cardKpiConsumo = CrearTarjetaKpi(
                "Consumo real",
                "$18,450.00",
                "Ingredientes utilizados",
                TemaCafeteria.TextoOscuro,
                TemaCafeteria.TextoSecundario,
                out _lblKpiConsumoVal,
                out _
            );

            _cardKpiExistencias = CrearTarjetaKpi(
                "Existencias actuales",
                "27 Insumos",
                "Valorizado en $89,120.00",
                TemaCafeteria.TextoOscuro,
                TemaCafeteria.TextoSecundario,
                out _lblKpiExistenciasVal,
                out _lblKpiExistenciasSub
            );

            _cardKpiMermas = CrearTarjetaKpi(
                "Mermas y pérdidas",
                "$2,418.00",
                "5 registros autorizados",
                ColorTranslator.FromHtml("#B26A00"),
                ColorTranslator.FromHtml("#B26A00"),
                out _lblKpiMermasVal,
                out _
            );

            // 4. Tarjeta inferior con tabla consolidada y botón Exportar reporte completo a PDF
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

            _btnExportarTodoPdf = new Button
            {
                Text = "Exportar a PDF",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1F242D"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(245, 38),
                Cursor = Cursors.Hand
            };
            _btnExportarTodoPdf.FlatAppearance.BorderSize = 0;
            _btnExportarTodoPdf.Click += (s, e) => ExportarReporteAPdf(soloUnaFila: null);

            ConfigurarTablaReporte();

            _cardTabla.Controls.AddRange(new Control[] { _lblTablaTitulo, _btnExportarTodoPdf, _dgvReporte });

            Controls.AddRange(new Control[]
            {
                _cardFiltroFechas,
                _cardKpiVentas, _cardKpiConsumo, _cardKpiExistencias, _cardKpiMermas,
                _cardTabla,
                _pnlTopBar
            });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private PanelRedondeado CrearTarjetaKpi(
            string titulo,
            string valorInicial,
            string subtexto,
            Color colorValor,
            Color colorSubtexto,
            out Label lblValor,
            out Label lblSub)
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
                Text = valorInicial,
                Font = new Font("Segoe UI", 17.5F, FontStyle.Bold),
                ForeColor = colorValor,
                Location = new Point(16, 42),
                AutoSize = true
            };

            lblSub = new Label
            {
                Text = subtexto,
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
                ColumnHeadersHeight = 44,
                RowTemplate = { Height = 54 },
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
                Padding = new Padding(12, 0, 12, 0)
            };

            _dgvReporte.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#FAF6F1"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(12, 0, 12, 0)
            };

            _dgvReporte.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCat", HeaderText = "CATEGORÍA DE REPORTE", FillWeight = 22 });
            _dgvReporte.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc", HeaderText = "DESCRIPCIÓN", FillWeight = 32 });
            _dgvReporte.Columns.Add(new DataGridViewTextBoxColumn { Name = "colVol", HeaderText = "VOLUMEN / UNIDADES", FillWeight = 16 });
            _dgvReporte.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "COSTO / VALOR TOTAL", FillWeight = 16 });
            _dgvReporte.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEstado", HeaderText = "ESTADO", FillWeight = 13 });

            var colAccion = new DataGridViewLinkColumn
            {
                Name = "colAccion",
                HeaderText = "ACCIÓN",
                Text = "Descargar PDF",
                UseColumnTextForLinkValue = true,
                LinkColor = ColorTranslator.FromHtml("#1E64D8"),
                ActiveLinkColor = TemaCafeteria.CafePrimario,
                TrackVisitedState = false,
                FillWeight = 14
            };
            _dgvReporte.Columns.Add(colAccion);

            _dgvReporte.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                if (e.ColumnIndex == _dgvReporte.Columns["colCat"]!.Index ||
                    e.ColumnIndex == _dgvReporte.Columns["colTotal"]!.Index)
                {
                    e.CellStyle!.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }

                if (e.ColumnIndex == _dgvReporte.Columns["colEstado"]!.Index)
                {
                    string est = e.Value?.ToString() ?? "";
                    e.CellStyle!.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    e.CellStyle.ForeColor = est switch
                    {
                        "Autorizado" => ColorTranslator.FromHtml("#B26A00"),
                        "Actualizado" => ColorTranslator.FromHtml("#1E64D8"),
                        _ => ColorTranslator.FromHtml("#1F4E35")
                    };
                }
            };

            _dgvReporte.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvReporte.Columns["colAccion"]!.Index)
                {
                    if (e.RowIndex < _filasReporte.Count)
                    {
                        ExportarReporteAPdf(_filasReporte[e.RowIndex]);
                    }
                }
            };
        }

        private void CargarDatosPeriodo()
        {
            _filasReporte.Clear();

            int dias = Math.Max(1, (_dtpFechaFinal.Value.Date - _dtpFechaInicial.Value.Date).Days + 1);
            decimal factorPeriodo = Math.Round(dias / 15m, 2);
            if (factorPeriodo <= 0) factorPeriodo = 1m;

            decimal ventasNetas = Math.Round(45820.00m * factorPeriodo, 2);
            decimal consumoReal = Math.Round(18450.00m * factorPeriodo, 2);
            decimal existenciasVal = 89120.00m;
            decimal mermasVal = Math.Round(2418.00m * factorPeriodo, 2);
            int tickets = Math.Max(12, (int)(1420 * factorPeriodo));

            _lblKpiVentasVal.Text = ventasNetas.ToString("C2", _culturaMxn);
            _lblKpiConsumoVal.Text = consumoReal.ToString("C2", _culturaMxn);
            _lblKpiExistenciasVal.Text = "27 Insumos";
            _lblKpiExistenciasSub.Text = $"Valorizado en {existenciasVal.ToString("C2", _culturaMxn)}";
            _lblKpiMermasVal.Text = mermasVal.ToString("C2", _culturaMxn);

            _filasReporte.AddRange(new[]
            {
                new FilaReporteFinanciero(
                    "Ventas netas",
                    "Ingresos totales descontando cancelaciones de caja",
                    $"{tickets:N0} tickets",
                    ventasNetas,
                    "Verificado"
                ),
                new FilaReporteFinanciero(
                    "Consumo real de ingredientes",
                    "Café, lácteos, jarabes y panadería descontados por recetas",
                    $"{Math.Round(350 * factorPeriodo, 0):N0} kg / L total",
                    consumoReal,
                    "Calculado"
                ),
                new FilaReporteFinanciero(
                    "Existencias actuales",
                    "Inventario físico valorizado PEPS al cierre de periodo",
                    "27 insumos activos",
                    existenciasVal,
                    "Actualizado"
                ),
                new FilaReporteFinanciero(
                    "Mermas y pérdidas",
                    "Total de mermas autorizadas por supervisión",
                    "5 solicitudes",
                    mermasVal,
                    "Autorizado"
                )
            });

            _dgvReporte.Rows.Clear();
            foreach (var f in _filasReporte)
            {
                _dgvReporte.Rows.Add(
                    f.Categoria,
                    f.Descripcion,
                    f.VolumenUnidades,
                    f.CostoValorTotal.ToString("C2", _culturaMxn),
                    f.Estado,
                    "Descargar PDF"
                );
            }
        }

        private void ExportarReporteAPdf(FilaReporteFinanciero? soloUnaFila)
        {
            string nombreSugerido = soloUnaFila == null
                ? $"Reporte_Financiero_Completo_{_dtpFechaInicial.Value:yyyyMMdd}_{_dtpFechaFinal.Value:yyyyMMdd}.pdf"
                : $"Reporte_{soloUnaFila.Categoria.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";

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
                var filasAImprimir = soloUnaFila == null
                    ? _filasReporte
                    : new List<FilaReporteFinanciero> { soloUnaFila };

                GenerarArchivoPdfNativo(sfd.FileName, filasAImprimir);

                var resp = MessageBox.Show(
                    $"El reporte PDF se generó correctamente en:\n{sfd.FileName}\n\n¿Deseas abrir el documento ahora?",
                    "Exportación PDF Exitosa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (resp == DialogResult.Yes && File.Exists(sfd.FileName))
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo generar el PDF directamente ({ex.Message}).\nVerifica que la impresora 'Microsoft Print to PDF' esté habilitada en Windows.",
                    "Error al exportar PDF",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void GenerarArchivoPdfNativo(string rutaDestinoPdf, List<FilaReporteFinanciero> filas)
        {
            using PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = "Microsoft Print to PDF";
            pd.PrinterSettings.PrintToFile = true;
            pd.PrinterSettings.PrintFileName = rutaDestinoPdf;

            pd.PrintPage += (s, e) =>
            {
                Graphics g = e.Graphics!;
                int x = 55;
                int y = 60;
                int anchoPagina = e.MarginBounds.Width;

                using Font fTitulo = new Font("Segoe UI", 18F, FontStyle.Bold);
                using Font fSub = new Font("Segoe UI", 10F, FontStyle.Regular);
                using Font fSec = new Font("Segoe UI", 11F, FontStyle.Bold);
                using Font fCuerpo = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                using Pen penBorde = new Pen(Color.FromArgb(210, 205, 198), 1);

                g.DrawString("SISTEMA DE GESTIÓN DE CAFETERÍA — SUCURSAL CENTRO", fSec, Brushes.SaddleBrown, x, y);
                y += 28;
                g.DrawString("Reporte Financiero y Analítico", fTitulo, Brushes.Black, x, y);
                y += 34;
                g.DrawString(
                    $"Periodo consultado: {_dtpFechaInicial.Value:dd/MM/yyyy} al {_dtpFechaFinal.Value:dd/MM/yyyy}   |   Generado: {DateTime.Now:dd/MM/yyyy HH:mm}",
                    fSub, Brushes.DimGray, x, y);
                y += 32;
                g.DrawLine(penBorde, x, y, x + anchoPagina, y);
                y += 24;

                // Encabezado de tabla en PDF
                g.FillRectangle(new SolidBrush(Color.FromArgb(245, 242, 238)), x, y, anchoPagina, 32);
                g.DrawString("CATEGORÍA", fSec, Brushes.Black, x + 8, y + 7);
                g.DrawString("VOLUMEN", fSec, Brushes.Black, x + 240, y + 7);
                g.DrawString("ESTADO", fSec, Brushes.Black, x + 380, y + 7);
                g.DrawString("TOTAL MXN", fSec, Brushes.Black, x + 490, y + 7);
                y += 38;

                foreach (var item in filas)
                {
                    g.DrawString(item.Categoria, fSec, Brushes.Black, x + 8, y);
                    g.DrawString(item.VolumenUnidades, fCuerpo, Brushes.Black, x + 240, y);
                    g.DrawString(item.Estado, fCuerpo, Brushes.DarkGreen, x + 380, y);
                    g.DrawString(item.CostoValorTotal.ToString("C2", _culturaMxn), fSec, Brushes.Black, x + 490, y);
                    y += 22;
                    g.DrawString(item.Descripcion, fSub, Brushes.DimGray, x + 8, y);
                    y += 24;
                    g.DrawLine(penBorde, x, y, x + anchoPagina, y);
                    y += 14;
                }

                y += 20;
                g.DrawString("Documento generado automáticamente para control administrativo y auditoría interna.", fSub, Brushes.Gray, x, y);
            };

            pd.Print();
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 32;
            int anchoUtil = Math.Max(820, ClientSize.Width - (margenX * 2));

            // 1. Tarjeta filtro de fechas
            int yFiltro = _pnlTopBar.Height + 16;
            _cardFiltroFechas.Location = new Point(margenX, yFiltro);

            // 2. Fila de 4 tarjetas KPI
            int yKpi = yFiltro + _cardFiltroFechas.Height + 16;
            int altoKpi = 112;
            int gapKpi = 16;
            int anchoKpi = (anchoUtil - (gapKpi * 3)) / 4;

            _cardKpiVentas.Bounds = new Rectangle(margenX, yKpi, anchoKpi, altoKpi);
            _cardKpiConsumo.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi), yKpi, anchoKpi, altoKpi);
            _cardKpiExistencias.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi) * 2, yKpi, anchoKpi, altoKpi);
            _cardKpiMermas.Bounds = new Rectangle(margenX + (anchoKpi + gapKpi) * 3, yKpi, anchoUtil - ((anchoKpi + gapKpi) * 3), altoKpi);

            // 3. Tarjeta inferior con tabla consolidada
            int yTabla = yKpi + altoKpi + 18;
            int altoTablaCard = Math.Max(290, ClientSize.Height - yTabla - 22);
            _cardTabla.Bounds = new Rectangle(margenX, yTabla, anchoUtil, altoTablaCard);

            _btnExportarTodoPdf.Location = new Point(anchoUtil - _btnExportarTodoPdf.Width - 22, 14);
            _dgvReporte.Bounds = new Rectangle(16, 62, anchoUtil - 32, altoTablaCard - 78);
        }
    }
}
