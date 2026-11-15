using CapaPresentacion.Estilos; // Cambia a CapaPresentacion.Estilos si tu proyecto no lleva acento
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapaPresentaciòn.Controles
{
    public partial class UC_MenuOperativo : UserControl
    {
        private Panel _contenedorCentral = null!;
        private Label _lblCafeteria = null!;
        private Label _lblMenuPrincipal = null!;

        private PanelRedondeado _cardVender = null!;
        private PanelRedondeado _cardRecopilacion = null!;
        private PanelRedondeado _cardInventario = null!;
        private PanelRedondeado _cardMerma = null!;
        private Button _btnSalir = null!;

        // Eventos para que la ventana maestra sepa qué pantalla abrir
        public event EventHandler? IrAVender;
        public event EventHandler? IrARecopilacion;
        public event EventHandler? IrAInventario;
        public event EventHandler? IrAMerma;
        public event EventHandler? SalirSesion;

        public UC_MenuOperativo()
        {
            InitializeComponent();
            ConfigurarMenuOperativo();
        }

        private void ConfigurarMenuOperativo()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            _contenedorCentral = new Panel
            {
                BackColor = Color.Transparent
            };

            // Encabezados superiores
            _lblCafeteria = new Label
            {
                Text = "CAFETERÍA",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            _lblMenuPrincipal = new Label
            {
                Text = "Menú principal",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                AutoSize = true
            };

            // Tarjetas 01 a 04
            _cardVender = CrearTarjetaOpcion("01", "Vender", "Registrar una nueva venta", () => IrAVender?.Invoke(this, EventArgs.Empty));
            _cardRecopilacion = CrearTarjetaOpcion("02", "Recopilación Mercancía", "Registrar la entrada de productos", () => IrARecopilacion?.Invoke(this, EventArgs.Empty));
            _cardInventario = CrearTarjetaOpcion("03", "Inventario", "Consultar existencias y productos", () => IrAInventario?.Invoke(this, EventArgs.Empty));
            _cardMerma = CrearTarjetaOpcion("04", "Merma", "Registrar pérdidas de mercancía", () => IrAMerma?.Invoke(this, EventArgs.Empty));

            // Botón rojo inferior "salir"
            _btnSalir = new Button
            {
                Text = "salir",
                Font = new Font("Segoe UI", 26F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#B22222"),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnSalir.FlatAppearance.BorderSize = 0;
            _btnSalir.Click += (s, e) => SalirSesion?.Invoke(this, EventArgs.Empty);

            _contenedorCentral.Controls.AddRange(new Control[]
            {
                _lblCafeteria,
                _lblMenuPrincipal,
                _cardVender,
                _cardRecopilacion,
                _cardInventario,
                _cardMerma,
                _btnSalir
            });

            Controls.Add(_contenedorCentral);

            // Recalcular tamaños cada vez que cambie el tamaño o se maximice la ventana
            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private PanelRedondeado CrearTarjetaOpcion(string numero, string titulo, string descripcion, Action accionAlHacerClic)
        {
            PanelRedondeado tarjeta = new PanelRedondeado
            {
                RadioBorde = 16,
                Cursor = Cursors.Hand,
                BackColor = TemaCafeteria.SuperficieTarjeta
            };

            Label lblNumero = new Label
            {
                Name = "lblNumero",
                Text = numero,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            Label lblTitulo = new Label
            {
                Name = "lblTitulo",
                Text = titulo,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                AutoSize = true
            };

            Label lblDesc = new Label
            {
                Name = "lblDesc",
                Text = descripcion,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            Label lblFlecha = new Label
            {
                Name = "lblFlecha",
                Text = "→",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                AutoSize = true
            };

            tarjeta.Click += (s, e) => accionAlHacerClic();
            foreach (Control c in new Control[] { lblNumero, lblTitulo, lblDesc, lblFlecha })
            {
                c.Cursor = Cursors.Hand;
                c.Click += (s, e) => accionAlHacerClic();
                tarjeta.Controls.Add(c);
            }

            return tarjeta;
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            // Márgenes exteriores proporcionales para cubrir ~90% de la pantalla
            int margenHorizontal = Math.Max(48, (int)(ClientSize.Width * 0.05));
            int margenVertical = Math.Max(28, (int)(ClientSize.Height * 0.04));

            int anchoUtil = Math.Max(700, ClientSize.Width - (margenHorizontal * 2));
            int altoUtil = Math.Max(520, ClientSize.Height - (margenVertical * 2));

            _contenedorCentral.Bounds = new Rectangle(margenHorizontal, margenVertical, anchoUtil, altoUtil);

            // Encabezado superior: CAFETERÍA a la izquierda y Menú principal alineado al borde derecho
            int altoHeader = 44;
            _lblCafeteria.Location = new Point(4, (altoHeader - _lblCafeteria.Height) / 2);
            _lblMenuPrincipal.Location = new Point(anchoUtil - _lblMenuPrincipal.Width - 4, (altoHeader - _lblMenuPrincipal.Height) / 2);

            // Distribución vertical dinámica de las 4 tarjetas y el botón salir
            int separacion = Math.Max(14, (int)(altoUtil * 0.022));
            int altoBotonSalir = Math.Clamp((int)(altoUtil * 0.095), 54, 74);

            int espacioDisponibleTarjetas = altoUtil - altoHeader - altoBotonSalir - (separacion * 5);
            int altoTarjeta = Math.Max(84, espacioDisponibleTarjetas / 4);

            PanelRedondeado[] tarjetas = { _cardVender, _cardRecopilacion, _cardInventario, _cardMerma };
            int yActual = altoHeader + separacion;

            foreach (var tarjeta in tarjetas)
            {
                tarjeta.Bounds = new Rectangle(0, yActual, anchoUtil, altoTarjeta);
                ReposicionarTextosTarjeta(tarjeta);
                tarjeta.Invalidate();
                yActual += altoTarjeta + separacion;
            }

            // Botón rojo inferior "salir" ocupando todo el ancho
            _btnSalir.Bounds = new Rectangle(0, yActual, anchoUtil, altoBotonSalir);
        }

        private void ReposicionarTextosTarjeta(PanelRedondeado tarjeta)
        {
            Control? lblNumero = tarjeta.Controls["lblNumero"];
            Control? lblTitulo = tarjeta.Controls["lblTitulo"];
            Control? lblDesc = tarjeta.Controls["lblDesc"];
            Control? lblFlecha = tarjeta.Controls["lblFlecha"];

            if (lblNumero != null)
                lblNumero.Location = new Point(36, (tarjeta.Height - lblNumero.Height) / 2);

            if (lblTitulo != null && lblDesc != null)
            {
                int bloqueTextoAlto = lblTitulo.Height + lblDesc.Height + 2;
                int inicioY = Math.Max(10, (tarjeta.Height - bloqueTextoAlto) / 2);
                lblTitulo.Location = new Point(95, inicioY);
                lblDesc.Location = new Point(97, inicioY + lblTitulo.Height + 2);
            }

            if (lblFlecha != null)
                lblFlecha.Location = new Point(tarjeta.Width - lblFlecha.Width - 40, (tarjeta.Height - lblFlecha.Height) / 2);
        }
    }
}