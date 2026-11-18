using CapaLogica;
using CapaPresentacion.Estilos;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace CapaPresentaciòn.Formularios
{
    public partial class FrmLogin : Form
    {
        private Panel _contenedorCentral = null!;
        private PictureBox _picLogo = null!;
        private Label _lblSubtituloSuperior = null!;
        private Label _lblTitulo = null!;
        private PanelRedondeado _tarjetaLogin = null!;
        private Label _lblUsuario = null!;
        private CajaTextoRedondeada _txtUsuario = null!;
        private Label _lblContrasena = null!;
        private CajaTextoRedondeada _txtContrasena = null!;
        private Label _lblError = null!;
        private BotonRedondeado _btnIniciarSesion = null!;
        private Label _lblPiePagina = null!;

        public FrmLogin()
        {
            InitializeComponent();
            ConfigurarPantallaLogin();
        }

        private void ConfigurarPantallaLogin()
        {
            // Configuración general de ventana (Windows 11 - RNF-12)
            Text = "Sistema de Gestión de Cafetería - Inicio de Sesión";
            BackColor = TemaCafeteria.FondoCrema;
            MinimumSize = new Size(900, 680);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            // Contenedor central que mantiene todo alineado al redimensionar
            _contenedorCentral = new Panel
            {
                Size = new Size(460, 640),
                BackColor = Color.Transparent
            };

            // 1. Logotipo circular superior
            _picLogo = new PictureBox
            {
                Size = new Size(155, 155),
                Location = new Point((_contenedorCentral.Width - 155) / 2, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = TemaCafeteria.BarraLateralOscura
            };
            _picLogo.Paint += PicLogo_Paint;
            CargarLogoSiExiste();

            // 2. Subtítulo superior ("Todo listo para un nuevo día.")
            _lblSubtituloSuperior = new Label
            {
                Text = "Todo listo para un nuevo día.",
                Font = TemaCafeteria.FuenteSubtitulo,
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = false,
                Size = new Size(460, 22),
                Location = new Point(0, 175),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 3. Título principal ("Bienvenido al Sistema")
            _lblTitulo = new Label
            {
                Text = "Bienvenido al Sistema",
                Font = TemaCafeteria.FuenteTituloPrincipal,
                ForeColor = TemaCafeteria.TextoOscuro,
                AutoSize = false,
                Size = new Size(460, 48),
                Location = new Point(0, 198),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 4. Tarjeta blanca central
            _tarjetaLogin = new PanelRedondeado
            {
                Size = new Size(400, 315),
                Location = new Point(30, 265),
                RadioBorde = 22
            };

            _lblUsuario = new Label
            {
                Text = "Usuario",
                Font = TemaCafeteria.FuenteEtiqueta,
                ForeColor = TemaCafeteria.TextoOscuro,
                AutoSize = true,
                Location = new Point(30, 28)
            };

            _txtUsuario = new CajaTextoRedondeada
            {
                Location = new Point(30, 52),
                Size = new Size(340, 44),
                Placeholder = "Ingrese usuario"
            };

            _lblContrasena = new Label
            {
                Text = "Contraseña",
                Font = TemaCafeteria.FuenteEtiqueta,
                ForeColor = TemaCafeteria.TextoOscuro,
                AutoSize = true,
                Location = new Point(30, 112)
            };

            _txtContrasena = new CajaTextoRedondeada
            {
                Location = new Point(30, 136),
                Size = new Size(340, 44),
                Placeholder = "Ingrese contraseña",
                EsContrasena = true // RIU-09: Ocultar caracteres ingresados
            };

            // Mensaje de error (Pantalla 1.1 / RF-11 / RIU-08)
            _lblError = new Label
            {
                Text = "*Usuario o contraseña incorrectos",
                Font = TemaCafeteria.FuenteError,
                ForeColor = TemaCafeteria.ErrorRojo,
                AutoSize = true,
                Location = new Point(30, 192),
                Visible = false
            };

            _btnIniciarSesion = new BotonRedondeado
            {
                Text = "Iniciar sesión",
                Location = new Point(30, 224),
                Size = new Size(340, 48),
                RadioBorde = 10
            };
            _btnIniciarSesion.Click += BtnIniciarSesion_Click;

            _tarjetaLogin.Controls.AddRange(new Control[]
            {
                _lblUsuario, _txtUsuario,
                _lblContrasena, _txtContrasena,
                _lblError, _btnIniciarSesion
            });

            // 5. Texto inferior ("Una buena jornada comienza con un café.")
            _lblPiePagina = new Label
            {
                Text = "Una buena jornada comienza con un café.",
                Font = TemaCafeteria.FuenteSubtitulo,
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = false,
                Size = new Size(460, 24),
                Location = new Point(0, 600),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _contenedorCentral.Controls.AddRange(new Control[]
            {
                _picLogo,
                _lblSubtituloSuperior,
                _lblTitulo,
                _tarjetaLogin,
                _lblPiePagina
            });

            Controls.Add(_contenedorCentral);
            AcceptButton = _btnIniciarSesion; // Permite iniciar sesión presionando Enter

            Resize += (s, e) => CentrarContenedor();
            CentrarContenedor();
        }

        private void CentrarContenedor()
        {
            _contenedorCentral.Location = new Point(
                Math.Max(0, (ClientSize.Width - _contenedorCentral.Width) / 2),
                Math.Max(0, (ClientSize.Height - _contenedorCentral.Height) / 2)
            );
        }

        private void CargarLogoSiExiste()
        {
            // Coloca tu imagen PNG como "logo_cafeteria.png" en la carpeta Assets (Copiar siempre)
            string rutaLogo = Path.Combine(AppContext.BaseDirectory, "Assets", "logo_cafeteria.png");
            if (File.Exists(rutaLogo))
            {
                using var stream = new FileStream(rutaLogo, FileMode.Open, FileAccess.Read);
                _picLogo.Image = Image.FromStream(stream);
            }
        }

        private void PicLogo_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = new GraphicsPath();
            path.AddEllipse(0, 0, _picLogo.Width - 1, _picLogo.Height - 1);
            _picLogo.Region = new Region(path);

            using Pen borde = new Pen(TemaCafeteria.CafePrimario, 4F);
            e.Graphics.DrawEllipse(borde, 2, 2, _picLogo.Width - 5, _picLogo.Height - 5);
        }

        private void BtnIniciarSesion_Click(object? sender, EventArgs e)
        {
            string usuario = _txtUsuario.Texto.Trim();
            string contrasena = _txtContrasena.Texto;

            // Validación visual básica antes de continuar (RIU-08)
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                MostrarErrorCredenciales("*Usuario o contraseña incorrectos");
                return;
            }

            Cursor = Cursors.WaitCursor;

            try
            {
                var auth = new AuthNegocio();
                var resultado = auth.IniciarSesion(usuario, contrasena);

                if (!resultado.Exito || resultado.Usuario == null)
                {
                    // Si fue error de conexión a MySQL muestra el detalle; si fueron credenciales usa tu estado visual (RF-11)
                    if (resultado.Mensaje.StartsWith("Error de conexión"))
                    {
                        MessageBox.Show(resultado.Mensaje, "Error de Base de Datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MostrarErrorCredenciales("*Usuario o contraseña incorrectos");
                    }
                    return;
                }

                // Credenciales válidas desde MySQL
                _lblError.Visible = false;
                _txtUsuario.TieneError = false;
                _txtContrasena.TieneError = false;

                // RIU-09: cada rol entra solo a su ventana. Un rol desconocido no entra a ninguna.
                string rol = resultado.Usuario.NombreRol;
                bool esAdmin = rol.Equals(Permisos.RolAdministrador, StringComparison.OrdinalIgnoreCase);
                if (!esAdmin && !Permisos.EsOperativo(rol))
                {
                    SesionActual.Cerrar();
                    MessageBox.Show($"El rol \"{rol}\" no tiene módulos asignados. Contacta al Administrador.",
                        "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtContrasena.Texto = string.Empty;
                    return;
                }

                Hide();

                if (esAdmin)
                {
                    using FrmPrincipalAdmin ventanaAdmin = new FrmPrincipalAdmin();
                    ventanaAdmin.ShowDialog();
                }
                else
                {
                    // Cajero y Barista: punto de venta, recepción, inventario y mermas
                    using FrmPrincipalOperativo ventanaCajero = new FrmPrincipalOperativo();
                    ventanaCajero.ShowDialog();
                }

                // Al cerrar sesión, limpia la sesión activa y restaura el login limpio
                SesionActual.Cerrar();
                _txtUsuario.Texto = string.Empty;
                _txtContrasena.Texto = string.Empty;
                _txtUsuario.TieneError = false;
                _txtContrasena.TieneError = false;
                Show();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void MostrarErrorCredenciales(string mensaje)
        {
            _lblError.Text = mensaje;
            _lblError.Visible = true;
            _txtUsuario.TieneError = true;
            _txtContrasena.TieneError = true;
            _txtContrasena.Texto = string.Empty;
        }
    }
}