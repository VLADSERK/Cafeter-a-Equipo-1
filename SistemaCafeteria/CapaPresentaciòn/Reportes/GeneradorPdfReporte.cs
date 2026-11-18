using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using CapaEntidades;

namespace CapaPresentaciòn.Reportes
{
    /// <summary>
    /// Exporta el reporte financiero a PDF (RIU-06) con la impresora "Microsoft Print to PDF",
    /// incluida en Windows 11 (RNF-12), sin dependencias externas.
    /// Hoja carta horizontal, encabezado y pie en cada página y tablas paginadas
    /// que repiten su encabezado al continuar en la página siguiente.
    /// </summary>
    public static class GeneradorPdfReporte
    {
        public const string ImpresoraPdf = "Microsoft Print to PDF";

        public static bool ImpresoraDisponible() =>
            PrinterSettings.InstalledPrinters.Cast<string>()
                .Any(p => string.Equals(p, ImpresoraPdf, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Genera el PDF. secciones vacío o null = reporte completo (resumen + 5.1 a 5.4).
        /// Devuelve false si Windows no terminó de escribir el archivo en el tiempo de espera.
        /// </summary>
        public static bool Exportar(ReporteFinanciero reporte, string rutaPdf, IReadOnlyCollection<SeccionReporte>? secciones = null)
        {
            if (!ImpresoraDisponible())
                throw new InvalidOperationException(
                    $"No se encontró la impresora \"{ImpresoraPdf}\". Actívala en Windows: Configuración > Bluetooth y dispositivos > Impresoras y escáneres.");

            var lista = (secciones == null || secciones.Count == 0)
                ? Enum.GetValues<SeccionReporte>().ToList()
                : secciones.ToList();

            var elementos = ConstruirElementos(reporte, lista);

            if (File.Exists(rutaPdf))
                File.Delete(rutaPdf);

            using var fuentes = new Fuentes();
            using var pd = new PrintDocument
            {
                DocumentName = Path.GetFileNameWithoutExtension(rutaPdf),
                PrintController = new StandardPrintController()   // sin ventana "Imprimiendo…"
            };
            pd.PrinterSettings.PrinterName = ImpresoraPdf;
            pd.PrinterSettings.PrintToFile = true;
            pd.PrinterSettings.PrintFileName = rutaPdf;
            pd.DefaultPageSettings.Landscape = true;
            pd.DefaultPageSettings.Margins = new Margins(45, 45, 40, 40);

            int indice = 0;
            int pagina = 0;
            TablaReporte? tablaEnCurso = null;

            pd.PrintPage += (s, e) =>
            {
                var g = e.Graphics!;
                var area = e.MarginBounds;
                pagina++;

                float y = DibujarEncabezado(g, area, reporte, fuentes);
                float limite = area.Bottom - 22;   // espacio del pie

                // Si una tabla quedó partida, repetir su encabezado
                if (tablaEnCurso != null && indice < elementos.Count &&
                    (elementos[indice].Tipo == TipoElemento.Fila || elementos[indice].Tipo == TipoElemento.Total))
                    y = DibujarEncabezadoTabla(g, area, y, tablaEnCurso, fuentes);

                while (indice < elementos.Count)
                {
                    var el = elementos[indice];
                    float alto = AltoRequerido(el);

                    if (y + alto > limite)
                    {
                        // Un título de sección nunca queda solo al final de la hoja
                        if (y > area.Top + 80)
                            break;
                    }

                    switch (el.Tipo)
                    {
                        case TipoElemento.Titulo:
                            y += 6;
                            g.DrawString(el.Texto, fuentes.Seccion, Brushes.Black, area.Left, y);
                            y += 22;
                            break;

                        case TipoElemento.Parrafo:
                            var rect = new RectangleF(area.Left, y, area.Width, 30);
                            g.DrawString(el.Texto, fuentes.Texto, Brushes.DimGray, rect);
                            y += g.MeasureString(el.Texto, fuentes.Texto, area.Width).Height + 4;
                            break;

                        case TipoElemento.EncabezadoTabla:
                            tablaEnCurso = el.Tabla;
                            y = DibujarEncabezadoTabla(g, area, y, el.Tabla!, fuentes);
                            break;

                        case TipoElemento.Fila:
                            y = DibujarFila(g, area, y, el.Tabla!, el.Celdas!, false, fuentes);
                            break;

                        case TipoElemento.Total:
                            y = DibujarFila(g, area, y, el.Tabla!, el.Celdas!, true, fuentes);
                            tablaEnCurso = null;
                            break;

                        case TipoElemento.Vacio:
                            g.DrawString(el.Texto, fuentes.Texto, Brushes.Gray, area.Left + 6, y + 3);
                            y += 20;
                            tablaEnCurso = null;
                            break;

                        case TipoElemento.Espacio:
                            y += 14;
                            break;
                    }
                    indice++;
                }

                DibujarPie(g, area, pagina, fuentes);
                e.HasMorePages = indice < elementos.Count;
            };

            pd.Print();
            return EsperarArchivo(rutaPdf, TimeSpan.FromSeconds(15));
        }

        // =====================================================================
        // CONTENIDO
        // =====================================================================

        private enum TipoElemento { Titulo, Parrafo, EncabezadoTabla, Fila, Total, Vacio, Espacio }

        private sealed class Elemento
        {
            public TipoElemento Tipo { get; init; }
            public string Texto { get; init; } = string.Empty;
            public TablaReporte? Tabla { get; init; }
            public string[]? Celdas { get; init; }
        }

        private static List<Elemento> ConstruirElementos(ReporteFinanciero r, List<SeccionReporte> secciones)
        {
            var elementos = new List<Elemento>();

            // Resumen consolidado: todas las filas en el reporte completo, solo la de la sección si es individual
            var resumen = TablasReporte.Resumen(r);
            var nombres = secciones.Select(TablasReporte.NombreSeccion).ToHashSet();
            var resumenFiltrado = new TablaReporte
            {
                Titulo = resumen.Titulo,
                Descripcion = resumen.Descripcion,
                Columnas = resumen.Columnas,
                ColumnaEstado = resumen.ColumnaEstado,
                Filas = resumen.Filas.Where(f => nombres.Contains(f[0])).ToList()
            };
            AgregarTabla(elementos, resumenFiltrado);

            foreach (var s in secciones)
            {
                elementos.Add(new Elemento { Tipo = TipoElemento.Espacio });
                AgregarTabla(elementos, TablasReporte.Detalle(s, r));
            }

            return elementos;
        }

        private static void AgregarTabla(List<Elemento> elementos, TablaReporte t)
        {
            elementos.Add(new Elemento { Tipo = TipoElemento.Titulo, Texto = t.Titulo });
            if (!string.IsNullOrWhiteSpace(t.Descripcion))
                elementos.Add(new Elemento { Tipo = TipoElemento.Parrafo, Texto = t.Descripcion });

            elementos.Add(new Elemento { Tipo = TipoElemento.EncabezadoTabla, Tabla = t });

            if (t.Filas.Count == 0)
            {
                elementos.Add(new Elemento { Tipo = TipoElemento.Vacio, Texto = "Sin registros en el periodo seleccionado." });
                return;
            }

            foreach (var f in t.Filas)
                elementos.Add(new Elemento { Tipo = TipoElemento.Fila, Tabla = t, Celdas = f });

            if (t.Totales != null)
                elementos.Add(new Elemento { Tipo = TipoElemento.Total, Tabla = t, Celdas = t.Totales });
        }

        private static float AltoRequerido(Elemento el) => el.Tipo switch
        {
            TipoElemento.Titulo => 28 + 18 + 22 + 18,    // título + descripción + encabezado + 1 fila (evita títulos huérfanos)
            TipoElemento.EncabezadoTabla => 22 + 18,
            TipoElemento.Parrafo => 18,
            TipoElemento.Espacio => 14,
            TipoElemento.Vacio => 20,
            _ => 18
        };

        // =====================================================================
        // DIBUJO
        // =====================================================================

        private sealed class Fuentes : IDisposable
        {
            public readonly Font Marca = new("Segoe UI", 9F, FontStyle.Bold);
            public readonly Font Titulo = new("Segoe UI", 15F, FontStyle.Bold);
            public readonly Font Seccion = new("Segoe UI", 11F, FontStyle.Bold);
            public readonly Font Texto = new("Segoe UI", 8.5F, FontStyle.Regular);
            public readonly Font Celda = new("Segoe UI", 8F, FontStyle.Regular);
            public readonly Font CeldaNegrita = new("Segoe UI", 8F, FontStyle.Bold);
            public readonly Pen Linea = new(Color.FromArgb(215, 210, 203), 1);
            public readonly SolidBrush FondoEncabezado = new(Color.FromArgb(245, 242, 238));
            public readonly SolidBrush FondoTotal = new(Color.FromArgb(239, 230, 220));
            public readonly SolidBrush Cafe = new(Color.FromArgb(84, 55, 38));

            public void Dispose()
            {
                foreach (var d in new IDisposable[] { Marca, Titulo, Seccion, Texto, Celda, CeldaNegrita, Linea, FondoEncabezado, FondoTotal, Cafe })
                    d.Dispose();
            }
        }

        private static float DibujarEncabezado(Graphics g, Rectangle area, ReporteFinanciero r, Fuentes f)
        {
            float y = area.Top;
            g.DrawString("SISTEMA DE GESTIÓN DE CAFETERÍA", f.Marca, f.Cafe, area.Left, y);
            y += 16;
            g.DrawString("Reporte Financiero y Analítico", f.Titulo, Brushes.Black, area.Left, y);
            y += 28;
            g.DrawString($"Periodo: {r.Desde:dd/MM/yyyy} al {r.Hasta:dd/MM/yyyy}   ·   Generado: {r.GeneradoEn:dd/MM/yyyy HH:mm}   ·   Importes en pesos mexicanos (MXN)",
                f.Texto, Brushes.DimGray, area.Left, y);
            y += 18;
            g.DrawLine(f.Linea, area.Left, y, area.Right, y);
            return y + 8;
        }

        private static void DibujarPie(Graphics g, Rectangle area, int pagina, Fuentes f)
        {
            float y = area.Bottom - 14;
            g.DrawLine(f.Linea, area.Left, y - 4, area.Right, y - 4);
            g.DrawString("Documento generado automáticamente para control administrativo y auditoría interna (RF-10).",
                f.Texto, Brushes.Gray, area.Left, y);
            string textoPagina = $"Página {pagina}";
            var tam = g.MeasureString(textoPagina, f.Texto);
            g.DrawString(textoPagina, f.Texto, Brushes.Gray, area.Right - tam.Width, y);
        }

        private static float[] AnchosColumnas(Rectangle area, TablaReporte t)
        {
            float total = t.Columnas.Sum(c => c.Peso);
            return t.Columnas.Select(c => area.Width * c.Peso / total).ToArray();
        }

        private static float DibujarEncabezadoTabla(Graphics g, Rectangle area, float y, TablaReporte t, Fuentes f)
        {
            float alto = 22;
            g.FillRectangle(f.FondoEncabezado, area.Left, y, area.Width, alto);

            var anchos = AnchosColumnas(area, t);
            float x = area.Left;
            for (int i = 0; i < t.Columnas.Count; i++)
            {
                DibujarCelda(g, t.Columnas[i].Titulo.ToUpperInvariant(), f.CeldaNegrita, Brushes.DimGray,
                    new RectangleF(x, y, anchos[i], alto), t.Columnas[i].AlinearDerecha);
                x += anchos[i];
            }
            return y + alto;
        }

        private static float DibujarFila(Graphics g, Rectangle area, float y, TablaReporte t, string[] celdas, bool esTotal, Fuentes f)
        {
            float alto = 18;
            if (esTotal)
                g.FillRectangle(f.FondoTotal, area.Left, y, area.Width, alto);

            var anchos = AnchosColumnas(area, t);
            float x = area.Left;
            for (int i = 0; i < t.Columnas.Count && i < celdas.Length; i++)
            {
                Brush color = Brushes.Black;
                if (!esTotal && i == t.ColumnaEstado)
                    color = ColorEstado(celdas[i]);

                // En la fila de totales, un texto ocupa también las celdas vacías que le siguen
                float ancho = anchos[i];
                int ultima = i;
                if (esTotal && celdas[i].Length > 0)
                {
                    while (ultima + 1 < t.Columnas.Count && ultima + 1 < celdas.Length && celdas[ultima + 1].Length == 0)
                    {
                        ultima++;
                        ancho += anchos[ultima];
                    }
                }

                DibujarCelda(g, celdas[i], esTotal || i == 0 ? f.CeldaNegrita : f.Celda, color,
                    new RectangleF(x, y, ancho, alto), t.Columnas[i].AlinearDerecha && ultima == i);
                x += ancho;
                i = ultima;
            }

            g.DrawLine(f.Linea, area.Left, y + alto, area.Right, y + alto);
            return y + alto;
        }

        private static void DibujarCelda(Graphics g, string texto, Font fuente, Brush color, RectangleF rect, bool derecha)
        {
            using var formato = new StringFormat
            {
                Alignment = derecha ? StringAlignment.Far : StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            var interior = new RectangleF(rect.X + 4, rect.Y, rect.Width - 8, rect.Height);
            g.DrawString(texto, fuente, color, interior, formato);
        }

        private static Brush ColorEstado(string estado) => estado switch
        {
            "Cancelada" or "Rechazada" or "Rojo" => Brushes.Firebrick,
            "Pendiente" or "Amarillo" => Brushes.DarkGoldenrod,
            "Blanco" => Brushes.DimGray,
            _ => Brushes.DarkGreen
        };

        /// <summary>El spooler de Windows escribe el PDF de forma asíncrona: se espera a que el archivo exista y esté completo.</summary>
        private static bool EsperarArchivo(string ruta, TimeSpan espera)
        {
            var limite = DateTime.Now + espera;
            long tamanoAnterior = -1;
            while (DateTime.Now < limite)
            {
                if (File.Exists(ruta))
                {
                    try
                    {
                        long tamano = new FileInfo(ruta).Length;
                        if (tamano > 0 && tamano == tamanoAnterior)
                        {
                            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.None);
                            return true;
                        }
                        tamanoAnterior = tamano;
                    }
                    catch (IOException)
                    {
                        // Todavía lo está escribiendo el spooler
                    }
                }
                Thread.Sleep(250);
            }
            return File.Exists(ruta) && new FileInfo(ruta).Length > 0;
        }
    }
}
