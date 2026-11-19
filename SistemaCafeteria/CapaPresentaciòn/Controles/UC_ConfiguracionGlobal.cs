using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;
using CapaPresentaciòn.Formularios;
using CapaEntidades;
using CapaLogica;
namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_ConfiguracionGlobal : UserControl
    {
       

        // Valores guardados en sesión
        private string _rangoVerdeGuardado = "50% - 100%";
        private string _rangoAmarilloGuardado = "21% - 49%";
        private string _rangoRojoGuardado = "1% - 20%";
        private string _rangoBlancoGuardado = "0%";
        private decimal _limiteMermaGuardado = 100.00m;

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;

        // Tarjeta 1: Parámetros del Semáforo de Inventario
        private PanelRedondeado _cardSemaforo = null!;
        private Panel _boxVerde = null!;
        private Panel _boxAmarillo = null!;
        private Panel _boxRojo = null!;
        private Panel _boxBlanco = null!;
        private TextBox _txtVerde = null!;
        private TextBox _txtAmarillo = null!;
        private TextBox _txtRojo = null!;
        private TextBox _txtBlanco = null!;

        // Tarjeta 2: Límite Económico Global de Mermas
        private PanelRedondeado _cardLimiteMerma = null!;
        private TextBox _txtLimiteMerma = null!;
        private Label _lblMonedaMxn = null!;

        // Botones inferiores
        private Button _btnCancelar = null!;
        private Button _btnGuardar = null!;

        // Contenido desplazable (la tarjeta de usuarios no cabe en pantallas de 768 px de alto)
        private Panel _pnlContenido = null!;

        // Tarjeta 3: Usuarios y credenciales
        private readonly UsuarioNegocio _usuarioNegocio = new();
        private List<Usuario> _usuarios = new();
        private PanelRedondeado _cardUsuarios = null!;
        private Label _lblUsuariosDesc = null!;
        private DataGridView _gridUsuarios = null!;
        private Button _btnEditarUsuario = null!;
        private Button _btnCambiarContrasena = null!;
        private Button _btnAsignarNip = null!;
        private Button _btnCambiarEstado = null!;

        private const string TextoDescripcionUsuarios =
            "Datos de acceso de Cajeros, Baristas y Administradores. Cada cambio se confirma con el NIP de un Administrador.";


        public UC_ConfiguracionGlobal()
        {

            ConfigurarInterfaz();
            CargarValoresGuardados();
            CargarUsuarios();
        }


        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra superior limpia (sin RF-12)
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
                Text = "ADMINISTRACIÓN / PARÁMETROS DEL SISTEMA",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(34, 14),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Configuración Global",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(32, 32),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Administración de parámetros operativos de la cafetería",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(34, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });

            // 2. Tarjeta 1: Parámetros del Semáforo de Inventario (% del Stock Máximo)
            _cardSemaforo = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label lblSemTit = new Label
            {
                Text = "Parámetros del Semáforo de Inventario (% del Stock Máximo)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 20),
                AutoSize = true
            };

            _boxVerde = CrearCajaNivelSemaforo(
                "●  Verde (Óptimo)",
                ColorTranslator.FromHtml("#1F4E35"),
                ColorTranslator.FromHtml("#4CAF50"),
                ColorTranslator.FromHtml("#F6FBF8"),
                out _txtVerde
            );

            _boxAmarillo = CrearCajaNivelSemaforo(
                "●  Amarillo (Precaución)",
                ColorTranslator.FromHtml("#B26A00"),
                ColorTranslator.FromHtml("#F5B041"),
                ColorTranslator.FromHtml("#FFFDF7"),
                out _txtAmarillo
            );

            _boxRojo = CrearCajaNivelSemaforo(
                "●  Rojo (Crítico)",
                ColorTranslator.FromHtml("#C62828"),
                ColorTranslator.FromHtml("#E57373"),
                ColorTranslator.FromHtml("#FFF8F8"),
                out _txtRojo
            );

            _boxBlanco = CrearCajaNivelSemaforo(
                "○  Blanco (Agotado)",
                TemaCafeteria.TextoSecundario,
                ColorTranslator.FromHtml("#BDBDBD"),
                ColorTranslator.FromHtml("#FAFAFA"),
                out _txtBlanco
            );

            _cardSemaforo.Controls.AddRange(new Control[]
            {
                lblSemTit,
                _boxVerde, _boxAmarillo, _boxRojo, _boxBlanco
            });

            // 3. Tarjeta 2: Límite Económico Global de Mermas
            _cardLimiteMerma = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label lblLimTit = new Label
            {
                Text = "Límite Económico Global de Mermas",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 20),
                AutoSize = true
            };

            Panel sepLim = new Panel
            {
                Name = "sepLim",
                Location = new Point(24, 50),
                Height = 1,
                BackColor = TemaCafeteria.BordeSuave
            };

            Label lblLimDesc = new Label
            {
                Text = "Monto máximo permitido en mermas antes de requerir autorización obligatoria por parte del administrador.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 62),
                AutoSize = true
            };

            Panel pnlMonto = new Panel
            {
                Location = new Point(24, 96),
                Size = new Size(210, 42),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblSigno = new Label
            {
                Text = "$",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(12, 9),
                AutoSize = true
            };

            _txtLimiteMerma = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                Location = new Point(30, 9),
                Width = 165
            };

            pnlMonto.Controls.AddRange(new Control[] { lblSigno, _txtLimiteMerma });

            _lblMonedaMxn = new Label
            {
                Text = "MXN (Pesos Mexicanos)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(248, 106),
                AutoSize = true
            };

            _cardLimiteMerma.Controls.AddRange(new Control[]
            {
                lblLimTit, sepLim, lblLimDesc, pnlMonto, _lblMonedaMxn
            });

            // 4. Botones inferiores: Cancelar cambios | Guardar configuración
            _btnCancelar = new Button
            {
                Text = "Cancelar cambios",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = ColorTranslator.FromHtml("#E6E2DC"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(175, 44),
                Cursor = Cursors.Hand
            };
            _btnCancelar.FlatAppearance.BorderSize = 0;
            _btnCancelar.Click += (s, e) =>
            {
                CargarValoresGuardados();
                MessageBox.Show("Se restauraron los últimos valores guardados.", "Cambios descartados", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _btnGuardar = new Button
            {
                Text = "Guardar configuración",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1F242D"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(205, 44),
                Cursor = Cursors.Hand
            };
            _btnGuardar.FlatAppearance.BorderSize = 0;
            _btnGuardar.Click += BtnGuardar_Click;

            // 5. Tarjeta 3: Usuarios y credenciales
            ConstruirTarjetaUsuarios();

            _pnlContenido = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 28),
                BackColor = TemaCafeteria.FondoCrema
            };

            _pnlContenido.Controls.AddRange(new Control[]
            {
                _cardSemaforo,
                _cardLimiteMerma,
                _btnCancelar,
                _btnGuardar,
                _cardUsuarios
            });

            // El contenido (Fill) va antes que la barra superior (Top) para que ésta se acomode primero
            Controls.AddRange(new Control[] { _pnlContenido, _pnlTopBar });

            _pnlContenido.Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        // =====================================================================
        // TARJETA 3: USUARIOS Y CREDENCIALES (RF-12 · Diseño 6.2)
        // =====================================================================

        private void ConstruirTarjetaUsuarios()
        {
            _cardUsuarios = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            Label lblTit = new Label
            {
                Text = "Usuarios y credenciales",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 20),
                AutoSize = true
            };

            Panel sep = new Panel
            {
                Name = "sepUsu",
                Location = new Point(24, 50),
                Height = 1,
                BackColor = TemaCafeteria.BordeSuave
            };

            _lblUsuariosDesc = new Label
            {
                Text = TextoDescripcionUsuarios,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 60),
                AutoSize = false,
                AutoEllipsis = true,
                Height = 20
            };

            _gridUsuarios = new DataGridView
            {
                Location = new Point(24, 90),
                Height = 196,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = TemaCafeteria.BordeSuave,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 34,
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };
            _gridUsuarios.RowTemplate.Height = 30;
            _gridUsuarios.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F4EFE9");
            _gridUsuarios.ColumnHeadersDefaultCellStyle.ForeColor = TemaCafeteria.TextoSecundario;
            _gridUsuarios.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _gridUsuarios.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F4EFE9");
            _gridUsuarios.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            _gridUsuarios.DefaultCellStyle.ForeColor = TemaCafeteria.TextoOscuro;
            _gridUsuarios.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EFE6DC");
            _gridUsuarios.DefaultCellStyle.SelectionForeColor = TemaCafeteria.TextoOscuro;
            _gridUsuarios.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

            _gridUsuarios.Columns.Add("colNombre", "NOMBRE");
            _gridUsuarios.Columns.Add("colUsuario", "USUARIO");
            _gridUsuarios.Columns.Add("colRol", "ROL");
            _gridUsuarios.Columns.Add("colEstado", "ESTADO");
            _gridUsuarios.Columns.Add("colNip", "NIP");
            _gridUsuarios.Columns["colNombre"]!.FillWeight = 34;
            _gridUsuarios.Columns["colUsuario"]!.FillWeight = 20;
            _gridUsuarios.Columns["colRol"]!.FillWeight = 18;
            _gridUsuarios.Columns["colEstado"]!.FillWeight = 13;
            _gridUsuarios.Columns["colNip"]!.FillWeight = 15;
            foreach (DataGridViewColumn col in _gridUsuarios.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            _gridUsuarios.SelectionChanged += (s, e) => ActualizarBotonesUsuarios();
            _gridUsuarios.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditarDatosUsuario(); };

            _btnEditarUsuario = CrearBotonUsuario("✎  Editar datos", primario: true);
            _btnCambiarContrasena = CrearBotonUsuario("🔑  Cambiar contraseña", primario: false);
            _btnAsignarNip = CrearBotonUsuario("🔒  Asignar NIP", primario: false);
            _btnCambiarEstado = CrearBotonUsuario("Desactivar cuenta", primario: false);

            _btnEditarUsuario.Click += (s, e) => EditarDatosUsuario();
            _btnCambiarContrasena.Click += (s, e) => CambiarContrasenaUsuario();
            _btnAsignarNip.Click += (s, e) => AsignarNipUsuario();
            _btnCambiarEstado.Click += (s, e) => CambiarEstadoUsuario();

            _cardUsuarios.Controls.AddRange(new Control[]
            {
                lblTit, sep, _lblUsuariosDesc, _gridUsuarios,
                _btnEditarUsuario, _btnCambiarContrasena, _btnAsignarNip, _btnCambiarEstado
            });

            ActualizarBotonesUsuarios();
        }

        private static Button CrearBotonUsuario(string texto, bool primario)
        {
            Button btn = new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = primario ? Color.White : TemaCafeteria.TextoOscuro,
                BackColor = primario ? TemaCafeteria.CafePrimario : ColorTranslator.FromHtml("#EFEAE4"),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 40),   // 4 botones caben en el ancho mínimo de la tarjeta (772 px)
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void CargarUsuarios(int? idSeleccionar = null)
        {
            try
            {
                _usuarios = _usuarioNegocio.ObtenerUsuarios();
                _lblUsuariosDesc.Text = TextoDescripcionUsuarios;
                _lblUsuariosDesc.ForeColor = TemaCafeteria.TextoSecundario;
            }
            catch (Exception ex)
            {
                _usuarios = new List<Usuario>();
                _lblUsuariosDesc.Text = $"⚠ No se pudieron cargar los usuarios: {ex.Message}";
                _lblUsuariosDesc.ForeColor = TemaCafeteria.ErrorRojo;
            }

            _gridUsuarios.Rows.Clear();
            foreach (var u in _usuarios)
            {
                bool esAdmin = UsuarioNegocio.EsAdministrador(u);
                string nip = !esAdmin ? "No aplica" : string.IsNullOrEmpty(u.NipHash) ? "Sin asignar" : "Asignado";

                int fila = _gridUsuarios.Rows.Add(u.Nombre, u.NombreUsuario, u.NombreRol, u.Estado, nip);
                var row = _gridUsuarios.Rows[fila];
                row.Tag = u;

                if (!UsuarioNegocio.EstaActivo(u))
                    row.DefaultCellStyle.ForeColor = TemaCafeteria.TextoPlaceholder;
                if (esAdmin && string.IsNullOrEmpty(u.NipHash))
                    row.Cells["colNip"].Style.ForeColor = TemaCafeteria.SemaforoRojo;
            }

            _gridUsuarios.ClearSelection();
            var seleccion = _gridUsuarios.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(r => r.Tag is Usuario u && u.IdUsuario == idSeleccionar);
            if (seleccion != null)
                seleccion.Selected = true;
            else if (_gridUsuarios.Rows.Count > 0)
                _gridUsuarios.Rows[0].Selected = true;

            ActualizarBotonesUsuarios();
        }

        private Usuario? UsuarioSeleccionado()
            => _gridUsuarios.SelectedRows.Count > 0 ? _gridUsuarios.SelectedRows[0].Tag as Usuario : null;

        private void ActualizarBotonesUsuarios()
        {
            var u = UsuarioSeleccionado();
            bool hay = u != null;

            _btnEditarUsuario.Enabled = hay;
            _btnCambiarContrasena.Enabled = hay;
            _btnCambiarEstado.Enabled = hay;
            _btnAsignarNip.Enabled = hay && UsuarioNegocio.EsAdministrador(u!);

            _btnAsignarNip.Text = hay && !string.IsNullOrEmpty(u!.NipHash) ? "🔒  Cambiar NIP" : "🔒  Asignar NIP";
            _btnCambiarEstado.Text = hay && !UsuarioNegocio.EstaActivo(u!) ? "Activar cuenta" : "Desactivar cuenta";
        }

        private void EditarDatosUsuario()
        {
            if (UsuarioSeleccionado() is not Usuario u) return;

            var valores = DialogoCaptura.Solicitar(this,
                "Editar datos del usuario",
                $"Cuenta de {u.NombreRol}. El nombre de usuario es el que se escribe al iniciar sesión.",
                new[]
                {
                    new CampoCaptura("Nombre completo", u.Nombre, LongitudMaxima: 100),
                    new CampoCaptura("Nombre de usuario", u.NombreUsuario, LongitudMaxima: 50)
                },
                v => _usuarioNegocio.ValidarDatos(u.IdUsuario, v[0], v[1]));

            if (valores == null) return;

            EjecutarConNip(u.IdUsuario,
                "Guardar datos del usuario",
                $"Se actualizarán los datos de \"{u.NombreUsuario}\".",
                nip => _usuarioNegocio.ActualizarDatos(u.IdUsuario, valores[0], valores[1], nip));
        }

        private void CambiarContrasenaUsuario()
        {
            if (UsuarioSeleccionado() is not Usuario u) return;

            var valores = DialogoCaptura.Solicitar(this,
                "Cambiar contraseña",
                $"Nueva contraseña para \"{u.NombreUsuario}\" (mínimo {UsuarioNegocio.LongitudMinimaContrasena} caracteres). Se guarda cifrada con bcrypt.",
                new[]
                {
                    new CampoCaptura("Nueva contraseña", Secreto: true, LongitudMaxima: UsuarioNegocio.LongitudMaximaContrasenaBytes),
                    new CampoCaptura("Confirmar contraseña", Secreto: true, LongitudMaxima: UsuarioNegocio.LongitudMaximaContrasenaBytes)
                },
                v => _usuarioNegocio.ValidarContrasena(u.IdUsuario, v[0], v[1]));

            if (valores == null) return;

            EjecutarConNip(u.IdUsuario,
                "Cambiar contraseña",
                $"Se reemplazará la contraseña de \"{u.NombreUsuario}\".",
                nip => _usuarioNegocio.CambiarContrasena(u.IdUsuario, valores[0], valores[1], nip));
        }

        private void AsignarNipUsuario()
        {
            if (UsuarioSeleccionado() is not Usuario u) return;

            var valores = DialogoCaptura.Solicitar(this,
                string.IsNullOrEmpty(u.NipHash) ? "Asignar NIP" : "Cambiar NIP",
                $"NIP de autorización de 4 dígitos para \"{u.NombreUsuario}\". No puede repetirse con el de otro Administrador.",
                new[]
                {
                    new CampoCaptura("Nuevo NIP", Secreto: true, LongitudMaxima: UsuarioNegocio.LongitudNip, SoloDigitos: true),
                    new CampoCaptura("Confirmar NIP", Secreto: true, LongitudMaxima: UsuarioNegocio.LongitudNip, SoloDigitos: true)
                },
                v => _usuarioNegocio.ValidarNuevoNip(u.IdUsuario, v[0], v[1]));

            if (valores == null) return;

            EjecutarConNip(u.IdUsuario,
                "Guardar NIP",
                $"Se guardará el nuevo NIP de \"{u.NombreUsuario}\". Autoriza con el NIP actual de un Administrador.",
                nip => _usuarioNegocio.AsignarNip(u.IdUsuario, valores[0], valores[1], nip));
        }

        private void CambiarEstadoUsuario()
        {
            if (UsuarioSeleccionado() is not Usuario u) return;

            bool activar = !UsuarioNegocio.EstaActivo(u);

            // Se revisa antes de pedir el NIP (p. ej. último Administrador o la propia cuenta)
            var validacion = _usuarioNegocio.ValidarCambioEstado(u.IdUsuario, activar);
            if (!validacion.Exito)
            {
                MessageBox.Show(validacion.Mensaje, "No se puede cambiar el estado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EjecutarConNip(u.IdUsuario,
                activar ? "Activar cuenta" : "Desactivar cuenta",
                activar
                    ? $"\"{u.NombreUsuario}\" podrá volver a iniciar sesión."
                    : $"\"{u.NombreUsuario}\" ya no podrá iniciar sesión. Su historial de ventas, recepciones y mermas se conserva.",
                nip => _usuarioNegocio.CambiarEstado(u.IdUsuario, activar, nip));
        }

        /// <summary>
        /// Pide el NIP de Administrador (confirmación explícita, RNF-13) y ejecuta la operación;
        /// la capa de lógica vuelve a validar el NIP y los datos antes de guardar.
        /// </summary>
        private void EjecutarConNip(int idUsuario, string titulo, string detalle, Func<string, (bool Exito, string Mensaje)> operacion)
        {
            string? nip = DialogoNipAdministrador.Solicitar(this, titulo, detalle);
            if (nip == null) return;

            var resultado = operacion(nip);

            MessageBox.Show(resultado.Mensaje,
                resultado.Exito ? "Usuarios y credenciales" : "No se guardaron los cambios",
                MessageBoxButtons.OK,
                resultado.Exito ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            CargarUsuarios(idUsuario);
        }

        private Panel CrearCajaNivelSemaforo(
            string titulo,
            Color colorTitulo,
            Color colorBorde,
            Color colorFondo,
            out TextBox txtRango)
        {
            Panel box = new Panel
            {
                BackColor = colorFondo,
                Height = 96
            };
            box.Paint += (s, e) =>
            {
                using var pen = new Pen(colorBorde, 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
            };

            Label lblTit = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = colorTitulo,
                Location = new Point(14, 12),
                AutoSize = true
            };

            txtRango = new TextBox
            {
                Name = "txtRango",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                Location = new Point(14, 42),
                Height = 34
            };

            box.Controls.AddRange(new Control[] { lblTit, txtRango });
            return box;
        }

        private readonly ConfiguracionNegocio _configNegocio = new();
        private readonly AuthNegocio _authNegocio = new();
        private int _idConfiguracionActual = 1;

        private void CargarValoresGuardados()
        {
            try
            {
                ConfiguracionGeneral cfg = _configNegocio.ObtenerConfiguracionActual();
                _idConfiguracionActual = cfg.IdConfiguracion;

                _rangoVerdeGuardado = $"{cfg.VerdeMinimo:0.##}% - 100%";
                _rangoAmarilloGuardado = $"{cfg.AmarilloMinimo:0.##}% - {cfg.AmarilloMaximo:0.##}%";
                _rangoRojoGuardado = $"1% - {cfg.RojoMaximo:0.##}%";
                _rangoBlancoGuardado = "0%";
                _limiteMermaGuardado = cfg.LimiteEconomicoMerma;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar la configuración desde MySQL:\n{ex.Message}",
                    "Aviso de conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _txtVerde.Text = _rangoVerdeGuardado;
            _txtAmarillo.Text = _rangoAmarilloGuardado;
            _txtRojo.Text = _rangoRojoGuardado;
            _txtBlanco.Text = _rangoBlancoGuardado;
            _txtLimiteMerma.Text = _limiteMermaGuardado.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtVerde.Text) ||
                string.IsNullOrWhiteSpace(_txtAmarillo.Text) ||
                string.IsNullOrWhiteSpace(_txtRojo.Text) ||
                string.IsNullOrWhiteSpace(_txtBlanco.Text))
            {
                MessageBox.Show("Todos los rangos del semáforo de inventario son obligatorios.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EntradaNumerica.TryLeerImporte(_txtLimiteMerma.Text, out decimal nuevoLimite, out string errorLimite))
            {
                MessageBox.Show($"Límite económico de mermas: {errorLimite}", "Monto inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtLimiteMerma.Focus();
                return;
            }

            if (!SolicitarNipAdministrador("Guardar parámetros globales del sistema", out string nombreAdminAutoriza))
                return;

            var resultado = _configNegocio.GuardarDesdeRangosTexto(
                _idConfiguracionActual,
                _txtVerde.Text.Trim(),
                _txtAmarillo.Text.Trim(),
                _txtRojo.Text.Trim(),
                nuevoLimite
            );

            if (!resultado.Exito)
            {
                MessageBox.Show(resultado.Mensaje, "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CargarValoresGuardados();

            MessageBox.Show(
                $"Configuración global actualizada en MySQL por {nombreAdminAutoriza}.\n\n" +
                $"• Verde (Óptimo): {_rangoVerdeGuardado}\n" +
                $"• Amarillo (Precaución): {_rangoAmarilloGuardado}\n" +
                $"• Rojo (Crítico): {_rangoRojoGuardado}\n" +
                $"• Blanco (Agotado): {_rangoBlancoGuardado}\n" +
                $"• Límite económico de mermas: ${_limiteMermaGuardado:N2} MXN",
                "Parámetros Guardados",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private bool SolicitarNipAdministrador(string accionConfirmar, out string nombreAdminAutoriza)
        {
            bool autorizado = false;
            string adminEncontrado = string.Empty;

            using Form dlgNip = new Form
            {
                Text = "Confirmación de Seguridad",
                Size = new Size(390, 265),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblIcono = new Label
            {
                Text = "🔒  Validación de NIP",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblInstruccion = new Label
            {
                Text = $"Ingresa un NIP de Administrador autorizado para:\n{accionConfirmar}",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 48),
                Size = new Size(330, 38)
            };

            TextBox txtNip = new TextBox
            {
                Location = new Point(95, 96),
                Size = new Size(180, 42),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                PasswordChar = '●',
                MaxLength = 4
            };
            txtNip.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };

            Button btnCancelar = new Button
            {
                Text = "Cancelar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = ColorTranslator.FromHtml("#EFECE8"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 162),
                Size = new Size(150, 40),
                Cursor = Cursors.Hand
            };
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Click += (s, e) => dlgNip.Close();

            Button btnConfirmar = new Button
            {
                Text = "Confirmar NIP",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(190, 162),
                Size = new Size(162, 40),
                Cursor = Cursors.Hand
            };
            btnConfirmar.FlatAppearance.BorderSize = 0;
            btnConfirmar.Click += (s, e) =>
            {
                var validacion = _authNegocio.ValidarNipAdministrador(txtNip.Text.Trim());
                if (!validacion.Autorizado)
                {
                    MessageBox.Show("NIP incorrecto o sin privilegios de Administrador.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtNip.Clear();
                    txtNip.Focus();
                    return;
                }

                autorizado = true;
                adminEncontrado = validacion.NombreAdmin;
                dlgNip.Close();
            };

            dlgNip.AcceptButton = btnConfirmar;
            dlgNip.Shown += (s, e) => txtNip.Focus();
            dlgNip.Controls.AddRange(new Control[] { lblIcono, lblInstruccion, txtNip, btnCancelar, btnConfirmar });
            dlgNip.ShowDialog(this);

            nombreAdminAutoriza = adminEncontrado;
            return autorizado;
        }

        private void AjustarLayoutResponsivo()
        {
            if (_pnlContenido == null || _pnlContenido.ClientSize.Width <= 0 || _pnlContenido.ClientSize.Height <= 0) return;

            // Coordenadas dentro del panel desplazable: se suma la posición de desplazamiento actual
            Point o = _pnlContenido.AutoScrollPosition;
            int margenX = 34;
            int anchoUtil = Math.Max(820, _pnlContenido.ClientSize.Width - (margenX * 2));

            // Tarjeta 1: Semáforo de inventario
            int ySem = 22;
            int altoSem = 176;
            _cardSemaforo.Bounds = new Rectangle(margenX + o.X, ySem + o.Y, anchoUtil, altoSem);

            int gapBox = 16;
            int anchoBox = (anchoUtil - 48 - (gapBox * 3)) / 4;
            Panel[] cajas = { _boxVerde, _boxAmarillo, _boxRojo, _boxBlanco };

            for (int i = 0; i < cajas.Length; i++)
            {
                cajas[i].Bounds = new Rectangle(24 + i * (anchoBox + gapBox), 56, anchoBox, 96);
                if (cajas[i].Controls["txtRango"] is TextBox txt)
                {
                    txt.Width = anchoBox - 28;
                }
            }

            // Tarjeta 2: Límite Económico Global de Mermas
            int yLim = ySem + altoSem + 20;
            int altoLim = 166;
            _cardLimiteMerma.Bounds = new Rectangle(margenX + o.X, yLim + o.Y, anchoUtil, altoLim);
            if (_cardLimiteMerma.Controls["sepLim"] is Panel sep)
                sep.Width = anchoUtil - 48;

            // Botones inferiores alineados a la derecha debajo de la segunda tarjeta
            int yBotones = yLim + altoLim + 24;
            _btnGuardar.Location = new Point(margenX + anchoUtil - _btnGuardar.Width + o.X, yBotones + o.Y);
            _btnCancelar.Location = new Point(_btnGuardar.Left - _btnCancelar.Width - 14, yBotones + o.Y);

            // Tarjeta 3: Usuarios y credenciales
            int yUsuarios = yBotones + _btnGuardar.Height + 28;
            int altoUsuarios = 352;
            _cardUsuarios.Bounds = new Rectangle(margenX + o.X, yUsuarios + o.Y, anchoUtil, altoUsuarios);

            int anchoInterno = anchoUtil - 48;
            if (_cardUsuarios.Controls["sepUsu"] is Panel sepUsu)
                sepUsu.Width = anchoInterno;
            _lblUsuariosDesc.Width = anchoInterno;
            _gridUsuarios.Width = anchoInterno;

            int yBotonesUsu = _gridUsuarios.Bottom + 16;
            int xBoton = 24;
            foreach (var btn in new[] { _btnEditarUsuario, _btnCambiarContrasena, _btnAsignarNip, _btnCambiarEstado })
            {
                btn.Location = new Point(xBoton, yBotonesUsu);
                xBoton += btn.Width + 12;
            }
        }
    }
}