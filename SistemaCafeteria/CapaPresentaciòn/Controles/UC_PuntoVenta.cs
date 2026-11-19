using CapaLogica;
using CapaPresentacion.Estilos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ent = CapaEntidades;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_PuntoVenta : UserControl
    {
        // Nombres de categoría tal como están en la tabla Categoria de MySQL
        private const string CatCalientes = "Bebidas Calientes";
        private const string CatFrias = "Bebidas Frías";
        private const string CatReposteria = "Panadería y Repostería";

        private readonly CultureInfo _culturaMxn = new("es-MX");
        private readonly CatalogoNegocio _catalogoNegocio = new();
        private readonly VentaNegocio _ventaNegocio = new();

        private class ItemComanda
        {
            public required Ent.Producto Producto { get; init; }
            public int Cantidad { get; set; }
            public decimal Importe => Ent.Redondeo.Importe(Producto.Precio * Cantidad);
        }

        private List<Ent.Producto> _catalogoCompleto = new();
        private readonly List<ItemComanda> _comandaActual = new();

        // Disponibilidad por receta e inventario + límite fijo por producto (se recalcula con cada cambio de la comanda)
        private DisponibilidadVenta _disponibilidad = DisponibilidadVenta.SoloLimite(VentaNegocio.LimitePorProducto);
        private readonly Dictionary<int, (Label Estado, Button Agregar)> _controlesTarjeta = new();
        private readonly ToolTip _toolTip = new();
        private readonly Dictionary<string, Image> _cacheImagenes = new(StringComparer.OrdinalIgnoreCase);
        private string _categoriaActiva = CatCalientes;
        private string? _ultimoFolio;

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblEstadoCaja = null!;
        private Label _lblTitulo = null!;
        private Label _lblFechaHora = null!;

        // Columna izquierda: Catálogo
        private Panel _pnlIzquierdo = null!;
        private Label _lblSubtituloProductos = null!;
        private Label _lblSubDescProductos = null!;
        private Label _lblBadgeContador = null!;
        private PanelRedondeado _pnlTabsCategorias = null!;
        private Button _btnTabCalientes = null!;
        private Button _btnTabFrias = null!;
        private Button _btnTabReposteria = null!;
        private FlowLayoutPanel _flowProductos = null!;

        // Columna derecha: Comanda actual
        private Panel _pnlDerecho = null!;
        private Label _lblNumComanda = null!;
        private Label _lblTituloComanda = null!;
        private Label _lblBadgeArticulos = null!;
        private FlowLayoutPanel _flowComanda = null!;
        private Label _lblSubtotalValor = null!;
        private Label _lblTotalValor = null!;
        private TextBox _txtMontoRecibido = null!;
        private Label _lblCambioValor = null!;
        private Button _btnConfirmarVenta = null!;

        public UC_PuntoVenta()
        {
            InitializeComponent();
            Disposed += (s, e) => _toolTip.Dispose();
            ConfigurarInterfazPuntoVenta();
            RenderizarProductos();
            RefrescarComanda();

            Load += (s, e) =>
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
                    return;

                CargarCatalogo();
            };
        }

        // =====================================================================
        // CARGA DESDE MYSQL
        // =====================================================================

        /// <summary>Productos activos y con receta vigente.</summary>
        private void CargarCatalogo()
        {
            try
            {
                _catalogoCompleto = _catalogoNegocio.ObtenerProductosParaVenta();
                _disponibilidad = _ventaNegocio.ObtenerDisponibilidad();
            }
            catch (Exception ex)
            {
                _catalogoCompleto = new List<Ent.Producto>();
                _disponibilidad = DisponibilidadVenta.SoloLimite(VentaNegocio.LimitePorProducto);
                MessageBox.Show($"No se pudo cargar el catálogo desde MySQL:\n{ex.Message}",
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Si cambió algún precio o se desactivó un producto, actualizar la comanda abierta
            foreach (var item in _comandaActual.ToList())
            {
                var actualizado = _catalogoCompleto.FirstOrDefault(p => p.IdProducto == item.Producto.IdProducto);
                _comandaActual.Remove(item);
                if (actualizado != null)
                    _comandaActual.Add(new ItemComanda { Producto = actualizado, Cantidad = item.Cantidad });
            }

            // Si las existencias bajaron, ajustar las cantidades de la comanda al nuevo máximo
            foreach (var item in _comandaActual.ToList())
            {
                int maximo = _disponibilidad.MaximoPara(item.Producto.IdProducto, CantidadesComanda());
                if (item.Cantidad <= maximo) continue;

                if (maximo <= 0) _comandaActual.Remove(item);
                else item.Cantidad = maximo;
            }

            RenderizarProductos();
            RefrescarComanda();
        }

        // =====================================================================
        // INTERFAZ
        // =====================================================================

        private void ConfigurarInterfazPuntoVenta()
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

            _lblEstadoCaja = new Label
            {
                Text = "●  CAJA 01 ABIERTA",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.SemaforoVerde,
                Location = new Point(32, 16),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Punto de venta",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(30, 34),
                AutoSize = true
            };

            _lblFechaHora = new Label
            {
                Text = $"{DateTime.Now:dddd, dd 'de' MMMM}\n{DateTime.Now:hh:mm tt}",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblEstadoCaja, _lblTitulo, _lblFechaHora });

            // 2. Columna derecha (Comanda actual fija a la derecha)
            ConstruirPanelComandaDerecho();

            // 3. Columna izquierda (Catálogo de productos)
            ConstruirPanelCatalogoIzquierdo();

            Controls.AddRange(new Control[] { _pnlIzquierdo, _pnlDerecho, _pnlTopBar });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private void ConstruirPanelCatalogoIzquierdo()
        {
            _pnlIzquierdo = new Panel
            {
                BackColor = TemaCafeteria.FondoCrema
            };

            _lblSubtituloProductos = new Label
            {
                Text = "Selecciona productos",
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(0, 0),
                AutoSize = true
            };

            _lblSubDescProductos = new Label
            {
                Text = "Agrega artículos a la comanda actual",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(0, 26),
                AutoSize = true
            };

            _lblBadgeContador = new Label
            {
                Text = "0 productos",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                BackColor = Color.White,
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true
            };

            // Barra de pestañas de categorías
            _pnlTabsCategorias = new PanelRedondeado
            {
                Location = new Point(0, 56),
                Height = 46,
                RadioBorde = 12,
                BackColor = ColorTranslator.FromHtml("#EFEAE4")
            };

            _btnTabCalientes = CrearBotonCategoria("☕  Bebidas Calientes");
            _btnTabFrias = CrearBotonCategoria("🧊  Bebidas Frías");
            _btnTabReposteria = CrearBotonCategoria("🥐  Repostería");

            _btnTabCalientes.Click += (s, e) => CambiarCategoria(CatCalientes);
            _btnTabFrias.Click += (s, e) => CambiarCategoria(CatFrias);
            _btnTabReposteria.Click += (s, e) => CambiarCategoria(CatReposteria);

            _pnlTabsCategorias.Controls.AddRange(new Control[] { _btnTabCalientes, _btnTabFrias, _btnTabReposteria });

            // Contenedor scrollable de tarjetas
            _flowProductos = new FlowLayoutPanel
            {
                Location = new Point(0, 114),
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent
            };

            _pnlIzquierdo.Controls.AddRange(new Control[]
            {
                _lblSubtituloProductos, _lblSubDescProductos, _lblBadgeContador,
                _pnlTabsCategorias, _flowProductos
            });

            ActualizarEstiloTabs();
        }

        private Button CrearBotonCategoria(string texto)
        {
            Button btn = new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Height = 36
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void CambiarCategoria(string categoria)
        {
            _categoriaActiva = categoria;
            ActualizarEstiloTabs();
            RenderizarProductos();
        }

        private void ActualizarEstiloTabs()
        {
            var mapa = new (Button btn, string cat)[]
            {
                (_btnTabCalientes, CatCalientes),
                (_btnTabFrias, CatFrias),
                (_btnTabReposteria, CatReposteria)
            };

            foreach (var (btn, cat) in mapa)
            {
                bool activo = cat == _categoriaActiva;
                btn.BackColor = activo ? Color.White : ColorTranslator.FromHtml("#EFEAE4");
                btn.ForeColor = activo ? TemaCafeteria.TextoOscuro : TemaCafeteria.TextoSecundario;
            }
        }

        private void RenderizarProductos()
        {
            _flowProductos.SuspendLayout();
            _flowProductos.Controls.Clear();
            _controlesTarjeta.Clear();

            var filtrados = _catalogoCompleto.Where(p => p.NombreCategoria == _categoriaActiva).ToList();
            _lblBadgeContador.Text = $"{filtrados.Count} productos";

            int anchoTarjeta = Math.Max(195, (_flowProductos.ClientSize.Width - 46) / 3);
            if (_flowProductos.ClientSize.Width < 620)
                anchoTarjeta = Math.Max(210, (_flowProductos.ClientSize.Width - 32) / 2);

            foreach (var prod in filtrados)
            {
                _flowProductos.Controls.Add(CrearTarjetaProducto(prod, anchoTarjeta));
            }

            _flowProductos.ResumeLayout();
            ActualizarEstadoTarjetas();
        }

        // =====================================================================
        // DISPONIBILIDAD (límite por producto + insumos de la receta)
        // =====================================================================

        private Dictionary<int, int> CantidadesComanda()
            => _comandaActual.ToDictionary(i => i.Producto.IdProducto, i => i.Cantidad);

        /// <summary>
        /// Etiqueta y botón de cada tarjeta según lo que aún se puede agregar, considerando lo que
        /// ya consume el resto de la comanda (insumos compartidos entre productos).
        /// </summary>
        private void ActualizarEstadoTarjetas()
        {
            var cantidades = CantidadesComanda();
            int limite = _disponibilidad.LimitePorProducto;

            foreach (var (idProducto, (lblEstado, btnAgregar)) in _controlesTarjeta)
            {
                int maximo = _disponibilidad.MaximoPara(idProducto, cantidades);
                cantidades.TryGetValue(idProducto, out int enComanda);
                int restante = Math.Max(0, maximo - enComanda);

                if (maximo == 0)
                {
                    lblEstado.Text = "Agotado";
                    lblEstado.ForeColor = TemaCafeteria.SemaforoRojo;
                    _toolTip.SetToolTip(lblEstado, "No hay insumos suficientes para preparar este producto con lo que lleva la venta.");
                }
                else if (restante == 0)
                {
                    lblEstado.Text = enComanda >= limite ? $"Límite: {limite} por venta" : "Máximo en la venta";
                    lblEstado.ForeColor = TemaCafeteria.CafePrimario;
                    _toolTip.SetToolTip(lblEstado, enComanda >= limite
                        ? $"Cada producto admite como máximo {limite} unidades por venta."
                        : "Los insumos disponibles no alcanzan para agregar más unidades.");
                }
                else
                {
                    lblEstado.Text = restante <= 5 ? $"Últimas {restante}" : $"Disponibles: {restante}";
                    lblEstado.ForeColor = restante <= 5 ? ColorTranslator.FromHtml("#B26A00") : TemaCafeteria.SemaforoVerde;
                    _toolTip.SetToolTip(lblEstado, $"Puedes agregar {restante} más (máximo {limite} por producto, según los insumos de la receta).");
                }

                btnAgregar.Enabled = restante > 0;
                btnAgregar.BackColor = restante > 0 ? TemaCafeteria.CafePrimario : TemaCafeteria.SemaforoBlanco;
            }
        }

        /// <summary>True si aún cabe una unidad más del producto en la comanda.</summary>
        private bool PuedeAgregarUno(int idProducto)
            => _disponibilidad.RestantePara(idProducto, CantidadesComanda()) > 0;

        private PanelRedondeado CrearTarjetaProducto(Ent.Producto prod, int ancho)
        {
            PanelRedondeado card = new PanelRedondeado
            {
                Size = new Size(ancho, 226),
                Margin = new Padding(0, 0, 12, 12),
                RadioBorde = 14,
                BackColor = Color.White
            };

            // Cabecera visual (foto del producto con etiqueta Disponible)
            Panel pnlImagen = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(ancho - 20, 104),
                BackColor = ObtenerColorFondoCategoria(prod.NombreCategoria)
            };

            Label lblDisponible = new Label
            {
                Text = "Disponibles",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TemaCafeteria.SemaforoVerde,
                BackColor = Color.White,
                Location = new Point(8, 8),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true
            };

            Image? imagen = ObtenerImagen(prod.ImagenRuta);
            if (imagen != null)
            {
                PictureBox picProducto = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Image = imagen
                };
                pnlImagen.Controls.Add(picProducto);
            }
            else
            {
                Label lblIconoCentral = new Label
                {
                    Text = prod.NombreCategoria == CatCalientes ? "☕" : prod.NombreCategoria == CatFrias ? "🥤" : "🥐",
                    Font = new Font("Segoe UI", 28F),
                    ForeColor = TemaCafeteria.CafePrimario,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                pnlImagen.Controls.Add(lblIconoCentral);
            }

            pnlImagen.Controls.Add(lblDisponible);
            lblDisponible.BringToFront();

            Label lblNombre = new Label
            {
                Text = prod.Nombre,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(12, 122),
                Size = new Size(ancho - 24, 22)
            };

            Label lblDesc = new Label
            {
                Text = prod.NombreCategoria,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(12, 144),
                Size = new Size(ancho - 24, 28)
            };

            Label lblPrecio = new Label
            {
                Text = prod.Precio.ToString("C2", _culturaMxn),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(12, 182),
                AutoSize = true
            };

            Button btnAgregar = new Button
            {
                Text = "+ Agregar",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = TemaCafeteria.CafePrimario,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(88, 30),
                Location = new Point(ancho - 100, 178),
                Cursor = Cursors.Hand
            };
            btnAgregar.FlatAppearance.BorderSize = 0;
            btnAgregar.Click += (s, e) => AgregarProductoAComanda(prod);

            _controlesTarjeta[prod.IdProducto] = (lblDisponible, btnAgregar);

            card.Controls.AddRange(new Control[] { pnlImagen, lblNombre, lblDesc, lblPrecio, btnAgregar });
            return card;
        }

        /// <summary>
        /// Carga la imagen una sola vez y la guarda en memoria (el catálogo se redibuja al cambiar de tamaño).
        /// </summary>
        private Image? ObtenerImagen(string? archivo)
        {
            if (string.IsNullOrWhiteSpace(archivo))
                return null;

            if (_cacheImagenes.TryGetValue(archivo, out var enCache))
                return enCache;

            string ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Productos", archivo);
            if (!File.Exists(ruta))
                return null;

            try
            {
                using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read);
                using var original = Image.FromStream(fs);
                var copia = new Bitmap(original);
                _cacheImagenes[archivo] = copia;
                return copia;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Color ObtenerColorFondoCategoria(string categoria) => categoria switch
        {
            CatCalientes => ColorTranslator.FromHtml("#EFE3D6"),
            CatFrias => ColorTranslator.FromHtml("#E3ECEB"),
            _ => ColorTranslator.FromHtml("#F4E7DA")
        };

        private void ConstruirPanelComandaDerecho()
        {
            _pnlDerecho = new Panel
            {
                BackColor = Color.White
            };
            _pnlDerecho.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, 0, 0, _pnlDerecho.Height);
            };

            _lblNumComanda = new Label
            {
                Text = "NUEVA VENTA",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(22, 18),
                AutoSize = true
            };

            _lblTituloComanda = new Label
            {
                Text = "Venta actual",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(20, 36),
                AutoSize = true
            };

            _lblBadgeArticulos = new Label
            {
                Text = "0 artículos",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                BackColor = ColorTranslator.FromHtml("#F4ECE4"),
                Padding = new Padding(8, 4, 8, 4),
                AutoSize = true
            };

            // Encabezados de tabla de comanda
            Label colProd = new Label { Name = "colProd", Text = "PRODUCTO", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(22, 74), AutoSize = true };
            Label colCant = new Label { Name = "colCant", Text = "CANT.", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(215, 74), AutoSize = true };
            Label colImp = new Label { Name = "colImp", Text = "IMPORTE", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, AutoSize = true };

            _flowComanda = new FlowLayoutPanel
            {
                Location = new Point(18, 98),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White
            };

            // Sección inferior fija de cobro
            Panel pnlCobro = new Panel
            {
                Name = "pnlCobro",
                BackColor = Color.White
            };
            pnlCobro.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 18, 0, pnlCobro.Width - 18, 0);
            };

            Label lblSubTxt = new Label { Text = "Subtotal", Font = new Font("Segoe UI", 9.5F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(22, 12), AutoSize = true };
            _lblSubtotalValor = new Label { Text = "$0.00", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, AutoSize = true };

            Label lblTotTxt = new Label { Text = "Total", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, Location = new Point(22, 36), AutoSize = true };
            _lblTotalValor = new Label { Text = "$0.00", Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = TemaCafeteria.CafePrimario, AutoSize = true };

            // Tarjeta Método de pago: Efectivo
            Label lblMetodoTit = new Label { Text = "MÉTODO DE PAGO", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(22, 74), AutoSize = true };
            PanelRedondeado cardEfectivo = new PanelRedondeado
            {
                Name = "cardEfectivo",
                Location = new Point(22, 94),
                Height = 50,
                RadioBorde = 12,
                BackColor = ColorTranslator.FromHtml("#F9F5F0")
            };

            Label lblEfTit = new Label { Text = "💵  Efectivo", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoOscuro, Location = new Point(14, 7), AutoSize = true };
            Label lblEfSub = new Label { Text = "Cobro en caja", Font = new Font("Segoe UI", 8F), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(38, 27), AutoSize = true };
            Label lblCheck = new Label { Name = "lblCheck", Text = "✓", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.White, BackColor = TemaCafeteria.CafePrimario, Size = new Size(24, 24), TextAlign = ContentAlignment.MiddleCenter };
            cardEfectivo.Controls.AddRange(new Control[] { lblEfTit, lblEfSub, lblCheck });

            // Monto recibido y Cambio
            Label lblMontoTit = new Label
            {
                Text = "MONTO RECIBIDO",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(20, 150),
                AutoSize = true
            };

            Panel pnlMonto = new Panel
            {
                Name = "pnlMonto",
                Location = new Point(20, 170),
                Size = new Size(215, 48),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblSigno = new Label
            {
                Name = "lblSigno",
                Text = "$",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(10, 12),
                AutoSize = true
            };

            _txtMontoRecibido = new TextBox
            {
                Text = "",
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                Location = new Point(38, 11),
                Width = 125,
                Height = 28
            };
            _txtMontoRecibido.TextChanged += (s, e) => ActualizarTotalesYCambio();

            Label lblMxn = new Label
            {
                Name = "lblMxn",
                Text = "MXN",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(172, 15),
                AutoSize = true
            };

            pnlMonto.Controls.AddRange(new Control[] { lblSigno, _txtMontoRecibido, lblMxn });

            PanelRedondeado cardCambio = new PanelRedondeado
            {
                Name = "cardCambio",
                Location = new Point(246, 170),
                Height = 48,
                RadioBorde = 10,
                BackColor = ColorTranslator.FromHtml("#EAF3EE")
            };
            Label lblCambioTit = new Label { Text = "Cambio", Font = new Font("Segoe UI", 8F), ForeColor = TemaCafeteria.SemaforoVerde, Location = new Point(10, 5), AutoSize = true };
            _lblCambioValor = new Label { Text = "$0.00", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = TemaCafeteria.SemaforoVerde, Location = new Point(10, 22), AutoSize = true };
            cardCambio.Controls.AddRange(new Control[] { lblCambioTit, _lblCambioValor });

            // Botón Confirmar venta
            _btnConfirmarVenta = new Button
            {
                Text = "✓  Confirmar venta   —   $0.00",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(22, 230),
                Height = 46,
                Cursor = Cursors.Hand
            };
            _btnConfirmarVenta.FlatAppearance.BorderSize = 0;
            _btnConfirmarVenta.Click += BtnConfirmarVenta_Click;

            pnlCobro.Controls.AddRange(new Control[]
            {
                lblSubTxt, _lblSubtotalValor,
                lblTotTxt, _lblTotalValor,
                lblMetodoTit, cardEfectivo,
                lblMontoTit, pnlMonto, cardCambio,
                _btnConfirmarVenta
            });

            _pnlDerecho.Controls.AddRange(new Control[]
            {
                _lblNumComanda, _lblTituloComanda, _lblBadgeArticulos,
                colProd, colCant, colImp,
                _flowComanda, pnlCobro
            });
        }

        // =====================================================================
        // COMANDA
        // =====================================================================

        private void AgregarProductoAComanda(Ent.Producto prod)
        {
            // El botón ya se deshabilita al llegar al máximo; esta revisión cubre clics rápidos
            if (!PuedeAgregarUno(prod.IdProducto))
            {
                ActualizarEstadoTarjetas();
                return;
            }

            var existente = _comandaActual.FirstOrDefault(i => i.Producto.IdProducto == prod.IdProducto);
            if (existente != null)
            {
                existente.Cantidad++;
            }
            else
            {
                _comandaActual.Add(new ItemComanda { Producto = prod, Cantidad = 1 });
            }
            RefrescarComanda();
        }

        private void RefrescarComanda()
        {
            _flowComanda.SuspendLayout();
            _flowComanda.Controls.Clear();

            int anchoFila = Math.Max(330, _flowComanda.ClientSize.Width - 10);

            // Posiciones calculadas desde la derecha: [nombre] [−] [cantidad] [+] [importe] [borrar]
            // La cantidad tiene 40 px para que quepan 2 dígitos aun con escalado de pantalla (125 % / 150 %).
            const int anchoCantidad = 40;
            int xBorrar = anchoFila - 30;
            int xImporte = xBorrar - 74;
            int xMas = xImporte - 32;
            int xCantidad = xMas - anchoCantidad - 2;
            int xMenos = xCantidad - 28;

            if (_pnlDerecho.Controls["colCant"] is Label colCant)
                colCant.Left = _flowComanda.Left + xCantidad + (anchoCantidad - colCant.Width) / 2;

            var cantidades = CantidadesComanda();

            foreach (var item in _comandaActual.ToList())
            {
                int maximo = _disponibilidad.MaximoPara(item.Producto.IdProducto, cantidades);
                bool enMaximo = item.Cantidad >= maximo;

                Panel fila = new Panel
                {
                    Size = new Size(anchoFila, 54),
                    Margin = new Padding(0, 0, 0, 6),
                    BackColor = Color.White
                };
                fila.Paint += (s, e) =>
                {
                    using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                    e.Graphics.DrawLine(pen, 4, fila.Height - 1, fila.Width - 4, fila.Height - 1);
                };

                Label lblNombre = new Label
                {
                    Text = item.Producto.Nombre,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = TemaCafeteria.TextoOscuro,
                    Location = new Point(6, 6),
                    Size = new Size(Math.Max(60, xMenos - 10), 20),
                    AutoEllipsis = true
                };

                Label lblPrecioUnit = new Label
                {
                    Text = enMaximo
                        ? $"{item.Producto.Precio.ToString("C2", _culturaMxn)} c/u · máx. {maximo}"
                        : $"{item.Producto.Precio.ToString("C2", _culturaMxn)} c/u",
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = enMaximo ? ColorTranslator.FromHtml("#B26A00") : TemaCafeteria.TextoSecundario,
                    Location = new Point(6, 26),
                    AutoSize = true
                };

                Button btnMenos = new Button
                {
                    Text = "−",
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    BackColor = TemaCafeteria.FondoCrema,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(26, 26),
                    Location = new Point(xMenos, 12),
                    Cursor = Cursors.Hand
                };
                btnMenos.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
                btnMenos.Click += (s, e) =>
                {
                    if (item.Cantidad > 1) item.Cantidad--;
                    else _comandaActual.Remove(item);
                    RefrescarComanda();
                };

                Label lblCant = new Label
                {
                    Text = item.Cantidad.ToString(CultureInfo.InvariantCulture),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    Location = new Point(xCantidad, 12),
                    Size = new Size(anchoCantidad, 26),
                    AutoSize = false,
                    AutoEllipsis = false,
                    UseCompatibleTextRendering = false,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Button btnMas = new Button
                {
                    Text = "+",
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    BackColor = TemaCafeteria.FondoCrema,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(26, 26),
                    Location = new Point(xMas, 12),
                    Cursor = enMaximo ? Cursors.Default : Cursors.Hand,
                    Enabled = !enMaximo
                };
                btnMas.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
                _toolTip.SetToolTip(btnMas, enMaximo
                    ? (maximo >= _disponibilidad.LimitePorProducto
                        ? $"Máximo {_disponibilidad.LimitePorProducto} unidades por producto en cada venta."
                        : "Los insumos disponibles no alcanzan para más unidades.")
                    : "Agregar una unidad");
                btnMas.Click += (s, e) =>
                {
                    if (!PuedeAgregarUno(item.Producto.IdProducto))
                    {
                        RefrescarComanda();
                        return;
                    }
                    item.Cantidad++;
                    RefrescarComanda();
                };

                Button btnBorrar = new Button
                {
                    Text = "🗑",
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = TemaCafeteria.TextoSecundario,
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(28, 28),
                    Location = new Point(xBorrar, 11),
                    Cursor = Cursors.Hand
                };
                btnBorrar.FlatAppearance.BorderSize = 0;
                btnBorrar.Click += (s, e) =>
                {
                    _comandaActual.Remove(item);
                    RefrescarComanda();
                };

                Label lblImporte = new Label
                {
                    Text = item.Importe.ToString("C2", _culturaMxn),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = TemaCafeteria.TextoOscuro,
                    Location = new Point(xImporte, 15),
                    Size = new Size(72, 22),
                    TextAlign = ContentAlignment.MiddleRight
                };

                fila.Controls.AddRange(new Control[]
                {
                    lblNombre, lblPrecioUnit, btnMenos, lblCant, btnMas, lblImporte, btnBorrar
                });
                _flowComanda.Controls.Add(fila);
            }

            _flowComanda.ResumeLayout();

            int totalArticulos = _comandaActual.Sum(i => i.Cantidad);
            _lblBadgeArticulos.Text = $"{totalArticulos} {(totalArticulos == 1 ? "artículo" : "artículos")}";
            ActualizarTotalesYCambio();
            ActualizarEstadoTarjetas();
        }

        private void ActualizarTotalesYCambio()
        {
            decimal total = _comandaActual.Sum(i => i.Importe);
            string totalFormateado = total.ToString("C2", _culturaMxn);

            _lblSubtotalValor.Text = totalFormateado;
            _lblTotalValor.Text = totalFormateado;
            _btnConfirmarVenta.Text = $"✓  Confirmar venta   —   {totalFormateado}";

            if (_pnlDerecho.Controls["pnlCobro"] is Panel pnlCobro)
            {
                _lblSubtotalValor.Location = new Point(pnlCobro.Width - _lblSubtotalValor.Width - 22, 12);
                _lblTotalValor.Location = new Point(pnlCobro.Width - _lblTotalValor.Width - 22, 34);
            }

            if (EntradaNumerica.TryLeerImporte(_txtMontoRecibido.Text, out decimal recibido, out _))
            {
                decimal cambio = Ent.Redondeo.Importe(recibido - total);
                if (cambio >= 0)
                {
                    _lblCambioValor.Text = cambio.ToString("C2", _culturaMxn);
                    _lblCambioValor.ForeColor = TemaCafeteria.SemaforoVerde;
                }
                else
                {
                    _lblCambioValor.Text = "Falta pago";
                    _lblCambioValor.ForeColor = ColorTranslator.FromHtml("#D32F2F");
                }
            }
            else
            {
                _lblCambioValor.Text = "$0.00";
                _lblCambioValor.ForeColor = TemaCafeteria.SemaforoVerde;
            }
        }

        // =====================================================================
        // COBRO
        // =====================================================================

        private void BtnConfirmarVenta_Click(object? sender, EventArgs e)
        {
            if (_comandaActual.Count == 0)
            {
                MessageBox.Show("Agrega al menos un producto a la comanda actual.", "Comanda vacía", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal total = _comandaActual.Sum(i => i.Importe);
            if (!EntradaNumerica.TryLeerImporte(_txtMontoRecibido.Text, out decimal recibido, out string errorMonto))
            {
                MessageBox.Show($"Monto recibido: {errorMonto}", "Monto inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtMontoRecibido.Focus();
                return;
            }

            if (recibido < total)
            {
                MessageBox.Show("El monto recibido en efectivo debe cubrir el total de la venta.", "Cobro insuficiente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtMontoRecibido.Focus();
                return;
            }

            var productos = _comandaActual
                .Select(i => (i.Producto.IdProducto, i.Cantidad))
                .ToList();

            _btnConfirmarVenta.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                var resultado = _ventaNegocio.RegistrarVenta(productos, "Efectivo", recibido);

                if (!resultado.Exito || resultado.Venta == null)
                {
                    MessageBox.Show(resultado.Mensaje, "No se pudo registrar la venta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var venta = resultado.Venta;
                _ultimoFolio = venta.Folio;

                MessageBox.Show(
                    $"Venta {venta.Folio} registrada con éxito.\n\n" +
                    $"• Total cobrado: {venta.Total.ToString("C2", _culturaMxn)}\n" +
                    $"• Recibido: {recibido.ToString("C2", _culturaMxn)}\n" +
                    $"• Cambio a entregar: {(venta.Cambio ?? 0m).ToString("C2", _culturaMxn)}\n" +
                    $"• Inventario descontado bajo método PEPS.",
                    "Venta confirmada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _lblNumComanda.Text = $"NUEVA VENTA  ·  ÚLTIMA: {_ultimoFolio}";
                _comandaActual.Clear();
                _txtMontoRecibido.Clear();

                // Recargar por si el administrador cambió precios o productos
                CargarCatalogo();
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnConfirmarVenta.Enabled = true;
            }
        }

        // =====================================================================
        // LAYOUT
        // =====================================================================

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            _lblFechaHora.Location = new Point(_pnlTopBar.Width - _lblFechaHora.Width - 32, 22);

            int topContenido = _pnlTopBar.Height;
            int altoContenido = Math.Max(520, ClientSize.Height - topContenido);

            // 1. Anclar siempre el panel derecho de cobro dentro del ancho visible
            int anchoDerecho = Math.Clamp((int)(ClientSize.Width * 0.36), 375, 430);
            if (ClientSize.Width - anchoDerecho < 420)
                anchoDerecho = Math.Max(350, ClientSize.Width - 420);

            _pnlDerecho.Bounds = new Rectangle(ClientSize.Width - anchoDerecho, topContenido, anchoDerecho, altoContenido);

            // 2. Ajustar el catálogo izquierdo al espacio restante exacto
            int margenIzq = 22;
            int anchoIzq = Math.Max(360, ClientSize.Width - anchoDerecho - (margenIzq * 2));
            _pnlIzquierdo.Bounds = new Rectangle(margenIzq, topContenido + 16, anchoIzq, altoContenido - 22);

            _lblBadgeContador.Location = new Point(anchoIzq - _lblBadgeContador.Width - 4, 14);
            _pnlTabsCategorias.Width = anchoIzq - 4;

            int anchoTab = (_pnlTabsCategorias.Width - 16) / 3;
            _btnTabCalientes.Bounds = new Rectangle(5, 5, anchoTab, 36);
            _btnTabFrias.Bounds = new Rectangle(8 + anchoTab, 5, anchoTab, 36);
            _btnTabReposteria.Bounds = new Rectangle(11 + (anchoTab * 2), 5, anchoTab, 36);

            _flowProductos.Size = new Size(anchoIzq, _pnlIzquierdo.Height - 116);
            RenderizarProductos();

            // 3. Ajustar controles internos de la comanda y cobro (Monto recibido + MXN + Cambio)
            _lblBadgeArticulos.Location = new Point(anchoDerecho - _lblBadgeArticulos.Width - 20, 38);
            if (_pnlDerecho.Controls["colImp"] is Label colImp)
                colImp.Location = new Point(anchoDerecho - colImp.Width - 54, 74);

            int altoCobro = 290;
            _flowComanda.Size = new Size(anchoDerecho - 32, Math.Max(140, altoContenido - 104 - altoCobro));

            if (_pnlDerecho.Controls["pnlCobro"] is Panel pnlCobro)
            {
                pnlCobro.Bounds = new Rectangle(0, altoContenido - altoCobro, anchoDerecho, altoCobro);
                if (pnlCobro.Controls["cardEfectivo"] is Panel cardEf)
                {
                    cardEf.Width = anchoDerecho - 40;
                    if (cardEf.Controls["lblCheck"] is Label chk)
                        chk.Location = new Point(cardEf.Width - 36, 13);
                }

                if (pnlCobro.Controls["pnlMonto"] is Panel pnlM && pnlCobro.Controls["cardCambio"] is Panel cardC)
                {
                    int anchoTotalFila = anchoDerecho - 40;
                    int anchoMonto = (int)(anchoTotalFila * 0.58);
                    pnlM.Location = new Point(20, 170);
                    pnlM.Width = anchoMonto;

                    if (pnlM.Controls["lblMxn"] is Label lblMxn)
                        lblMxn.Location = new Point(anchoMonto - lblMxn.Width - 10, 13);

                    _txtMontoRecibido.Width = Math.Max(75, anchoMonto - 78);

                    cardC.Location = new Point(20 + anchoMonto + 10, 170);
                    cardC.Width = anchoTotalFila - anchoMonto - 10;
                }

                _btnConfirmarVenta.Left = 20;
                _btnConfirmarVenta.Width = anchoDerecho - 40;
            }

            RefrescarComanda();
        }
    }
}