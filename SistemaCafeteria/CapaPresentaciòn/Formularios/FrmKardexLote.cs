using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CapaEntidades;
using CapaLogica;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Formularios
{
    /// <summary>
    /// Kardex de un lote (RF-03): datos del lote y sus movimientos con fecha, hora, tipo,
    /// cantidad, saldo y referencia del documento de origen. Solo lectura.
    /// </summary>
    public sealed class FrmKardexLote : Form
    {
        private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

        private readonly DataGridView _dgv;
        private readonly Label _lblPie;

        /// <summary>Abre el kardex del lote como ventana modal.</summary>
        public static void Mostrar(IWin32Window owner, Lote lote)
        {
            var (exito, mensaje, movimientos) = new InventarioNegocio().ObtenerKardexLote(lote.IdLote);
            if (!exito)
            {
                MessageBox.Show(owner, mensaje, "Historial del lote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var frm = new FrmKardexLote(lote, movimientos);
            frm.ShowDialog(owner);
        }

        private FrmKardexLote(Lote lote, List<MovimientoKardex> movimientos)
        {
            Text = $"Historial de movimientos — {lote.CodigoLote}";
            Size = new Size(1080, 620);
            MinimumSize = new Size(860, 460);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = TemaCafeteria.FondoCrema;
            Font = new Font("Segoe UI", 9.5F);

            // ---- Encabezado con los datos del lote ----
            var pnlEncabezado = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = Color.White, Padding = new Padding(24, 14, 24, 10) };
            pnlEncabezado.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, pnlEncabezado.Height - 1, pnlEncabezado.Width, pnlEncabezado.Height - 1);
            };

            var lblRuta = new Label
            {
                Text = "KARDEX POR LOTE (PEPS)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(24, 12),
                AutoSize = true
            };

            var lblTitulo = new Label
            {
                Text = $"{lote.NombreInsumo}  ·  {lote.CodigoLote}",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(22, 30),
                AutoSize = true
            };

            string u = lote.UnidadAbreviatura;
            var datos = new (string Titulo, string Valor)[]
            {
                ("Unidad", u),
                ("Recepción", lote.FechaRecepcion.ToString("dd/MM/yyyy HH:mm", Mx)),
                ("Proveedor", string.IsNullOrWhiteSpace(lote.NombreProveedor) ? "—" : lote.NombreProveedor),
                ("Caducidad", lote.FechaCaducidad?.ToString("dd/MM/yyyy", Mx) ?? "Sin caducidad"),
                ("Cantidad inicial", $"{Cant(lote.CantidadInicial)} {u}"),
                ("Disponible", $"{Cant(lote.CantidadDisponible)} {u}"),
                ("Costo unitario", lote.CostoUnitario.ToString("C2", Mx))
            };

            var flowDatos = new FlowLayoutPanel
            {
                Location = new Point(24, 70),
                Size = new Size(1000, 52),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                WrapContents = false,
                AutoScroll = false
            };
            foreach (var (titulo, valor) in datos)
            {
                var celda = new Panel { Size = new Size(10, 48), Margin = new Padding(0, 0, 26, 0) };
                var lt = new Label { Text = titulo, Font = new Font("Segoe UI", 8F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(0, 2), AutoSize = true };
                var lv = new Label { Text = valor, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, Location = new Point(0, 20), AutoSize = true };
                celda.Controls.AddRange(new Control[] { lt, lv });
                celda.Width = Math.Max(TextRenderer.MeasureText(titulo, lt.Font).Width, TextRenderer.MeasureText(valor, lv.Font).Width) + 4;
                flowDatos.Controls.Add(celda);
            }

            pnlEncabezado.Controls.AddRange(new Control[] { lblRuta, lblTitulo, flowDatos });

            // ---- Tabla del kardex ----
            var card = new PanelRedondeado { RadioBorde = 14, BackColor = Color.White, Dock = DockStyle.Fill, Padding = new Padding(14) };
            var contenedor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 8), BackColor = TemaCafeteria.FondoCrema };

            _dgv = new DataGridView
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
                ColumnHeadersHeight = 38,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 38 }
            };
            _dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F7F3EE"),
                ForeColor = TemaCafeteria.TextoSecundario,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Padding = new Padding(6, 0, 6, 0)
            };
            _dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F),
                SelectionBackColor = ColorTranslator.FromHtml("#F4ECE4"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(6, 0, 6, 0)
            };

            AgregarColumna("colFecha", "FECHA", 10);
            AgregarColumna("colHora", "HORA", 7);
            AgregarColumna("colTipo", "TIPO DE MOVIMIENTO", 17);
            AgregarColumna("colCantidad", "CANTIDAD", 11, true);
            AgregarColumna("colSaldo", "SALDO", 11, true);
            AgregarColumna("colReferencia", "REFERENCIA DE ORIGEN", 30);
            AgregarColumna("colUsuario", "USUARIO", 14);

            foreach (var m in movimientos)
            {
                int i = _dgv.Rows.Add(
                    m.FechaMovimiento.ToString("dd/MM/yyyy", Mx),
                    m.FechaMovimiento.ToString("HH:mm:ss", Mx),
                    m.TipoDescripcion,
                    $"{(m.EsEntrada ? "+" : "−")}{Cant(m.Cantidad)} {u}",
                    $"{Cant(m.Saldo)} {u}",
                    m.Referencia,
                    m.Usuario);

                var fila = _dgv.Rows[i];
                fila.Cells["colCantidad"].Style.ForeColor = m.EsEntrada ? ColorTranslator.FromHtml("#1F6B3A") : ColorTranslator.FromHtml("#C62828");
                fila.Cells["colCantidad"].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                if (!string.IsNullOrWhiteSpace(m.Observaciones))
                    fila.Cells["colReferencia"].ToolTipText = m.Observaciones;
            }

            card.Controls.Add(_dgv);
            contenedor.Controls.Add(card);

            // ---- Pie: resumen y verificación del saldo ----
            var pnlPie = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = TemaCafeteria.FondoCrema, Padding = new Padding(24, 8, 24, 12) };

            decimal entradas = movimientos.Where(m => m.EsEntrada).Sum(m => m.Cantidad);
            decimal salidas = movimientos.Where(m => !m.EsEntrada).Sum(m => m.Cantidad);
            decimal saldoKardex = movimientos.Count > 0 ? movimientos[^1].Saldo : 0m;
            bool cuadra = saldoKardex == Redondeo.Cantidad(lote.CantidadDisponible);

            _lblPie = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = cuadra ? TemaCafeteria.TextoOscuro : ColorTranslator.FromHtml("#C62828"),
                Text = movimientos.Count == 0
                    ? "Este lote no tiene movimientos registrados."
                    : $"{movimientos.Count} {(movimientos.Count == 1 ? "movimiento" : "movimientos")}  ·  Entradas +{Cant(entradas)} {u}  ·  Salidas −{Cant(salidas)} {u}  ·  " +
                      (cuadra
                          ? $"Saldo {Cant(saldoKardex)} {u} = disponible en el lote ✓"
                          : $"⚠ Saldo del kardex {Cant(saldoKardex)} {u} ≠ disponible {Cant(lote.CantidadDisponible)} {u}")
            };

            var btnCerrar = new Button
            {
                Text = "Cerrar",
                Dock = DockStyle.Right,
                Width = 130,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            CancelButton = btnCerrar;
            AcceptButton = btnCerrar;

            pnlPie.Controls.Add(_lblPie);
            pnlPie.Controls.Add(btnCerrar);

            Controls.Add(contenedor);
            Controls.Add(pnlPie);
            Controls.Add(pnlEncabezado);
        }

        private void AgregarColumna(string nombre, string titulo, float peso, bool derecha = false)
        {
            var col = new DataGridViewTextBoxColumn { Name = nombre, HeaderText = titulo, FillWeight = peso, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (derecha)
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            _dgv.Columns.Add(col);
        }

        private static string Cant(decimal valor) => FormatoInventario.Numero(valor);
    }
}
