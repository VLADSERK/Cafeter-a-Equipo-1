using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CapaLogica;

namespace CapaPresentacion.Estilos
{
    /// <summary>
    /// Presentación común de las tablas de inventario (cajero y Administrador): una fila por lote,
    /// agrupadas por insumo. Evita textos recortados y dibuja la separación por grupos.
    /// </summary>
    public static class TablaInventario
    {
        // Fuentes compartidas (antes se creaba una Font nueva por cada fila en cada recarga)
        public static readonly Font FuenteInsumo = new("Segoe UI", 9.5F, FontStyle.Bold);
        public static readonly Font FuenteAviso = new("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font FuenteLote = new("Consolas", 9.5F, FontStyle.Bold);

        private static readonly Color LineaGrupo = TemaCafeteria.BordeSuave;
        private static readonly Color LineaLote = ColorTranslator.FromHtml("#F1ECE6");

        private const TextFormatFlags FormatoMedida = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        /// <summary>
        /// Configura la tabla: las columnas indicadas pueden partir el texto en dos renglones,
        /// la altura de fila crece si hace falta (sin bajar de la altura de diseño) y las líneas
        /// separadoras las dibuja <see cref="PintarSeparadores"/>.
        /// </summary>
        public static void Configurar(DataGridView dgv, params string[] columnasConSalto)
        {
            foreach (string nombre in columnasConSalto)
            {
                if (dgv.Columns[nombre] is { } col)
                    col.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }

            dgv.RowTemplate.MinimumHeight = dgv.RowTemplate.Height;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;
        }

        /// <summary>
        /// Fija el ancho mínimo de cada columna según su contenido actual para que ningún texto
        /// se corte con "...": en columnas de una línea cabe el texto completo más largo y en las
        /// que parten renglón cabe su palabra más larga. Los encabezados (que parten renglón)
        /// aportan su palabra más larga. Si la ventana es angosta aparece la barra horizontal.
        /// </summary>
        /// <param name="anchosFijos">Mínimos adicionales por columna (por ejemplo, celdas pintadas a mano).</param>
        public static void AjustarAnchosMinimos(DataGridView dgv, IReadOnlyDictionary<string, int>? anchosFijos = null)
        {
            Font fuenteCelda = dgv.DefaultCellStyle.Font ?? dgv.Font;
            Font fuenteEncabezado = dgv.ColumnHeadersDefaultCellStyle.Font ?? fuenteCelda;
            int margenCelda = dgv.DefaultCellStyle.Padding.Horizontal + 10;
            int margenEncabezado = dgv.ColumnHeadersDefaultCellStyle.Padding.Horizontal + 10;

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (!col.Visible)
                    continue;

                bool parteRenglon = col.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
                int minimo = PalabraMasAncha(col.HeaderText, fuenteEncabezado) + margenEncabezado;

                foreach (DataGridViewRow row in dgv.Rows)
                {
                    string texto = row.Cells[col.Index].Value?.ToString() ?? string.Empty;
                    if (texto.Length == 0)
                        continue;

                    Font fuente = row.Cells[col.Index].Style.Font ?? col.DefaultCellStyle.Font ?? fuenteCelda;
                    int ancho = parteRenglon ? PalabraMasAncha(texto, fuente) : Medir(texto, fuente);
                    minimo = Math.Max(minimo, ancho + margenCelda);
                }

                if (anchosFijos != null && anchosFijos.TryGetValue(col.Name, out int fijo))
                    minimo = Math.Max(minimo, fijo);

                col.MinimumWidth = Math.Max(40, minimo);
            }
        }

        /// <summary>Ancho que necesita una celda de semáforo pintada con <see cref="SemaforoVisual.PintarCelda"/>.</summary>
        public static int AnchoCeldaSemaforo()
            => new[] { NivelSemaforo.Verde, NivelSemaforo.Amarillo, NivelSemaforo.Rojo, NivelSemaforo.Blanco }
                   .Max(n => SemaforoVisual.AnchoContenido(n)) + 18;

        /// <summary>
        /// Línea marcada al terminar cada insumo y una línea tenue entre sus lotes, que solo cruza
        /// las columnas del lote: así las filas secundarias se leen como parte del mismo insumo.
        /// </summary>
        /// <param name="mismoGrupo">True si las dos filas (índices) pertenecen al mismo insumo.</param>
        /// <param name="columnaInicioLote">Primera columna con datos del lote.</param>
        public static void PintarSeparadores(DataGridView dgv, DataGridViewRowPostPaintEventArgs e,
            Func<int, int, bool> mismoGrupo, string columnaInicioLote)
        {
            int i = e.RowIndex;
            bool cierraGrupo = i >= dgv.Rows.Count - 1 || !mismoGrupo(i, i + 1);
            int y = e.RowBounds.Bottom - 1;
            var g = e.Graphics!;

            if (cierraGrupo)
            {
                using var pen = new Pen(LineaGrupo, 1);
                g.DrawLine(pen, e.RowBounds.Left, y, e.RowBounds.Right, y);
                return;
            }

            int x = e.RowBounds.Left;
            if (dgv.Columns[columnaInicioLote] is { } col)
            {
                var rect = dgv.GetColumnDisplayRectangle(col.Index, false);
                if (rect.Width > 0)
                    x = rect.Left;
            }

            using var penLote = new Pen(LineaLote, 1);
            g.DrawLine(penLote, x, y, e.RowBounds.Right, y);
        }

        private static int Medir(string texto, Font fuente)
            => TextRenderer.MeasureText(texto, fuente, Size.Empty, FormatoMedida).Width;

        private static int PalabraMasAncha(string texto, Font fuente)
            => texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => Medir(p, fuente))
                    .DefaultIfEmpty(0)
                    .Max();
    }
}
