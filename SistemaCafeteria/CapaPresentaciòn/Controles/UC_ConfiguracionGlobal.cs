using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CapaPresentacion.Estilos;
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


        public UC_ConfiguracionGlobal()
        {
           
            ConfigurarInterfaz();
            CargarValoresGuardados();
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

            Controls.AddRange(new Control[]
            {
                _cardSemaforo,
                _cardLimiteMerma,
                _btnCancelar,
                _btnGuardar,
                _pnlTopBar
            });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
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

            if (!decimal.TryParse(_txtLimiteMerma.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal nuevoLimite) || nuevoLimite < 0)
            {
                MessageBox.Show("Ingresa un monto económico válido en MXN para el límite de mermas.", "Monto inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 34;
            int anchoUtil = Math.Max(820, ClientSize.Width - (margenX * 2));

            // Tarjeta 1: Semáforo de inventario
            int ySem = _pnlTopBar.Height + 22;
            int altoSem = 176;
            _cardSemaforo.Bounds = new Rectangle(margenX, ySem, anchoUtil, altoSem);

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
            _cardLimiteMerma.Bounds = new Rectangle(margenX, yLim, anchoUtil, altoLim);
            if (_cardLimiteMerma.Controls["sepLim"] is Panel sep)
                sep.Width = anchoUtil - 48;

            // Botones inferiores alineados a la derecha debajo de la segunda tarjeta
            int yBotones = yLim + altoLim + 24;
            _btnGuardar.Location = new Point(margenX + anchoUtil - _btnGuardar.Width, yBotones);
            _btnCancelar.Location = new Point(_btnGuardar.Left - _btnCancelar.Width - 14, yBotones);
        }
    }
}