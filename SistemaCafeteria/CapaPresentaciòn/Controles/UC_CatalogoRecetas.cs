using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CapaLogica;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Estilos;
using Ent = CapaEntidades;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_CatalogoRecetas : UserControl
    {
        private const string TodasLasCategorias = "Todas las categorías";

        private readonly CatalogoNegocio _negocio = new();
        private readonly CultureInfo _culturaMxn = CultureInfo.GetCultureInfo("es-MX");

        // Datos cargados desde MySQL
        private List<Ent.Producto> _productos = new();
        private List<Ent.Categoria> _categorias = new();
        private List<Ent.Insumo> _insumos = new();
        private Dictionary<int, Ent.UnidadMedida> _unidades = new();
        private Dictionary<int, decimal> _costosReferencia = new();

        // Estado de edición
        private Ent.Producto? _productoSeleccionado;          // null = producto nuevo sin guardar
        private List<Ent.DetalleReceta> _recetaEdicion = new();
        private bool _recetaModificada;
        private string? _imagenEdicion;
        private bool _suprimirSeleccion;

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
            ConfigurarInterfaz();

            Load += (s, e) =>
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
                    return;

                CargarDatos();
            };
        }

        // =====================================================================
        // CARGA DE DATOS DESDE MYSQL
        // =====================================================================

        private void CargarDatos(int? idSeleccionar = null)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _categorias = _negocio.ObtenerCategorias();
                _insumos = _negocio.ObtenerInsumosParaReceta();
                _unidades = _negocio.ObtenerUnidadesMedida().ToDictionary(u => u.IdUnidad);
                _costosReferencia = _negocio.ObtenerCostosReferencia();
                _productos = _negocio.ObtenerProductos(soloActivos: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el catálogo desde MySQL:\n{ex.Message}",
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            LlenarCombos();

            int? id = idSeleccionar ?? _productoSeleccionado?.IdProducto ?? _productos.FirstOrDefault()?.IdProducto;
            if (id.HasValue)
                CargarProductoEnFicha(id.Value);
            else
                PrepararProductoNuevo();

            FiltrarProductos();
        }

        private void LlenarCombos()
        {
            string filtroActual = _cmbFiltroCat.SelectedItem?.ToString() ?? TodasLasCategorias;

            _cmbFiltroCat.BeginUpdate();
            _cmbFiltroCat.Items.Clear();
            _cmbFiltroCat.Items.Add(TodasLasCategorias);
            foreach (var c in _categorias)
                _cmbFiltroCat.Items.Add(c.Nombre);
            _cmbFiltroCat.EndUpdate();

            int idx = _cmbFiltroCat.Items.IndexOf(filtroActual);
            _cmbFiltroCat.SelectedIndex = idx >= 0 ? idx : 0;

            _cmbCategoriaProd.Items.Clear();
            foreach (var c in _categorias)
                _cmbCategoriaProd.Items.Add(c.Nombre);

            _cmbNuevoInsumo.Items.Clear();
            foreach (var i in _insumos)
                _cmbNuevoInsumo.Items.Add(i);
            if (_cmbNuevoInsumo.Items.Count > 0)
                _cmbNuevoInsumo.SelectedIndex = 0;
        }

        // =====================================================================
        // CONSTRUCCIÓN DE LA INTERFAZ
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
                Text = "Catálogo de productos (0)",
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
            _cmbFiltroCat.Items.Add(TodasLasCategorias);
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

            // Columna oculta con el id real del producto en MySQL
            _dgvProductos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProdId", HeaderText = "Id", Visible = false });
            // Columnas visibles: Nombre | Categoría | Precio | Estado | Código de barras
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
                if (_suprimirSeleccion || _dgvProductos.SelectedRows.Count == 0)
                    return;

                if (_dgvProductos.SelectedRows[0].Cells["colProdId"].Value is not int id)
                    return;

                if (_productoSeleccionado?.IdProducto == id)
                    return;

                CargarProductoEnFicha(id);
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

            // Vista previa de foto (clic para cambiarla)
            _picProducto = new PictureBox
            {
                Location = new Point(24, 22),
                Size = new Size(118, 126),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#F7F3EE"),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            _picProducto.Click += (s, e) => SeleccionarImagen();
            new ToolTip().SetToolTip(_picProducto, "Haz clic para elegir la imagen del producto");

            Label lblNom = new Label { Text = "Nombre del producto", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(156, 18), AutoSize = true };
            _txtNombreProd = new TextBox { Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(156, 38), Width = 220 };

            Label lblCod = new Label { Text = "Código de barras (EAN / SKU)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(390, 18), AutoSize = true };
            _txtCodigoBarras = new TextBox
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                PlaceholderText = "Opcional, ej. 7501234567893",
                Location = new Point(390, 38),
                Width = 180
            };

            Label lblCat = new Label { Text = "Categoría", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(156, 74), AutoSize = true };
            _cmbCategoriaProd = new ComboBox { Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(156, 94), Width = 155 };

            Label lblPrec = new Label { Text = "Precio ($)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TemaCafeteria.TextoSecundario, Location = new Point(322, 74), AutoSize = true };
            _txtPrecioProd = new TextBox { Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(322, 94), Width = 82 };
            _txtPrecioProd.TextChanged += (s, e) => RefrescarTablaReceta();

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
                Width = 225,
                DisplayMember = "Nombre"
            };

            _txtCantidadInsumo = new TextBox
            {
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "Cant. (0.020)",
                Location = new Point(258, 204),
                Width = 98
            };

            _lblUnidadInsumo = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(362, 208),
                AutoSize = true
            };

            _cmbNuevoInsumo.SelectedIndexChanged += (s, e) =>
            {
                _lblUnidadInsumo.Text = (_cmbNuevoInsumo.SelectedItem as Ent.Insumo)?.UnidadAbreviatura ?? "";
            };

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
                if (e.RowIndex >= 0
                    && e.RowIndex < _recetaEdicion.Count
                    && e.ColumnIndex == _dgvReceta.Columns["colRecQuitar"]!.Index)
                {
                    _recetaEdicion.RemoveAt(e.RowIndex);
                    _recetaModificada = true;
                    RefrescarTablaReceta();
                }
            };

            // Pie financiero de la receta
            _lblCostoRecetaVal = new Label
            {
                Text = "Costo estimado de receta: $0.00",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                AutoSize = true
            };

            _lblMargenVal = new Label
            {
                Text = "Utilidad bruta: $0.00 (0%)",
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

        // =====================================================================
        // LISTA DE PRODUCTOS
        // =====================================================================

        private void FiltrarProductos()
        {
            if (_dgvProductos == null)
                return;

            string texto = _txtBuscarProd.Text.Trim().ToLowerInvariant();
            string cat = _cmbFiltroCat.SelectedItem?.ToString() ?? TodasLasCategorias;

            _suprimirSeleccion = true;
            _dgvProductos.Rows.Clear();
            int conteo = 0;

            foreach (var p in _productos)
            {
                if (cat != TodasLasCategorias && p.NombreCategoria != cat)
                    continue;

                string codigo = p.CodigoBarras ?? string.Empty;

                if (!string.IsNullOrEmpty(texto))
                {
                    bool coincideNombre = p.Nombre.ToLowerInvariant().Contains(texto);
                    bool coincideCodigo = codigo.ToLowerInvariant().Contains(texto);
                    if (!coincideNombre && !coincideCodigo)
                        continue;
                }

                string estado = p.Estado != "Activo"
                    ? "Inactivo"
                    : (p.IdVersionRecetaActiva.HasValue ? "Activo" : "Sin receta");

                _dgvProductos.Rows.Add(
                    p.IdProducto,
                    p.Nombre,
                    p.NombreCategoria,
                    p.Precio.ToString("C2", _culturaMxn),
                    estado,
                    string.IsNullOrWhiteSpace(codigo) ? "-" : codigo
                );
                conteo++;
            }

            // Volver a marcar el producto que se está editando (sin recargarlo)
            _dgvProductos.ClearSelection();
            if (_productoSeleccionado != null)
            {
                foreach (DataGridViewRow row in _dgvProductos.Rows)
                {
                    if (row.Cells["colProdId"].Value is int id && id == _productoSeleccionado.IdProducto)
                    {
                        _dgvProductos.CurrentCell = row.Cells["colProdNombre"];
                        row.Selected = true;
                        break;
                    }
                }
            }
            _suprimirSeleccion = false;

            if (_cardCatalogo.Controls["lblTitCat"] is Label lbl)
                lbl.Text = $"Catálogo de productos ({conteo})";
        }

        // =====================================================================
        // FICHA DEL PRODUCTO
        // =====================================================================

        private void CargarProductoEnFicha(int idProducto)
        {
            Ent.Producto? prod;
            try
            {
                prod = _negocio.ObtenerProductoConReceta(idProducto);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el producto desde MySQL:\n{ex.Message}",
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (prod == null)
                return;

            _productoSeleccionado = prod;
            _txtNombreProd.Text = prod.Nombre;
            _txtCodigoBarras.Text = prod.CodigoBarras ?? string.Empty;
            _cmbCategoriaProd.SelectedItem = prod.NombreCategoria;
            _txtPrecioProd.Text = prod.Precio.ToString("0.00", CultureInfo.InvariantCulture);
            _chkDisponible.Checked = prod.Estado == "Activo";

            _imagenEdicion = prod.ImagenRuta;
            MostrarImagen(_imagenEdicion);

            // Copia de trabajo de la receta (no se guarda hasta presionar "Guardar")
            _recetaEdicion = prod.RecetaActiva
                .Select(d => new Ent.DetalleReceta
                {
                    IdInsumo = d.IdInsumo,
                    IdUnidad = d.IdUnidad,
                    Cantidad = d.Cantidad,
                    NombreInsumo = d.NombreInsumo,
                    UnidadAbreviatura = d.UnidadAbreviatura
                })
                .ToList();
            _recetaModificada = false;

            RefrescarTablaReceta();
        }

        private void PrepararProductoNuevo()
        {
            _productoSeleccionado = null;

            _suprimirSeleccion = true;
            _dgvProductos.ClearSelection();
            _suprimirSeleccion = false;

            _txtNombreProd.Text = string.Empty;
            _txtCodigoBarras.Text = string.Empty;
            _txtPrecioProd.Text = string.Empty;
            _cmbCategoriaProd.SelectedIndex = _cmbCategoriaProd.Items.Count > 0 ? 0 : -1;
            _chkDisponible.Checked = true;

            _imagenEdicion = null;
            MostrarImagen(null);

            _recetaEdicion = new List<Ent.DetalleReceta>();
            _recetaModificada = false;
            RefrescarTablaReceta();

            _txtNombreProd.Focus();
        }

        private void MostrarImagen(string? archivo)
        {
            var anterior = _picProducto.Image;
            _picProducto.Image = null;
            anterior?.Dispose();

            if (string.IsNullOrWhiteSpace(archivo))
                return;

            string ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Productos", archivo);
            if (!File.Exists(ruta))
                return;

            try
            {
                // Se copia a un Bitmap en memoria para no dejar el archivo bloqueado
                using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read);
                using var img = Image.FromStream(fs);
                _picProducto.Image = new Bitmap(img);
            }
            catch (Exception)
            {
                _picProducto.Image = null;
            }
        }

        private void SeleccionarImagen()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecciona la imagen del producto",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                string carpeta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Productos");
                Directory.CreateDirectory(carpeta);

                string nombreArchivo = Path.GetFileName(dlg.FileName);
                string destino = Path.Combine(carpeta, nombreArchivo);

                if (!string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase))
                    File.Copy(dlg.FileName, destino, overwrite: true);

                _imagenEdicion = nombreArchivo;
                MostrarImagen(nombreArchivo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo copiar la imagen:\n{ex.Message}",
                    "Imagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // =====================================================================
        // RECETA
        // =====================================================================

        private void RefrescarTablaReceta()
        {
            if (_dgvReceta == null)
                return;

            _dgvReceta.Rows.Clear();

            decimal costoTotal = 0m;
            bool faltanCostos = false;

            foreach (var item in _recetaEdicion)
            {
                decimal? costo = CalcularCostoLinea(item);
                if (costo.HasValue)
                    costoTotal += costo.Value;
                else
                    faltanCostos = true;

                _dgvReceta.Rows.Add(
                    item.NombreInsumo,
                    item.Cantidad.ToString("0.###", CultureInfo.InvariantCulture),
                    item.UnidadAbreviatura,
                    costo.HasValue ? costo.Value.ToString("C2", _culturaMxn) : "Sin lotes",
                    "Quitar"
                );
            }

            decimal precio = TryParseDecimal(_txtPrecioProd.Text, out decimal p) ? p : 0m;
            decimal utilidad = precio - costoTotal;
            decimal margen = precio > 0 ? (utilidad / precio) * 100m : 0m;

            _lblCostoRecetaVal.Text = $"Costo estimado de receta: {costoTotal.ToString("C2", _culturaMxn)}"
                                      + (faltanCostos ? " (algunos insumos sin lotes)" : string.Empty);
            _lblMargenVal.Text = $"Utilidad bruta: {utilidad.ToString("C2", _culturaMxn)} ({margen:N1}%)";
            _lblMargenVal.ForeColor = utilidad >= 0 ? TemaCafeteria.SemaforoVerde : ColorTranslator.FromHtml("#C62828");

            if (_cardDetalle != null && _cardDetalle.Width > 0)
                _lblMargenVal.Left = _cardDetalle.Width - _lblMargenVal.Width - 28;
        }

        /// <summary>
        /// Costo de una línea de receta usando el costo promedio de los lotes del insumo.
        /// Devuelve null si el insumo todavía no tiene lotes registrados.
        /// </summary>
        private decimal? CalcularCostoLinea(Ent.DetalleReceta linea)
        {
            if (!_costosReferencia.TryGetValue(linea.IdInsumo, out decimal costoReferencia))
                return null;

            decimal factor = 1m;
            var insumo = _insumos.FirstOrDefault(i => i.IdInsumo == linea.IdInsumo);
            if (insumo != null
                && _unidades.TryGetValue(linea.IdUnidad, out var unidadReceta)
                && _unidades.TryGetValue(insumo.IdUnidad, out var unidadInsumo))
            {
                factor = CatalogoNegocio.FactorConversion(unidadReceta, unidadInsumo);
            }

            return Math.Round(linea.Cantidad * factor * costoReferencia, 2);
        }

        private void BtnAgregarInsumo_Click(object? sender, EventArgs e)
        {
            if (_cmbNuevoInsumo.SelectedItem is not Ent.Insumo insumo)
            {
                MessageBox.Show("Selecciona un insumo de la lista.", "Insumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryParseDecimal(_txtCantidadInsumo.Text, out decimal cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingresa una cantidad válida mayor a cero (por ejemplo: 0.020).", "Cantidad inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existente = _recetaEdicion.FirstOrDefault(r => r.IdInsumo == insumo.IdInsumo);
            if (existente != null)
            {
                // Si la línea existente está en otra unidad, se convierte antes de sumar
                decimal factor = 1m;
                if (_unidades.TryGetValue(insumo.IdUnidad, out var uInsumo)
                    && _unidades.TryGetValue(existente.IdUnidad, out var uExistente))
                {
                    factor = CatalogoNegocio.FactorConversion(uInsumo, uExistente);
                }
                existente.Cantidad += cantidad * factor;
            }
            else
            {
                _recetaEdicion.Add(new Ent.DetalleReceta
                {
                    IdInsumo = insumo.IdInsumo,
                    IdUnidad = insumo.IdUnidad,
                    Cantidad = cantidad,
                    NombreInsumo = insumo.Nombre,
                    UnidadAbreviatura = insumo.UnidadAbreviatura
                });
            }

            _recetaModificada = true;
            _txtCantidadInsumo.Clear();
            RefrescarTablaReceta();
        }

        // =====================================================================
        // BOTONES
        // =====================================================================

        private void BtnNuevoProducto_Click(object? sender, EventArgs e)
        {
            PrepararProductoNuevo();
        }

        private void BtnGuardarProducto_Click(object? sender, EventArgs e)
        {
            string nombre = _txtNombreProd.Text.Trim();

            if (!TryParseDecimal(_txtPrecioProd.Text, out decimal precio) || precio <= 0)
            {
                MessageBox.Show("Ingresa un precio de venta válido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string nombreCategoria = _cmbCategoriaProd.SelectedItem?.ToString() ?? string.Empty;
            var categoria = _categorias.FirstOrDefault(c => c.Nombre == nombreCategoria);
            if (categoria == null)
            {
                MessageBox.Show("Selecciona una categoría.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string codigo = _txtCodigoBarras.Text.Trim();
            bool esNuevo = _productoSeleccionado == null;

            var producto = new Ent.Producto
            {
                IdProducto = _productoSeleccionado?.IdProducto ?? 0,
                IdCategoria = categoria.IdCategoria,
                Nombre = nombre,
                Precio = precio,
                CodigoBarras = (codigo.Length == 0 || codigo == "-") ? null : codigo,
                ImagenRuta = _imagenEdicion
            };

            try
            {
                Cursor = Cursors.WaitCursor;

                // 1. Guardar datos del producto (validaciones en CatalogoNegocio)
                var resProducto = _negocio.GuardarProducto(producto);
                if (!resProducto.Exito)
                {
                    MessageBox.Show(resProducto.Mensaje, "No se pudo guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int idProducto = resProducto.IdProducto;
                bool huboAvisos = false;
                var resumen = new List<string>
                {
                    $"• Producto: {nombre} — {precio.ToString("C2", _culturaMxn)}"
                };

                // 2. Estado Activo / Inactivo
                bool estabaActivo = esNuevo || _productoSeleccionado!.Estado == "Activo";
                if (_chkDisponible.Checked != estabaActivo)
                {
                    var resEstado = _negocio.CambiarEstadoProducto(idProducto, _chkDisponible.Checked);
                    if (!resEstado.Exito)
                        huboAvisos = true;

                    resumen.Add(resEstado.Exito
                        ? $"• Estado: {(_chkDisponible.Checked ? "Activo" : "Inactivo")}"
                        : $"• Estado: {resEstado.Mensaje}");
                }
                else
                {
                    resumen.Add($"• Estado: {(_chkDisponible.Checked ? "Activo" : "Inactivo")}");
                }

                // 3. Receta (se guarda como versión nueva solo si cambió)
                bool recetaGuardada = false;
                if (_recetaModificada)
                {
                    if (_recetaEdicion.Count == 0)
                    {
                        huboAvisos = true;
                        resumen.Add("• Receta: no se guardó porque está vacía.");
                    }
                    else
                    {
                        var resReceta = _negocio.GuardarReceta(idProducto, _recetaEdicion);
                        recetaGuardada = resReceta.Exito;
                        if (!resReceta.Exito)
                            huboAvisos = true;

                        resumen.Add(resReceta.Exito
                            ? $"• Receta: nueva versión guardada ({_recetaEdicion.Count} ingredientes)"
                            : $"• Receta: {resReceta.Mensaje}");
                    }
                }
                else if (esNuevo || _productoSeleccionado?.IdVersionRecetaActiva == null)
                {
                    huboAvisos = true;
                    resumen.Add("• Receta: sin ingredientes. Agrégalos para que aparezca en el Punto de Venta.");
                }
                else
                {
                    resumen.Add("• Receta: sin cambios");
                }

                // 4. Recargar desde MySQL, conservando la receta si no se pudo guardar
                bool conservarReceta = _recetaModificada && !recetaGuardada;
                var recetaPendiente = _recetaEdicion;

                _productos = _negocio.ObtenerProductos(soloActivos: false);
                CargarProductoEnFicha(idProducto);

                if (conservarReceta)
                {
                    _recetaEdicion = recetaPendiente;
                    _recetaModificada = true;
                    RefrescarTablaReceta();
                }

                FiltrarProductos();

                MessageBox.Show(
                    string.Join("\n", resumen),
                    huboAvisos ? "Guardado con avisos" : "Catálogo y recetas actualizados",
                    MessageBoxButtons.OK,
                    huboAvisos ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en MySQL:\n{ex.Message}",
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // =====================================================================
        // AUXILIARES
        // =====================================================================

        /// <summary>Acepta tanto "0.020" como "0,020".</summary>
        private static bool TryParseDecimal(string texto, out decimal valor)
        {
            return decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
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