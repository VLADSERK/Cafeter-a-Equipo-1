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
    /// <summary>
    /// Configuración Global (RF-12): semáforo de inventario, límite económico de mermas,
    /// cuenta del rol de Cajero y credenciales del Administrador en sesión.
    /// Toda validación y el NIP de autorización se resuelven en la capa de lógica; esta pantalla
    /// solo captura, avisa antes de pedir el NIP y muestra el resultado.
    /// </summary>
    public partial class UC_ConfiguracionGlobal : UserControl
    {
        private static readonly Color ColorBotonOscuro = ColorTranslator.FromHtml("#1F242D");
        private static readonly Color ColorBotonSuave = ColorTranslator.FromHtml("#E6E2DC");
        private static readonly Color FondoSoloLectura = ColorTranslator.FromHtml("#F3F1EE");

        private readonly ConfiguracionNegocio _configNegocio = new();
        private readonly UsuarioNegocio _usuarioNegocio = new();

        // Valores guardados (para "Cancelar cambios" y para detectar si algo cambió)
        private ConfiguracionGeneral? _configGuardada;
        private int _idConfiguracionActual = 1;
        private Usuario? _cuentaCajero;          // cuenta del rol de Cajero que se edita (la elige la lógica)

        // Encabezado y pie
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;
        private Panel _pnlPie = null!;
        private Label _lblPieAviso = null!;
        private Button _btnCancelar = null!;
        private Button _btnGuardar = null!;

        // Contenido desplazable (las 4 tarjetas no caben en pantallas de 768 px de alto)
        private Panel _pnlContenido = null!;

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

        // Tarjeta 3: Configuración del Rol de Cajero
        private PanelRedondeado _cardCajero = null!;
        private TextBox _txtNombreCajero = null!;
        private TextBox _txtUsuarioCajero = null!;
        private TextBox _txtContrasenaCajero = null!;
        private TextBox _txtConfirmarCajero = null!;
        private ComboBox _cmbTurnoCajero = null!;
        private Label _lblAvisoCajero = null!;
        private Panel[] _camposCajero = null!;   // Nombre, Usuario, Contraseña, Turno (cuadrícula 2 × 2)

        // Tarjeta 4: Configuración del Rol de Administrador
        private PanelRedondeado _cardAdmin = null!;
        private Label _lblCuentaAdmin = null!;
        private TextBox _txtContrasenaAdmin = null!;
        private TextBox _txtConfirmarAdmin = null!;
        private TextBox _txtNipAdmin = null!;
        private TextBox _txtConfirmarNip = null!;
        private Label _lblAvisoAdmin = null!;
        private Panel[] _camposAdmin = null!;    // Contraseña, NIP

        private static readonly string TextoAvisoCajero =
            $"ⓘ  La contraseña se guarda cifrada con bcrypt (mínimo {UsuarioNegocio.LongitudMinimaContrasena} caracteres); " +
            "déjala vacía para conservar la actual. El usuario no puede repetirse.";

        public UC_ConfiguracionGlobal()
        {
            ConfigurarInterfaz();
            CargarTodo();
        }

        // =====================================================================
        // CONSTRUCCIÓN DE LA INTERFAZ
        // =====================================================================

        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            ConstruirEncabezado();
            ConstruirTarjetaSemaforo();
            ConstruirTarjetaLimite();
            ConstruirTarjetaCajero();
            ConstruirTarjetaAdministrador();
            ConstruirPie();

            _pnlContenido = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 24),
                BackColor = TemaCafeteria.FondoCrema
            };
            _pnlContenido.Controls.AddRange(new Control[] { _cardSemaforo, _cardLimiteMerma, _cardCajero, _cardAdmin });

            // El contenido (Fill) va antes que la barra superior y el pie para que éstos se acomoden primero
            Controls.AddRange(new Control[] { _pnlContenido, _pnlPie, _pnlTopBar });

            _pnlContenido.Resize += (s, e) => AjustarLayoutResponsivo();
            _pnlPie.Resize += (s, e) => AjustarPie();
            Load += (s, e) => { AjustarLayoutResponsivo(); AjustarPie(); };
            AjustarLayoutResponsivo();
        }

        private void ConstruirEncabezado()
        {
            _pnlTopBar = new Panel { BackColor = Color.White, Dock = DockStyle.Top, Height = 92 };
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
                Text = "Configuración Global (RF-12)",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(32, 32),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Administración de parámetros operativos, cajeros y credenciales de la cafetería",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(34, 65),
                AutoSize = true
            };

            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo });
        }

        private void ConstruirPie()
        {
            _pnlPie = new Panel { BackColor = Color.White, Dock = DockStyle.Bottom, Height = 76 };
            _pnlPie.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, 0, _pnlPie.Width, 0);
            };

            _lblPieAviso = new Label
            {
                Text = "Los cambios de las cuatro tarjetas se guardan juntos y se confirman con el NIP de un Administrador.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 44
            };

            _btnCancelar = new Button
            {
                Text = "Cancelar cambios",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = ColorBotonSuave,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(175, 44),
                Cursor = Cursors.Hand
            };
            _btnCancelar.FlatAppearance.BorderSize = 0;
            _btnCancelar.Click += (s, e) =>
            {
                CargarTodo();
                MessageBox.Show("Se restauraron los últimos valores guardados.", "Cambios descartados",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _btnGuardar = new Button
            {
                Text = "Guardar configuración",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorBotonOscuro,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(205, 44),
                Cursor = Cursors.Hand
            };
            _btnGuardar.FlatAppearance.BorderSize = 0;
            _btnGuardar.Click += BtnGuardar_Click;

            _pnlPie.Controls.AddRange(new Control[] { _lblPieAviso, _btnCancelar, _btnGuardar });
        }

        // ---------------------------------------------------------------------
        // Tarjeta 1 · Semáforo
        // ---------------------------------------------------------------------

        private void ConstruirTarjetaSemaforo()
        {
            _cardSemaforo = new PanelRedondeado { RadioBorde = 16, BackColor = Color.White };

            Label lblTit = CrearTituloTarjeta("Parámetros del Semáforo de Inventario (% del Stock Máximo)");

            _boxVerde = CrearCajaNivelSemaforo("●  Verde (Óptimo)", "Abasto suficiente",
                ColorTranslator.FromHtml("#1F4E35"), ColorTranslator.FromHtml("#4CAF50"), ColorTranslator.FromHtml("#F6FBF8"), out _txtVerde);
            _boxAmarillo = CrearCajaNivelSemaforo("●  Amarillo (Precaución)", "Reabastecer pronto",
                ColorTranslator.FromHtml("#B26A00"), ColorTranslator.FromHtml("#F5B041"), ColorTranslator.FromHtml("#FFFDF7"), out _txtAmarillo);
            _boxRojo = CrearCajaNivelSemaforo("●  Rojo (Crítico)", "Reabastecer urgente",
                ColorTranslator.FromHtml("#C62828"), ColorTranslator.FromHtml("#E57373"), ColorTranslator.FromHtml("#FFF8F8"), out _txtRojo);
            _boxBlanco = CrearCajaNivelSemaforo("○  Blanco (Agotado)", "Fijo: sin existencias",
                TemaCafeteria.TextoSecundario, ColorTranslator.FromHtml("#BDBDBD"), ColorTranslator.FromHtml("#FAFAFA"), out _txtBlanco);

            // Blanco = 0 % por definición (RF-08): se muestra pero no se edita
            _txtBlanco.ReadOnly = true;
            _txtBlanco.TabStop = false;
            _txtBlanco.BackColor = FondoSoloLectura;
            new ToolTip().SetToolTip(_txtBlanco, "Blanco siempre es 0 %: el insumo no tiene existencias (RF-08).");

            _cardSemaforo.Controls.AddRange(new Control[] { lblTit, _boxVerde, _boxAmarillo, _boxRojo, _boxBlanco });
        }

        private static Panel CrearCajaNivelSemaforo(string titulo, string leyenda, Color colorTitulo, Color colorBorde,
            Color colorFondo, out TextBox txtRango)
        {
            var box = new Panel { BackColor = colorFondo, Height = 108 };
            box.Paint += (s, e) =>
            {
                // Indicador de color: franja superior y borde del mismo tono
                using (var franja = new SolidBrush(colorBorde))
                    e.Graphics.FillRectangle(franja, 0, 0, box.Width, 5);
                using var pen = new Pen(colorBorde, 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
            };

            var lblTit = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = colorTitulo,
                Location = new Point(14, 16),
                AutoSize = true
            };

            txtRango = new TextBox
            {
                Name = "txtRango",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                Location = new Point(14, 44)
            };

            var lblLeyenda = new Label
            {
                Text = leyenda,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(14, 80),
                AutoSize = true
            };

            box.Controls.AddRange(new Control[] { lblTit, txtRango, lblLeyenda });
            return box;
        }

        // ---------------------------------------------------------------------
        // Tarjeta 2 · Límite de mermas
        // ---------------------------------------------------------------------

        private void ConstruirTarjetaLimite()
        {
            _cardLimiteMerma = new PanelRedondeado { RadioBorde = 16, BackColor = Color.White };

            Label lblTit = CrearTituloTarjeta("Límite Económico Global de Mermas");
            Panel sep = CrearSeparador("sepLim");

            var lblDesc = new Label
            {
                Text = "Monto máximo permitido en mermas antes de requerir autorización obligatoria por parte del administrador.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 64),
                AutoSize = true
            };

            var pnlMonto = new Panel
            {
                Location = new Point(24, 96),
                Size = new Size(210, 42),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblSigno = new Label
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
                Width = 165,
                PlaceholderText = "100.00"
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

            _cardLimiteMerma.Controls.AddRange(new Control[] { lblTit, sep, lblDesc, pnlMonto, _lblMonedaMxn });
        }

        // ---------------------------------------------------------------------
        // Tarjeta 3 · Rol de Cajero
        // ---------------------------------------------------------------------

        private void ConstruirTarjetaCajero()
        {
            _cardCajero = new PanelRedondeado { RadioBorde = 16, BackColor = Color.White };

            Label lblTit = CrearTituloTarjeta("Configuración del Rol de Cajero");
            Panel sep = CrearSeparador("sepCaj");

            _txtNombreCajero = CrearCaja("Ej. Sofía Ramírez", maxLongitud: 100);
            _txtUsuarioCajero = CrearCaja("Ej. cajero", maxLongitud: 50);
            _txtContrasenaCajero = CrearCaja("Nueva (vacío = sin cambio)", secreto: true, maxLongitud: UsuarioNegocio.LongitudMaximaContrasenaBytes);
            _txtConfirmarCajero = CrearCaja("Confirmar", secreto: true, maxLongitud: UsuarioNegocio.LongitudMaximaContrasenaBytes);

            // Turno: solo las opciones que acepta la lógica (y el ENUM de la base), sin texto libre
            _cmbTurnoCajero = new ComboBox
            {
                Font = new Font("Segoe UI", 10.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro
            };
            foreach (var turno in UsuarioNegocio.Turnos)
                _cmbTurnoCajero.Items.Add(turno);

            _camposCajero = new[]
            {
                CrearCampo("Nombre Completo", _txtNombreCajero),
                CrearCampo("Usuario", _txtUsuarioCajero),
                CrearCampo("Contraseña", _txtContrasenaCajero, _txtConfirmarCajero),
                CrearCampo("Datos Generales / Turno", _cmbTurnoCajero)
            };

            _lblAvisoCajero = CrearAviso(string.Empty);
            _lblAvisoCajero.Text = TextoAvisoCajero;

            _cardCajero.Controls.AddRange(new Control[] { lblTit, sep, _lblAvisoCajero });
            _cardCajero.Controls.AddRange(_camposCajero);
        }

        // ---------------------------------------------------------------------
        // Tarjeta 4 · Rol de Administrador
        // ---------------------------------------------------------------------

        private void ConstruirTarjetaAdministrador()
        {
            _cardAdmin = new PanelRedondeado { RadioBorde = 16, BackColor = Color.White };

            Label lblTit = CrearTituloTarjeta("Configuración del Rol de Administrador");
            Panel sep = CrearSeparador("sepAdm");

            _lblCuentaAdmin = new Label
            {
                Text = "Cuenta en sesión: —",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            _txtContrasenaAdmin = CrearCaja("Nueva (vacío = sin cambio)", secreto: true, maxLongitud: UsuarioNegocio.LongitudMaximaContrasenaBytes);
            _txtConfirmarAdmin = CrearCaja("Confirmar", secreto: true, maxLongitud: UsuarioNegocio.LongitudMaximaContrasenaBytes);
            _txtNipAdmin = CrearCaja("Nuevo NIP", secreto: true, maxLongitud: UsuarioNegocio.LongitudNip, soloDigitos: true);
            _txtConfirmarNip = CrearCaja("Confirmar NIP", secreto: true, maxLongitud: UsuarioNegocio.LongitudNip, soloDigitos: true);

            _camposAdmin = new[]
            {
                CrearCampo("Contraseña de Administrador", _txtContrasenaAdmin, _txtConfirmarAdmin),
                CrearCampo("NIP de 4 Dígitos (Autorizaciones Rápidas)", _txtNipAdmin, _txtConfirmarNip)
            };

            _lblAvisoAdmin = CrearAviso(
                "Contraseña y NIP se guardan cifrados con bcrypt. El NIP autoriza mermas, cancelaciones y cambios de configuración, " +
                "y no puede repetirse con el de otro Administrador. Deja los campos vacíos para conservar los actuales.");

            _cardAdmin.Controls.AddRange(new Control[] { lblTit, _lblCuentaAdmin, sep, _lblAvisoAdmin });
            _cardAdmin.Controls.AddRange(_camposAdmin);
        }

        // ---------------------------------------------------------------------
        // Ayudantes de construcción
        // ---------------------------------------------------------------------

        private static Label CrearTituloTarjeta(string texto) => new()
        {
            Text = texto,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TemaCafeteria.TextoOscuro,
            Location = new Point(24, 20),
            AutoSize = true
        };

        private static Panel CrearSeparador(string nombre) => new()
        {
            Name = nombre,
            Location = new Point(24, 52),
            Height = 1,
            BackColor = TemaCafeteria.BordeSuave
        };

        private static Label CrearAviso(string texto) => new()
        {
            Text = "ⓘ  " + texto,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = TemaCafeteria.TextoSecundario,
            AutoSize = false,
            Height = 34
        };

        private static TextBox CrearCaja(string marcador, bool secreto = false, int maxLongitud = 0, bool soloDigitos = false)
        {
            var txt = new TextBox
            {
                Font = new Font("Segoe UI", 10.5F),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = marcador,
                UseSystemPasswordChar = secreto
            };
            if (maxLongitud > 0)
                txt.MaxLength = maxLongitud;
            if (soloDigitos)
            {
                txt.KeyPress += (s, e) =>
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                        e.Handled = true;
                };
            }
            return txt;
        }

        /// <summary>Bloque etiqueta + uno o dos controles lado a lado (se acomodan en el layout).</summary>
        private static Panel CrearCampo(string etiqueta, params Control[] cajas)
        {
            var pnl = new Panel { BackColor = Color.White, Height = 62 };
            pnl.Controls.Add(new Label
            {
                Text = etiqueta,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(0, 0),
                AutoSize = true
            });
            pnl.Controls.AddRange(cajas);
            pnl.Tag = cajas;
            return pnl;
        }

        private static void AcomodarCampo(Panel campo, Rectangle limites)
        {
            campo.Bounds = limites;
            if (campo.Tag is not Control[] cajas || cajas.Length == 0) return;

            const int gap = 10;
            int ancho = (limites.Width - gap * (cajas.Length - 1)) / cajas.Length;
            for (int i = 0; i < cajas.Length; i++)
            {
                cajas[i].Location = new Point(i * (ancho + gap), 24);
                cajas[i].Width = ancho;
            }
        }

        // =====================================================================
        // CARGA DE DATOS
        // =====================================================================

        private void CargarTodo()
        {
            CargarValoresGuardados();
            CargarCajero();
            CargarAdministrador();
        }

        private void CargarValoresGuardados()
        {
            string verde = "50% - 100%", amarillo = "21% - 49%", rojo = "1% - 20%";
            decimal limite = 100.00m;

            try
            {
                ConfiguracionGeneral cfg = _configNegocio.ObtenerConfiguracionActual();
                _configGuardada = cfg;
                _idConfiguracionActual = cfg.IdConfiguracion;

                verde = $"{Pct(cfg.VerdeMinimo)}% - 100%";
                amarillo = $"{Pct(cfg.AmarilloMinimo)}% - {Pct(cfg.AmarilloMaximo)}%";
                rojo = $"1% - {Pct(cfg.RojoMaximo)}%";
                limite = cfg.LimiteEconomicoMerma;
            }
            catch (Exception ex)
            {
                _configGuardada = null;
                MessageBox.Show($"No se pudo cargar la configuración desde MySQL:\n{ex.Message}",
                    "Aviso de conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _txtVerde.Text = verde;
            _txtAmarillo.Text = amarillo;
            _txtRojo.Text = rojo;
            _txtBlanco.Text = "0%";
            _txtLimiteMerma.Text = limite.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Pct(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Carga en la tarjeta la cuenta del rol de Cajero que indique la lógica, con su turno.</summary>
        private void CargarCajero()
        {
            var controles = new Control[] { _txtNombreCajero, _txtUsuarioCajero, _txtContrasenaCajero, _txtConfirmarCajero, _cmbTurnoCajero };
            _txtNombreCajero.Clear();
            _txtUsuarioCajero.Clear();
            _txtContrasenaCajero.Clear();
            _txtConfirmarCajero.Clear();
            _cmbTurnoCajero.SelectedIndex = -1;

            try
            {
                _cuentaCajero = _usuarioNegocio.ObtenerCuentaCajero();
                string? turno = _cuentaCajero == null ? null : _usuarioNegocio.ObtenerTurno(_cuentaCajero.IdUsuario);

                _lblAvisoCajero.Text = _cuentaCajero == null
                    ? "ⓘ  No hay ninguna cuenta con rol Cajero registrada."
                    : TextoAvisoCajero;
                _lblAvisoCajero.ForeColor = TemaCafeteria.TextoSecundario;

                if (_cuentaCajero != null)
                {
                    _txtNombreCajero.Text = _cuentaCajero.Nombre;
                    _txtUsuarioCajero.Text = _cuentaCajero.NombreUsuario;
                    _cmbTurnoCajero.SelectedItem = turno;   // null → sin selección (la cuenta aún no tiene turno)
                }
            }
            catch (Exception ex)
            {
                _cuentaCajero = null;
                _lblAvisoCajero.Text = $"⚠  No se pudo cargar la cuenta de Cajero: {ex.Message}";
                _lblAvisoCajero.ForeColor = TemaCafeteria.ErrorRojo;
            }

            foreach (var c in controles)
                c.Enabled = _cuentaCajero != null;
        }

        private void CargarAdministrador()
        {
            foreach (var txt in new[] { _txtContrasenaAdmin, _txtConfirmarAdmin, _txtNipAdmin, _txtConfirmarNip })
                txt.Clear();

            try
            {
                var admin = _usuarioNegocio.ObtenerAdministradorEnSesion();
                _lblCuentaAdmin.Text = admin == null
                    ? "Cuenta en sesión: —"
                    : $"Cuenta en sesión: {admin.Nombre} ({admin.NombreUsuario}) · NIP {(string.IsNullOrEmpty(admin.NipHash) ? "sin asignar" : "asignado")}";
            }
            catch (Exception ex)
            {
                _lblCuentaAdmin.Text = $"⚠  {ex.Message}";
            }
            AjustarLayoutResponsivo();
        }

        // =====================================================================
        // GUARDAR (una sola autorización con NIP para las cuatro tarjetas)
        // =====================================================================

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            // 1. Semáforo y límite de mermas
            if (!EntradaNumerica.TryLeerImporte(_txtLimiteMerma.Text, out decimal nuevoLimite, out string errorLimite))
            {
                Avisar($"Límite económico de mermas: {errorLimite}", "Monto inválido", _txtLimiteMerma);
                return;
            }

            var vConfig = _configNegocio.ValidarParametros(_idConfiguracionActual,
                _txtVerde.Text.Trim(), _txtAmarillo.Text.Trim(), _txtRojo.Text.Trim(), nuevoLimite);
            if (!vConfig.Exito)
            {
                Avisar(vConfig.Mensaje, "Semáforo de inventario", _txtVerde);
                return;
            }
            bool cambiaConfig = _configGuardada == null || !ConfiguracionNegocio.MismosParametros(vConfig.Config!, _configGuardada);

            // 2. Cuenta del rol de Cajero
            var cuenta = _cuentaCajero;
            string? turno = _cmbTurnoCajero.SelectedItem as string;
            bool cambiaCajero = false;
            if (cuenta != null)
            {
                var vCajero = _usuarioNegocio.ValidarCuentaOperativa(cuenta.IdUsuario,
                    _txtNombreCajero.Text, _txtUsuarioCajero.Text, _txtContrasenaCajero.Text, _txtConfirmarCajero.Text, turno);
                if (!vCajero.Exito)
                {
                    Avisar(vCajero.Mensaje, "Configuración del Rol de Cajero", _txtNombreCajero);
                    return;
                }
                cambiaCajero = vCajero.HayCambios;
            }

            // 3. Credenciales del Administrador en sesión
            var vAdmin = _usuarioNegocio.ValidarCredencialesAdministrador(
                _txtContrasenaAdmin.Text, _txtConfirmarAdmin.Text, _txtNipAdmin.Text, _txtConfirmarNip.Text);
            if (!vAdmin.Exito)
            {
                Avisar(vAdmin.Mensaje, "Configuración del Rol de Administrador", _txtContrasenaAdmin);
                return;
            }

            var secciones = new List<string>();
            if (cambiaConfig) secciones.Add("parámetros del semáforo y límite de mermas");
            if (cambiaCajero) secciones.Add($"cuenta \"{cuenta!.NombreUsuario}\"");
            if (vAdmin.HayCambios) secciones.Add("credenciales del Administrador");

            if (secciones.Count == 0)
            {
                MessageBox.Show("No hay cambios por guardar.", "Configuración Global", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 4. Confirmación explícita con NIP (RNF-13); la lógica lo vuelve a validar en cada operación
            string? nip = DialogoNipAdministrador.Solicitar(this, "Guardar configuración",
                $"Se guardará: {string.Join(", ", secciones)}.");
            if (nip == null) return;

            var resultados = new List<(bool Exito, string Mensaje)>();

            if (cambiaConfig)
            {
                var r = _configNegocio.GuardarDesdeRangosTexto(_idConfiguracionActual,
                    _txtVerde.Text.Trim(), _txtAmarillo.Text.Trim(), _txtRojo.Text.Trim(), nuevoLimite, nip);
                resultados.Add((r.Exito, r.Exito
                    ? $"Semáforo: Verde {Pct(r.ConfigGuardada!.VerdeMinimo)}–100 %, Amarillo {Pct(r.ConfigGuardada.AmarilloMinimo)}–{Pct(r.ConfigGuardada.AmarilloMaximo)} %, " +
                      $"Rojo ≤ {Pct(r.ConfigGuardada.RojoMaximo)} %, Blanco 0 % · límite de mermas ${r.ConfigGuardada.LimiteEconomicoMerma:N2} MXN."
                    : r.Mensaje));
            }

            if (cambiaCajero)
            {
                resultados.Add(_usuarioNegocio.GuardarCuentaOperativa(cuenta!.IdUsuario,
                    _txtNombreCajero.Text, _txtUsuarioCajero.Text, _txtContrasenaCajero.Text, _txtConfirmarCajero.Text, turno, nip));
            }

            // Al final: si el Administrador cambia su propio NIP, las operaciones anteriores ya usaron el actual
            if (vAdmin.HayCambios)
            {
                resultados.Add(_usuarioNegocio.GuardarCredencialesAdministrador(
                    _txtContrasenaAdmin.Text, _txtConfirmarAdmin.Text, _txtNipAdmin.Text, _txtConfirmarNip.Text, nip));
            }

            bool todoBien = resultados.All(r => r.Exito);
            MessageBox.Show(
                string.Join("\n\n", resultados.Select(r => (r.Exito ? "✔  " : "✖  ") + r.Mensaje)),
                todoBien ? "Configuración guardada" : "Algunos cambios no se guardaron",
                MessageBoxButtons.OK,
                todoBien ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            CargarTodo();
        }

        private void Avisar(string mensaje, string titulo, Control enfocar)
        {
            MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            enfocar.Focus();
        }

        // =====================================================================
        // DISEÑO RESPONSIVO
        // =====================================================================

        private void AjustarPie()
        {
            if (_pnlPie == null || _pnlPie.Width <= 0) return;

            const int margen = 34;
            int y = (_pnlPie.Height - _btnGuardar.Height) / 2;
            _btnGuardar.Location = new Point(_pnlPie.Width - margen - _btnGuardar.Width, y);
            _btnCancelar.Location = new Point(_btnGuardar.Left - _btnCancelar.Width - 14, y);
            _lblPieAviso.Bounds = new Rectangle(margen, (_pnlPie.Height - 44) / 2, Math.Max(0, _btnCancelar.Left - margen - 20), 44);
        }

        private void AjustarLayoutResponsivo()
        {
            if (_pnlContenido == null || _pnlContenido.ClientSize.Width <= 0 || _pnlContenido.ClientSize.Height <= 0) return;

            // Coordenadas dentro del panel desplazable: se suma la posición de desplazamiento actual
            Point o = _pnlContenido.AutoScrollPosition;
            const int margenX = 34;
            const int gapTarjetas = 20;
            int anchoUtil = Math.Max(820, _pnlContenido.ClientSize.Width - (margenX * 2));
            int anchoInterno = anchoUtil - 48;
            int y = 22;

            // Tarjeta 1: Semáforo (4 cajas en fila)
            const int altoSem = 186;
            _cardSemaforo.Bounds = new Rectangle(margenX + o.X, y + o.Y, anchoUtil, altoSem);

            const int gapBox = 16;
            int anchoBox = (anchoInterno - (gapBox * 3)) / 4;
            Panel[] cajas = { _boxVerde, _boxAmarillo, _boxRojo, _boxBlanco };
            for (int i = 0; i < cajas.Length; i++)
            {
                cajas[i].Bounds = new Rectangle(24 + i * (anchoBox + gapBox), 58, anchoBox, 108);
                if (cajas[i].Controls["txtRango"] is TextBox txt)
                    txt.Width = anchoBox - 28;
            }
            y += altoSem + gapTarjetas;

            // Tarjeta 2: Límite de mermas
            const int altoLim = 162;
            _cardLimiteMerma.Bounds = new Rectangle(margenX + o.X, y + o.Y, anchoUtil, altoLim);
            if (_cardLimiteMerma.Controls["sepLim"] is Panel sepLim)
                sepLim.Width = anchoInterno;
            y += altoLim + gapTarjetas;

            // Tarjeta 3: Rol de Cajero (cuadrícula 2 × 2)
            const int altoCaj = 246;
            _cardCajero.Bounds = new Rectangle(margenX + o.X, y + o.Y, anchoUtil, altoCaj);
            if (_cardCajero.Controls["sepCaj"] is Panel sepCaj)
                sepCaj.Width = anchoInterno;

            const int gapColumnas = 24;
            int anchoColumna = (anchoInterno - gapColumnas) / 2;
            for (int i = 0; i < _camposCajero.Length; i++)
            {
                int col = i % 2, fila = i / 2;
                AcomodarCampo(_camposCajero[i], new Rectangle(24 + col * (anchoColumna + gapColumnas), 68 + fila * 72, anchoColumna, 62));
            }
            _lblAvisoCajero.Bounds = new Rectangle(24, 68 + 2 * 72 + 2, anchoInterno, 34);
            y += altoCaj + gapTarjetas;

            // Tarjeta 4: Rol de Administrador (dos campos en fila) con la cuenta en sesión a la derecha
            const int altoAdm = 178;
            _cardAdmin.Bounds = new Rectangle(margenX + o.X, y + o.Y, anchoUtil, altoAdm);
            if (_cardAdmin.Controls["sepAdm"] is Panel sepAdm)
                sepAdm.Width = anchoInterno;
            _lblCuentaAdmin.Location = new Point(anchoUtil - 24 - _lblCuentaAdmin.Width, 22);

            for (int i = 0; i < _camposAdmin.Length; i++)
                AcomodarCampo(_camposAdmin[i], new Rectangle(24 + i * (anchoColumna + gapColumnas), 68, anchoColumna, 62));
            _lblAvisoAdmin.Bounds = new Rectangle(24, 68 + 72 + 2, anchoInterno, 34);
        }
    }
}
