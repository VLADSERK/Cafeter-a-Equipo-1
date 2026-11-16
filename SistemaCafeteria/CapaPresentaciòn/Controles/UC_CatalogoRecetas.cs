using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Estilos;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_CatalogoRecetas : UserControl
    {
        private class IngredienteRecetaMock
        {
            public string Insumo { get; set; } = "";
            public decimal Cantidad { get; set; }
            public string Unidad { get; set; } = "";
            public decimal CostoUnitarioReferencia { get; set; }
            public decimal SubtotalCosto => Math.Round(Cantidad * CostoUnitarioReferencia, 2);
        }

        private class ProductoAdminMock
        {
            public string Nombre { get; set; } = "";
            public string Descripcion { get; set; } = "";
            public string Categoria { get; set; } = "";
            public decimal PrecioVenta { get; set; }
            public string CodigoBarras { get; set; } = "-";
            public string ImagenArchivo { get; set; } = "";
            public bool Disponible { get; set; } = true;
            public List<IngredienteRecetaMock> Receta { get; set; } = new();
        }

        private readonly List<ProductoAdminMock> _productos = new();
        private ProductoAdminMock? _productoSeleccionado;
        private readonly CultureInfo _culturaMxn = CultureInfo.GetCultureInfo("es-MX");

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;

        // Columna izquierda: Catálogo de productos vendibles
        private PanelRedondeado _cardCatalogo = null!;
        private TextBox _txtBuscarProd = null!;
        private ComboBox _cmbFiltroCat = null!;
        private Button _btnNuevoProducto = null!;
        private DataGridView _dgvProductos = null!;

        // Columna derecha: Ficha de producto + Editor de receta
        private PanelRedondeado _cardDetalle = null!;
        private PictureBox _picProducto = null!;
        private TextBox _txtNombreProd = null!;
        private ComboBox _cmbCategoriaProd = null!;
        private TextBox _txtPrecioProd = null!;
        private TextBox _txtCodigoBarras = null!;
        private CheckBox _chkDisponible = null!;
        private Button _btnGuardarProducto = null!;

        // Sección inferior derecha: Tabla de receta e insumos
        private ComboBox _cmbNuevoInsumo = null!;
        private TextBox _txtCantidadInsumo = null!;
        private Label _lblUnidadInsumo = null!;
        private Button _btnAgregarInsumo = null!;
        private DataGridView _dgvReceta = null!;
        private Label _lblCostoRecetaVal = null!;
        private Label _lblMargenVal = null!;

        public UC_CatalogoRecetas()
        {
            
            InicializarCatalogoConRecetas();
            ConfigurarInterfaz();
            FiltrarProductos();

            if (_productos.Count > 0)
            {
                SeleccionarProducto(_productos[0]);
            }
        }

        private void InicializarCatalogoConRecetas()
        {
            _productos.AddRange(new[]
            {
                new ProductoAdminMock
                {
                    Nombre = "Cappuccino",
                    Descripcion = "Espresso, leche y espuma",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 58.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "capuchino.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café en Grano", Cantidad = 0.018m, Unidad = "kg", CostoUnitarioReferencia = 340m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.220m, Unidad = "L", CostoUnitarioReferencia = 26m },
                        new() { Insumo = "Vaso Desechable 12 oz", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 2.20m },
                        new() { Insumo = "Tapa para Vaso", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 1.10m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Latte",
                    Descripcion = "Espresso doble con leche vaporizada",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 60.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "latte.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café en Grano", Cantidad = 0.018m, Unidad = "kg", CostoUnitarioReferencia = 340m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.260m, Unidad = "L", CostoUnitarioReferencia = 26m },
                        new() { Insumo = "Vaso Desechable 12 oz", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 2.20m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Americano",
                    Descripcion = "Doble espresso y agua caliente",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 42.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "americano.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café en Grano", Cantidad = 0.018m, Unidad = "kg", CostoUnitarioReferencia = 340m },
                        new() { Insumo = "Vaso Desechable 12 oz", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 2.20m },
                        new() { Insumo = "Tapa para Vaso", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 1.10m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Espresso",
                    Descripcion = "Carga intensa de café de especialidad",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 38.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "expresso.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café en Grano", Cantidad = 0.018m, Unidad = "kg", CostoUnitarioReferencia = 340m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Moka Caliente",
                    Descripcion = "Chocolate, espresso y leche",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 68.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "mokaC.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café en Grano", Cantidad = 0.018m, Unidad = "kg", CostoUnitarioReferencia = 340m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.220m, Unidad = "L", CostoUnitarioReferencia = 26m },
                        new() { Insumo = "Jarabe de Chocolate", Cantidad = 0.030m, Unidad = "L", CostoUnitarioReferencia = 145m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Chocolate Caliente",
                    Descripcion = "Cacao artesanal y leche cremosa",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 56.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "chocolateC.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Cacao en Polvo", Cantidad = 0.025m, Unidad = "kg", CostoUnitarioReferencia = 210m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.250m, Unidad = "L", CostoUnitarioReferencia = 26m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Matcha Latte",
                    Descripcion = "Té verde matcha ceremonial y leche",
                    Categoria = "Bebidas Calientes",
                    PrecioVenta = 65.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "MatchaLatte.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Té Matcha en Polvo", Cantidad = 0.012m, Unidad = "kg", CostoUnitarioReferencia = 680m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.240m, Unidad = "L", CostoUnitarioReferencia = 26m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Latte Frío",
                    Descripcion = "Espresso, leche fría y hielo",
                    Categoria = "Bebidas Frías",
                    PrecioVenta = 64.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "LatteF.jpeg",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café en Grano", Cantidad = 0.018m, Unidad = "kg", CostoUnitarioReferencia = 340m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.220m, Unidad = "L", CostoUnitarioReferencia = 26m },
                        new() { Insumo = "Hielo en Cubos", Cantidad = 0.150m, Unidad = "kg", CostoUnitarioReferencia = 12m },
                        new() { Insumo = "Vaso Plástico Frío 16 oz", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 2.80m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Cold Brew",
                    Descripcion = "Extracción en frío por 18 horas",
                    Categoria = "Bebidas Frías",
                    PrecioVenta = 55.00m,
                    CodigoBarras = "7501002348112",
                    ImagenArchivo = "ColdBrew.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Café Descafeinado", Cantidad = 0.025m, Unidad = "kg", CostoUnitarioReferencia = 350m },
                        new() { Insumo = "Hielo en Cubos", Cantidad = 0.180m, Unidad = "kg", CostoUnitarioReferencia = 12m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Frappé Moka",
                    Descripcion = "Café, cacao y crema batida",
                    Categoria = "Bebidas Frías",
                    PrecioVenta = 74.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "FrappeMoka.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Base Neutro Frappé", Cantidad = 0.045m, Unidad = "kg", CostoUnitarioReferencia = 160m },
                        new() { Insumo = "Jarabe de Chocolate", Cantidad = 0.030m, Unidad = "L", CostoUnitarioReferencia = 145m },
                        new() { Insumo = "Crema Batida", Cantidad = 0.035m, Unidad = "L", CostoUnitarioReferencia = 95m },
                        new() { Insumo = "Hielo en Cubos", Cantidad = 0.200m, Unidad = "kg", CostoUnitarioReferencia = 12m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Matcha Iced Latte",
                    Descripcion = "Té verde matcha frío y leche",
                    Categoria = "Bebidas Frías",
                    PrecioVenta = 68.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "MatchaIcedLatte.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Té Matcha en Polvo", Cantidad = 0.012m, Unidad = "kg", CostoUnitarioReferencia = 680m },
                        new() { Insumo = "Leche Deslactosada", Cantidad = 0.220m, Unidad = "L", CostoUnitarioReferencia = 29m },
                        new() { Insumo = "Hielo en Cubos", Cantidad = 0.150m, Unidad = "kg", CostoUnitarioReferencia = 12m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Chocolate Frío",
                    Descripcion = "Cacao artesanal frío con hielo",
                    Categoria = "Bebidas Frías",
                    PrecioVenta = 58.00m,
                    CodigoBarras = "-",
                    ImagenArchivo = "ChocolateF.jpeg",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Cacao en Polvo", Cantidad = 0.025m, Unidad = "kg", CostoUnitarioReferencia = 210m },
                        new() { Insumo = "Leche Entera", Cantidad = 0.230m, Unidad = "L", CostoUnitarioReferencia = 26m },
                        new() { Insumo = "Hielo en Cubos", Cantidad = 0.150m, Unidad = "kg", CostoUnitarioReferencia = 12m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Galleta de Chocolate",
                    Descripcion = "Chispas de chocolate semiamargo",
                    Categoria = "Repostería",
                    PrecioVenta = 32.00m,
                    CodigoBarras = "7501234567817",
                    ImagenArchivo = "GalletaCh.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Harina de Trigo", Cantidad = 0.060m, Unidad = "kg", CostoUnitarioReferencia = 24m },
                        new() { Insumo = "Mantequilla Sin Sal", Cantidad = 0.025m, Unidad = "kg", CostoUnitarioReferencia = 180m },
                        new() { Insumo = "Chispas de Chocolate", Cantidad = 0.030m, Unidad = "kg", CostoUnitarioReferencia = 195m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Brownie de Chocolate",
                    Descripcion = "Intenso cacao con nuez tostada",
                    Categoria = "Repostería",
                    PrecioVenta = 48.00m,
                    CodigoBarras = "7501234567824",
                    ImagenArchivo = "BrowniedeCh.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Cacao en Polvo", Cantidad = 0.030m, Unidad = "kg", CostoUnitarioReferencia = 210m },
                        new() { Insumo = "Mantequilla Sin Sal", Cantidad = 0.035m, Unidad = "kg", CostoUnitarioReferencia = 180m },
                        new() { Insumo = "Huevo Blanco", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 3.20m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Cheesecake de Fresa",
                    Descripcion = "Base de galleta con mermelada natural",
                    Categoria = "Repostería",
                    PrecioVenta = 65.00m,
                    CodigoBarras = "7501234567831",
                    ImagenArchivo = "CheesecakeFresa.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Queso Crema", Cantidad = 0.085m, Unidad = "kg", CostoUnitarioReferencia = 155m },
                        new() { Insumo = "Mermelada de Fresa", Cantidad = 0.030m, Unidad = "kg", CostoUnitarioReferencia = 90m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Croissant Mantequilla",
                    Descripcion = "Hojaldre francés recién horneado",
                    Categoria = "Repostería",
                    PrecioVenta = 45.00m,
                    CodigoBarras = "7501234567893",
                    ImagenArchivo = "CroissantM.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Harina de Trigo", Cantidad = 0.080m, Unidad = "kg", CostoUnitarioReferencia = 24m },
                        new() { Insumo = "Mantequilla Sin Sal", Cantidad = 0.045m, Unidad = "kg", CostoUnitarioReferencia = 180m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Dona Glaseada",
                    Descripcion = "Esponjosa con glaseado de vainilla",
                    Categoria = "Repostería",
                    PrecioVenta = 35.00m,
                    CodigoBarras = "7501234567855",
                    ImagenArchivo = "DonaGlaseada.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Harina de Trigo", Cantidad = 0.065m, Unidad = "kg", CostoUnitarioReferencia = 24m },
                        new() { Insumo = "Azúcar Refinada", Cantidad = 0.025m, Unidad = "kg", CostoUnitarioReferencia = 28m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Muffin de Arándanos",
                    Descripcion = "Horneado con arándanos frescos",
                    Categoria = "Repostería",
                    PrecioVenta = 44.00m,
                    CodigoBarras = "7501234567862",
                    ImagenArchivo = "MuffinArandanos.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Harina de Trigo", Cantidad = 0.070m, Unidad = "kg", CostoUnitarioReferencia = 24m },
                        new() { Insumo = "Huevo Blanco", Cantidad = 1m, Unidad = "pza", CostoUnitarioReferencia = 3.20m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Panqué de Plátano",
                    Descripcion = "Plátanos maduros con nuez",
                    Categoria = "Repostería",
                    PrecioVenta = 40.00m,
                    CodigoBarras = "7501234567879",
                    ImagenArchivo = "PanquedePlatano.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Harina de Trigo", Cantidad = 0.075m, Unidad = "kg", CostoUnitarioReferencia = 24m },
                        new() { Insumo = "Canela Molida", Cantidad = 0.003m, Unidad = "kg", CostoUnitarioReferencia = 240m }
                    }
                },
                new ProductoAdminMock
                {
                    Nombre = "Rol de Canela",
                    Descripcion = "Glaseado cremoso de queso crema",
                    Categoria = "Repostería",
                    PrecioVenta = 52.00m,
                    CodigoBarras = "7501234567886",
                    ImagenArchivo = "RoldeC.png",
                    Receta = new List<IngredienteRecetaMock>
                    {
                        new() { Insumo = "Harina de Trigo", Cantidad = 0.085m, Unidad = "kg", CostoUnitarioReferencia = 24m },
                        new() { Insumo = "Canela Molida", Cantidad = 0.005m, Unidad = "kg", CostoUnitarioReferencia = 240m },
                        new() { Insumo = "Queso Crema", Cantidad = 0.030m, Unidad = "kg", CostoUnitarioReferencia = 155m }
                    }
                }
            });
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
                Text = "ADMINISTRACIÓN / CATÁLOGO Y RECETAS PEPS",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(32, 14),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Catálogo y recetas",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(30, 32),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Administra tu catálogo, código de barras, define las recetas y configura los insumos de tu cafetería.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(32, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });

            ConstruirPanelCatalogoIzquierdo();
            ConstruirPanelDetalleDerecho();

            Controls.AddRange(new Control[] { _cardCatalogo, _cardDetalle, _pnlTopBar });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private void ConstruirPanelCatalogoIzquierdo()
        {
            _cardCatalogo = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label lblTitCat = new Label
            {
                Text = "Catálogo de productos (20)",
                Name = "lblTitCat",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(20, 18),
                AutoSize = true
            };

            Label lblSubPrecios = new Label
            {
                Name = "lblSubPrecios",
                Text = "Precios en MXN",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(22, 42),
                AutoSize = true
            };

            _btnNuevoProducto = new Button
            {
                Text = "+ Nuevo",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(92, 32),
                Cursor = Cursors.Hand
            };
            _btnNuevoProducto.FlatAppearance.BorderSize = 0;
            _btnNuevoProducto.Click += BtnNuevoProducto_Click;

            _txtBuscarProd = new TextBox
            {
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "Buscar por nombre o código de barras...",
                Location = new Point(20, 68),
                Height = 30
            };
            _txtBuscarProd.TextChanged += (s, e) => FiltrarProductos();

            _cmbFiltroCat = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(20, 102),
                Height = 32
            };
            _cmbFiltroCat.Items.AddRange(new object[] { "Todas las categorías", "Bebidas Calientes", "Bebidas Frías", "Repostería" });
            _cmbFiltroCat.SelectedIndex = 0;
            _cmbFiltroCat.SelectedIndexChanged += (s, e) => FiltrarProductos();

            _dgvProductos = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 40 },
                EnableHeadersVisualStyles = false,
                GridColor = TemaCafeteria.BordeSuave,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _dgvProductos.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Padding = new Padding(6, 0, 6, 0)
            };

            _dgvProductos.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 8.5F),
                SelectionBackColor = ColorTranslator.FromHtml("#EFE6DC"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(6, 0, 6, 0)
            };

            // Las 5 columnas exactas de tu diseño: Nombre | Categoría | Precio | Estado | Código de barras
            _dgvProductos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProdNombre", HeaderText = "Nombre", FillWeight = 28 });
            _dgvProductos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProdCat", HeaderText = "Categoría", FillWeight = 22 });
            _dgvProductos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProdPrecio", HeaderText = "Precio", FillWeight = 15 });
            _dgvProductos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProdEstado", HeaderText = "Estado", FillWeight = 14 });
            _dgvProductos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProdCodigo", HeaderText = "Código de barras", FillWeight = 23 });

            _dgvProductos.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvProductos.Columns["colProdEstado"]!.Index)
                {
                    string estado = e.Value?.ToString() ?? "Activo";
                    bool activo = estado == "Activo";
                    e.CellStyle!.ForeColor = activo ? ColorTranslator.FromHtml("#1F4E35") : ColorTranslator.FromHtml("#C62828");
                    e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }
            };

            _dgvProductos.SelectionChanged += (s, e) =>
            {
                if (_dgvProductos.SelectedRows.Count > 0)
                {
                    string nombre = _dgvProductos.SelectedRows[0].Cells["colProdNombre"].Value?.ToString() ?? "";
                    var prod = _productos.FirstOrDefault(p => p.Nombre == nombre);
                    if (prod != null) SeleccionarProducto(prod);
                }
            };

            _cardCatalogo.Controls.AddRange(new Control[] { lblTitCat, lblSubPrecios, _btnNuevoProducto, _txtBuscarProd, _cmbFiltroCat, _dgvProductos });
        }

        private void ConstruirPanelDetalleDerecho()
        {
            _cardDetalle = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            // Vista previa de foto
            _picProducto = new PictureBox
            {
                Location = new Point(24, 22),
                Size = new Size(118, 126),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#F7F3EE"),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblNom = new Label { Text = "Nombre del producto", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(156, 18), AutoSize = true };
            _txtNombreProd = new TextBox { Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(156, 38), Width = 220 };

            Label lblCod = new Label { Text = "Código de barras (EAN / SKU)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(390, 18), AutoSize = true };
            _txtCodigoBarras = new TextBox
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                PlaceholderText = "Ej. 7501234567893 o -",
                Location = new Point(390, 38),
                Width = 180
            };

            Label lblCat = new Label { Text = "Categoría", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(156, 74), AutoSize = true };
            _cmbCategoriaProd = new ComboBox { Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(156, 94), Width = 155 };
            _cmbCategoriaProd.Items.AddRange(new object[] { "Bebidas Calientes", "Bebidas Frías", "Repostería" });

            Label lblPrec = new Label { Text = "Precio ($)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(322, 74), AutoSize = true };
            _txtPrecioProd = new TextBox { Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(322, 94), Width = 82 };

            _chkDisponible = new CheckBox
            {
                Text = "Activo",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.SemaforoVerde,
                Location = new Point(414, 97),
                AutoSize = true,
                Checked = true
            };

            _btnGuardarProducto = new Button
            {
                Text = "Guardar",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1F4E35"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(92, 32),
                Location = new Point(485, 92),
                Cursor = Cursors.Hand
            };
            _btnGuardarProducto.FlatAppearance.BorderSize = 0;
            _btnGuardarProducto.Click += BtnGuardarProducto_Click;

            // Separador sección receta
            Panel sep = new Panel { Name = "sepReceta", Location = new Point(24, 162), Height = 1, BackColor = TemaCafeteria.BordeSuave };

            Label lblRecetaTit = new Label
            {
                Text = "Receta / Explosión de insumos (descuento PEPS por unidad vendida)",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 174),
                AutoSize = true
            };

            // Controles para agregar insumo a la receta
            _cmbNuevoInsumo = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(24, 204),
                Width = 225
            };
            foreach (var ins in CatalogoInsumosMock.ListaIngredientes)
            {
                _cmbNuevoInsumo.Items.Add(ins.Nombre);
            }

            _txtCantidadInsumo = new TextBox
            {
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "Cant. (0.020)",
                Location = new Point(258, 204),
                Width = 98
            };

            _lblUnidadInsumo = new Label
            {
                Text = "kg",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(362, 208),
                AutoSize = true
            };

            _cmbNuevoInsumo.SelectedIndexChanged += (s, e) =>
            {
                if (_lblUnidadInsumo == null) return;
                string sel = _cmbNuevoInsumo.SelectedItem?.ToString() ?? "";
                var loteInfo = CatalogoInsumosMock.ObtenerLotesIniciales().FirstOrDefault(l => l.Insumo == sel);
                _lblUnidadInsumo.Text = loteInfo != null ? loteInfo.Unidad : "kg";
            };
            if (_cmbNuevoInsumo.Items.Count > 0) _cmbNuevoInsumo.SelectedIndex = 0;

            _btnAgregarInsumo = new Button
            {
                Text = "+ Agregar a receta",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(146, 30),
                Location = new Point(402, 202),
                Cursor = Cursors.Hand
            };
            _btnAgregarInsumo.FlatAppearance.BorderSize = 0;
            _btnAgregarInsumo.Click += BtnAgregarInsumo_Click;

            // Tabla de ingredientes de la receta
            _dgvReceta = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 36,
                RowTemplate = { Height = 36 },
                EnableHeadersVisualStyles = false,
                GridColor = TemaCafeteria.BordeSuave,
                Location = new Point(24, 244)
            };

            _dgvReceta.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _dgvReceta.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRecInsumo", HeaderText = "Insumo de inventario", FillWeight = 38 });
            _dgvReceta.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRecCant", HeaderText = "Cantidad", FillWeight = 18 });
            _dgvReceta.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRecUnidad", HeaderText = "Unidad", FillWeight = 14 });
            _dgvReceta.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRecCosto", HeaderText = "Costo est.", FillWeight = 18 });

            var colQuitar = new DataGridViewButtonColumn
            {
                Name = "colRecQuitar",
                HeaderText = "Acción",
                Text = "Quitar",
                UseColumnTextForButtonValue = true,
                FillWeight = 14
            };
            _dgvReceta.Columns.Add(colQuitar);

            _dgvReceta.CellContentClick += (s, e) =>
            {
                if (_productoSeleccionado != null && e.RowIndex >= 0 && e.ColumnIndex == _dgvReceta.Columns["colRecQuitar"]!.Index)
                {
                    _productoSeleccionado.Receta.RemoveAt(e.RowIndex);
                    RefrescarTablaReceta();
                }
            };

            // Pie financiero de la receta
            _lblCostoRecetaVal = new Label
            {
                Text = "Costo insumos: $0.00",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                AutoSize = true
            };

            _lblMargenVal = new Label
            {
                Text = "Margen bruto: 0%",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.SemaforoVerde,
                AutoSize = true
            };

            _cardDetalle.Controls.AddRange(new Control[]
            {
                _picProducto, lblNom, _txtNombreProd, lblCod, _txtCodigoBarras,
                lblCat, _cmbCategoriaProd, lblPrec, _txtPrecioProd, _chkDisponible, _btnGuardarProducto,
                sep, lblRecetaTit,
                _cmbNuevoInsumo, _txtCantidadInsumo, _lblUnidadInsumo, _btnAgregarInsumo,
                _dgvReceta, _lblCostoRecetaVal, _lblMargenVal
            });
        }

        private void FiltrarProductos()
        {
            string texto = _txtBuscarProd.Text.Trim().ToLowerInvariant();
            string cat = _cmbFiltroCat.SelectedItem?.ToString() ?? "Todas las categorías";

            _dgvProductos.Rows.Clear();
            int conteo = 0;

            foreach (var p in _productos)
            {
                if (cat != "Todas las categorías" && p.Categoria != cat) continue;

                if (!string.IsNullOrEmpty(texto))
                {
                    bool coincideNombre = p.Nombre.ToLowerInvariant().Contains(texto);
                    bool coincideCodigo = p.CodigoBarras.ToLowerInvariant().Contains(texto);
                    if (!coincideNombre && !coincideCodigo) continue;
                }

                _dgvProductos.Rows.Add(
                    p.Nombre,
                    p.Categoria,
                    p.PrecioVenta.ToString("C2", _culturaMxn),
                    p.Disponible ? "Activo" : "Inactivo",
                    string.IsNullOrWhiteSpace(p.CodigoBarras) ? "-" : p.CodigoBarras
                );
                conteo++;
            }

            if (_cardCatalogo.Controls["lblTitCat"] is Label lbl)
                lbl.Text = $"Catálogo de productos ({conteo})";
        }

        private void SeleccionarProducto(ProductoAdminMock prod)
        {
            _productoSeleccionado = prod;
            _txtNombreProd.Text = prod.Nombre;
            _txtCodigoBarras.Text = prod.CodigoBarras;
            _cmbCategoriaProd.SelectedItem = prod.Categoria;
            _txtPrecioProd.Text = prod.PrecioVenta.ToString("0.00", CultureInfo.InvariantCulture);
            _chkDisponible.Checked = prod.Disponible;

            string rutaImg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Productos", prod.ImagenArchivo);
            if (!string.IsNullOrEmpty(prod.ImagenArchivo) && File.Exists(rutaImg))
            {
                using var fs = new FileStream(rutaImg, FileMode.Open, FileAccess.Read);
                _picProducto.Image = Image.FromStream(fs);
            }
            else
            {
                _picProducto.Image = null;
            }

            RefrescarTablaReceta();
        }

        private void RefrescarTablaReceta()
        {
            _dgvReceta.Rows.Clear();
            if (_productoSeleccionado == null) return;

            decimal costoTotal = 0m;
            foreach (var item in _productoSeleccionado.Receta)
            {
                _dgvReceta.Rows.Add(
                    item.Insumo,
                    item.Cantidad.ToString("0.###"),
                    item.Unidad,
                    item.SubtotalCosto.ToString("C2", _culturaMxn),
                    "Quitar"
                );
                costoTotal += item.SubtotalCosto;
            }

            decimal margen = _productoSeleccionado.PrecioVenta > 0
                ? ((_productoSeleccionado.PrecioVenta - costoTotal) / _productoSeleccionado.PrecioVenta) * 100m
                : 0m;

            _lblCostoRecetaVal.Text = $"Costo estimado de receta: {costoTotal.ToString("C2", _culturaMxn)}";
            _lblMargenVal.Text = $"Utilidad bruta: {(_productoSeleccionado.PrecioVenta - costoTotal).ToString("C2", _culturaMxn)} ({margen:N1}%)";
        }

        private void BtnAgregarInsumo_Click(object? sender, EventArgs e)
        {
            if (_productoSeleccionado == null) return;

            if (!decimal.TryParse(_txtCantidadInsumo.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cant) || cant <= 0)
            {
                MessageBox.Show("Ingresa una cantidad válida mayor a cero (por ejemplo: 0.020).", "Cantidad inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string insumo = _cmbNuevoInsumo.SelectedItem?.ToString() ?? "";
            string unidad = _lblUnidadInsumo.Text;

            var existente = _productoSeleccionado.Receta.FirstOrDefault(r => r.Insumo == insumo);
            if (existente != null)
            {
                existente.Cantidad += cant;
            }
            else
            {
                _productoSeleccionado.Receta.Add(new IngredienteRecetaMock
                {
                    Insumo = insumo,
                    Cantidad = cant,
                    Unidad = unidad,
                    CostoUnitarioReferencia = 45.00m
                });
            }

            _txtCantidadInsumo.Clear();
            RefrescarTablaReceta();
        }

        private void BtnGuardarProducto_Click(object? sender, EventArgs e)
        {
            if (_productoSeleccionado == null) return;

            if (!decimal.TryParse(_txtPrecioProd.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal nuevoPrecio) || nuevoPrecio <= 0)
            {
                MessageBox.Show("Ingresa un precio de venta válido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _productoSeleccionado.Nombre = _txtNombreProd.Text.Trim();
            _productoSeleccionado.CodigoBarras = string.IsNullOrWhiteSpace(_txtCodigoBarras.Text) ? "-" : _txtCodigoBarras.Text.Trim();
            _productoSeleccionado.Categoria = _cmbCategoriaProd.SelectedItem?.ToString() ?? "Bebidas Calientes";
            _productoSeleccionado.PrecioVenta = nuevoPrecio;
            _productoSeleccionado.Disponible = _chkDisponible.Checked;

            FiltrarProductos();
            RefrescarTablaReceta();

            MessageBox.Show(
                $"Cambios guardados para '{_productoSeleccionado.Nombre}'.\n" +
                $"• Código de barras: {_productoSeleccionado.CodigoBarras}\n" +
                $"• Precio: {nuevoPrecio.ToString("C2", _culturaMxn)}\n" +
                $"• Ingredientes en receta: {_productoSeleccionado.Receta.Count}",
                "Catálogo y Recetas Actualizados",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void BtnNuevoProducto_Click(object? sender, EventArgs e)
        {
            var nuevo = new ProductoAdminMock
            {
                Nombre = $"Nuevo Producto {_productos.Count + 1}",
                Descripcion = "Producto agregado desde catálogo",
                Categoria = "Bebidas Calientes",
                PrecioVenta = 50.00m,
                CodigoBarras = "-",
                Disponible = true
            };
            _productos.Add(nuevo);
            FiltrarProductos();
            SeleccionarProducto(nuevo);
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 24;
            int topContenido = _pnlTopBar.Height + 16;
            int altoDisponible = Math.Max(480, ClientSize.Height - topContenido - 20);
            int anchoUtil = Math.Max(860, ClientSize.Width - (margenX * 2));
            int gap = 16;

            int anchoIzq = Math.Clamp((int)(anchoUtil * 0.45), 420, 560);
            int anchoDer = anchoUtil - anchoIzq - gap;

            _cardCatalogo.Bounds = new Rectangle(margenX, topContenido, anchoIzq, altoDisponible);
            _cardDetalle.Bounds = new Rectangle(margenX + anchoIzq + gap, topContenido, anchoDer, altoDisponible);

            // Controles internos panel izquierdo
            _btnNuevoProducto.Location = new Point(anchoIzq - _btnNuevoProducto.Width - 18, 16);
            _txtBuscarProd.Width = anchoIzq - 36;
            _cmbFiltroCat.Width = anchoIzq - 36;
            _dgvProductos.Bounds = new Rectangle(14, 144, anchoIzq - 28, altoDisponible - 160);

            // Controles internos panel derecho
            if (_cardDetalle.Controls["sepReceta"] is Panel sep)
                sep.Width = anchoDer - 48;

            int altoPieReceta = 44;
            int yTablaReceta = 244;
            int altoTablaReceta = Math.Max(150, altoDisponible - yTablaReceta - altoPieReceta - 14);
            _dgvReceta.Bounds = new Rectangle(24, yTablaReceta, anchoDer - 48, altoTablaReceta);

            int yPie = yTablaReceta + altoTablaReceta + 12;
            _lblCostoRecetaVal.Location = new Point(24, yPie);
            _lblMargenVal.Location = new Point(anchoDer - _lblMargenVal.Width - 28, yPie);
        }
    }
}