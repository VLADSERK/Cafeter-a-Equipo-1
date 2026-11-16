using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_AutorizacionMermas : UserControl
    {
        private class SolicitudMermaMock
        {
            public string Folio { get; set; } = "";
            public string Insumo { get; set; } = "";
            public string Categoria { get; set; } = "";
            public string CantidadTexto { get; set; } = "";
            public string CodigoLote { get; set; } = "";
            public string Motivo { get; set; } = "";
            public decimal CostoEstimado { get; set; }
            public string FechaRegistro { get; set; } = "";
            public string ReportadoPor { get; set; } = "";
            public string ObservacionesOperador { get; set; } = "";
            public string PorcentajeLote { get; set; } = "";
        }

        private readonly List<SolicitudMermaMock> _solicitudesPendientes = new();
        private readonly CultureInfo _culturaMxn = CultureInfo.GetCultureInfo("es-MX");

        // Barra superior
        private Panel _pnlTopBar = null!;
        private Label _lblRuta = null!;
        private Label _lblTitulo = null!;
        private Label _lblSubtitulo = null!;
        private PanelRedondeado _cardContadorPendientes = null!;
        private Label _lblConteoPendientesVal = null!;

        // Barra de herramientas
        private Panel _pnlToolbar = null!;
        private TextBox _txtBuscar = null!;
        private ComboBox _cmbFiltroMotivo = null!;
        private Button _btnFiltrar = null!;

        // Tabla de solicitudes y pie
        private PanelRedondeado _cardTabla = null!;
        private DataGridView _dgvSolicitudes = null!;
        private Label _lblPieConteo = null!;

        public UC_AutorizacionMermas()
        {
           
            CargarSolicitudesIniciales();
            ConfigurarInterfaz();
            ActualizarTablaYContadores();
        }

        private void CargarSolicitudesIniciales()
        {
            _solicitudesPendientes.AddRange(new[]
            {
                new SolicitudMermaMock
                {
                    Folio = "MER-0105",
                    Insumo = "Café arábica premium",
                    Categoria = "Grano de café",
                    CantidadTexto = "2.5 kg",
                    CodigoLote = "CA-240614-A",
                    Motivo = "Humedad detectada en almacén",
                    CostoEstimado = 865.00m,
                    FechaRegistro = "15 nov 2026, 09:42 h",
                    ReportadoPor = "Carlos Méndez (Barista Turno Matutino)",
                    PorcentajeLote = "25% del lote",
                    ObservacionesOperador = "Bolsa de 2.5 kg presentó filtración de humedad en estante inferior de bodega seca."
                },
                new SolicitudMermaMock
                {
                    Folio = "MER-0104",
                    Insumo = "Leche entera",
                    Categoria = "Lácteos",
                    CantidadTexto = "8 L",
                    CodigoLote = "LE-120625-09",
                    Motivo = "Caducidad próxima / cadena de frío",
                    CostoEstimado = 248.00m,
                    FechaRegistro = "15 nov 2026, 08:16 h",
                    ReportadoPor = "Sofía Ramírez (Cajera / Barista)",
                    PorcentajeLote = "32% del lote",
                    ObservacionesOperador = "Envases próximos a vencer sin rotación suficiente durante el cierre nocturno."
                },
                new SolicitudMermaMock
                {
                    Folio = "MER-0103",
                    Insumo = "Jarabe de vainilla",
                    Categoria = "Saborizantes",
                    CantidadTexto = "3 botellas",
                    CodigoLote = "JV-0525-18",
                    Motivo = "Envase dañado durante recepción",
                    CostoEstimado = 447.00m,
                    FechaRegistro = "14 nov 2026, 17:35 h",
                    ReportadoPor = "Carlos Méndez (Barista Turno Vespertino)",
                    PorcentajeLote = "30% del lote",
                    ObservacionesOperador = "Caja golpeada al descargar pedido; tres botellas presentaron fisura en cuello."
                },
                new SolicitudMermaMock
                {
                    Folio = "MER-0102",
                    Insumo = "Croissant de mantequilla",
                    Categoria = "Panadería",
                    CantidadTexto = "12 pzas",
                    CodigoLote = "CM-130625-M",
                    Motivo = "Producto fuera de estándar",
                    CostoEstimado = 396.00m,
                    FechaRegistro = "14 nov 2026, 16:08 h",
                    ReportadoPor = "Sofía Ramírez (Cajera / Barista)",
                    PorcentajeLote = "40% del lote",
                    ObservacionesOperador = "Piezas con exceso de horneado no aptas para exhibición en vitrina."
                },
                new SolicitudMermaMock
                {
                    Folio = "MER-0101",
                    Insumo = "Chocolate semiamargo",
                    Categoria = "Insumos de barra",
                    CantidadTexto = "1.8 kg",
                    CodigoLote = "CH-280525-B",
                    Motivo = "Derrame accidental en preparación",
                    CostoEstimado = 522.00m,
                    FechaRegistro = "14 nov 2026, 12:21 h",
                    ReportadoPor = "Diego Torres (Barista)",
                    PorcentajeLote = "22% del lote",
                    ObservacionesOperador = "Contenedor volcado durante limpieza de estación de preparación en hora pico."
                }
            });
        }

        private void ConfigurarInterfaz()
        {
            BackColor = TemaCafeteria.FondoCrema;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            // 1. Barra superior con ruta, título y tarjeta amarilla de pendientes
            _pnlTopBar = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Top,
                Height = 108
            };
            _pnlTopBar.Paint += (s, e) =>
            {
                using var pen = new Pen(TemaCafeteria.BordeSuave, 1);
                e.Graphics.DrawLine(pen, 0, _pnlTopBar.Height - 1, _pnlTopBar.Width, _pnlTopBar.Height - 1);
            };

            _lblRuta = new Label
            {
                Text = "Sucursal Centro / Control de inventario / Autorización de mermas",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(34, 14),
                AutoSize = true
            };

            _lblTitulo = new Label
            {
                Text = "Autorización de mermas",
                Font = new Font("Segoe UI", 18.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(32, 36),
                AutoSize = true
            };

            _lblSubtitulo = new Label
            {
                Text = "Revisa y autoriza las pérdidas de inventario registradas por el personal",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(34, 72),
                AutoSize = true
            };

            // Tarjeta amarilla superior derecha (SOLICITUDES PENDIENTES)
            _cardContadorPendientes = new PanelRedondeado
            {
                Size = new Size(265, 66),
                RadioBorde = 12,
                BackColor = ColorTranslator.FromHtml("#FFF8E1")
            };
            _cardContadorPendientes.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#F5C156"), 1.2f);
                e.Graphics.DrawRectangle(pen, 0, 0, _cardContadorPendientes.Width - 1, _cardContadorPendientes.Height - 1);
            };

            Label lblTitPend = new Label
            {
                Text = "SOLICITUDES PENDIENTES",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#B26A00"),
                Location = new Point(16, 11),
                AutoSize = true
            };

            _lblConteoPendientesVal = new Label
            {
                Text = "5 Solicitudes pendientes",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#5C3A00"),
                Location = new Point(16, 30),
                AutoSize = true
            };

            _cardContadorPendientes.Controls.AddRange(new Control[] { lblTitPend, _lblConteoPendientesVal });
            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo, _cardContadorPendientes });

            // 2. Barra de herramientas (Buscar insumo o lote + Filtro + Botón Filtrar)
            _pnlToolbar = new Panel { BackColor = Color.Transparent };

            Panel pnlBuscar = new Panel
            {
                Name = "pnlBuscar",
                Location = new Point(0, 4),
                Size = new Size(340, 38),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblLupa = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(10, 8),
                AutoSize = true
            };

            _txtBuscar = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                PlaceholderText = "Buscar insumo o lote...",
                Location = new Point(34, 9),
                Width = 294
            };
            _txtBuscar.TextChanged += (s, e) => ActualizarTablaYContadores();
            pnlBuscar.Controls.AddRange(new Control[] { lblLupa, _txtBuscar });

            _cmbFiltroMotivo = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 230
            };
            _cmbFiltroMotivo.Items.AddRange(new object[]
            {
                "Todos los motivos",
                "Humedad detectada en almacén",
                "Caducidad próxima / cadena de frío",
                "Envase dañado durante recepción",
                "Producto fuera de estándar",
                "Derrame accidental en preparación"
            });
            _cmbFiltroMotivo.SelectedIndex = 0;

            _btnFiltrar = new Button
            {
                Text = "Filtrar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(96, 38),
                Cursor = Cursors.Hand
            };
            _btnFiltrar.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
            _btnFiltrar.FlatAppearance.BorderSize = 1;
            _btnFiltrar.Click += (s, e) => ActualizarTablaYContadores();

            _pnlToolbar.Controls.AddRange(new Control[] { pnlBuscar, _cmbFiltroMotivo, _btnFiltrar });

            // 3. Tarjeta con Tabla de Solicitudes
            _cardTabla = new PanelRedondeado
            {
                RadioBorde = 16,
                BackColor = Color.White
            };

            ConfigurarTablaSolicitudes();

            _lblPieConteo = new Label
            {
                Text = "Mostrando 5 de 5 solicitudes",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                AutoSize = true
            };

            _cardTabla.Controls.AddRange(new Control[] { _dgvSolicitudes, _lblPieConteo });

            Controls.AddRange(new Control[] { _pnlToolbar, _cardTabla, _pnlTopBar });

            Resize += (s, e) => AjustarLayoutResponsivo();
            Load += (s, e) => AjustarLayoutResponsivo();
            AjustarLayoutResponsivo();
        }

        private void ConfigurarTablaSolicitudes()
        {
            _dgvSolicitudes = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 44,
                RowTemplate = { Height = 56 },
                EnableHeadersVisualStyles = false,
                GridColor = TemaCafeteria.BordeSuave,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _dgvSolicitudes.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ColorTranslator.FromHtml("#F5F2EE"),
                ForeColor = TemaCafeteria.TextoSecundario,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };

            _dgvSolicitudes.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TemaCafeteria.TextoOscuro,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                SelectionBackColor = ColorTranslator.FromHtml("#FAF6F1"),
                SelectionForeColor = TemaCafeteria.TextoOscuro,
                Padding = new Padding(12, 4, 12, 4)
            };

            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFolio", Visible = false });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInsumo", HeaderText = "INSUMO", FillWeight = 22 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCantidad", HeaderText = "CANTIDAD", FillWeight = 11 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLote", HeaderText = "LOTE", FillWeight = 14 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMotivo", HeaderText = "MOTIVO", FillWeight = 25 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCosto", HeaderText = "COSTO ESTIMADO", FillWeight = 14 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFecha", HeaderText = "FECHA DE REGISTRO", FillWeight = 16 });

            var colAccion = new DataGridViewButtonColumn
            {
                Name = "colAccion",
                HeaderText = "ACCIONES",
                Text = "Revisar",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 11
            };
            colAccion.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1E64D8");
            colAccion.DefaultCellStyle.ForeColor = Color.White;
            colAccion.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            colAccion.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvSolicitudes.Columns.Add(colAccion);

            // Formato personalizado para negritas en Insumo y Costo Estimado
            _dgvSolicitudes.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                if (e.ColumnIndex == _dgvSolicitudes.Columns["colInsumo"]!.Index ||
                    e.ColumnIndex == _dgvSolicitudes.Columns["colCosto"]!.Index)
                {
                    e.CellStyle!.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            };

            _dgvSolicitudes.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == _dgvSolicitudes.Columns["colAccion"]!.Index)
                {
                    string folio = _dgvSolicitudes.Rows[e.RowIndex].Cells["colFolio"].Value?.ToString() ?? "";
                    var solicitud = _solicitudesPendientes.FirstOrDefault(x => x.Folio == folio);
                    if (solicitud != null)
                    {
                        AbrirModalRevisionSolicitud(solicitud);
                    }
                }
            };
        }

        private void ActualizarTablaYContadores()
        {
            string busqueda = _txtBuscar.Text.Trim().ToLowerInvariant();
            string motivoFiltro = _cmbFiltroMotivo.SelectedItem?.ToString() ?? "Todos los motivos";

            _dgvSolicitudes.Rows.Clear();
            int mostradas = 0;

            foreach (var sol in _solicitudesPendientes)
            {
                if (motivoFiltro != "Todos los motivos" && sol.Motivo != motivoFiltro)
                    continue;

                if (!string.IsNullOrEmpty(busqueda))
                {
                    bool coincide = sol.Insumo.ToLowerInvariant().Contains(busqueda) ||
                                    sol.CodigoLote.ToLowerInvariant().Contains(busqueda) ||
                                    sol.Categoria.ToLowerInvariant().Contains(busqueda);
                    if (!coincide) continue;
                }

                _dgvSolicitudes.Rows.Add(
                    sol.Folio,
                    $"{sol.Insumo}\n({sol.Categoria})",
                    sol.CantidadTexto,
                    sol.CodigoLote,
                    sol.Motivo,
                    sol.CostoEstimado.ToString("C2", _culturaMxn),
                    sol.FechaRegistro,
                    "Revisar"
                );
                mostradas++;
            }

            int total = _solicitudesPendientes.Count;
            _lblConteoPendientesVal.Text = $"{total} {(total == 1 ? "Solicitud pendiente" : "Solicitudes pendientes")}";
            _lblPieConteo.Text = $"Mostrando {mostradas} de {total} solicitudes";
        }

        private const string NIP_AUTORIZACION_ADMIN = "1234";

        private void AbrirModalRevisionSolicitud(SolicitudMermaMock sol)
        {
            using Form dlg = new Form
            {
                Text = $"Revisión de Solicitud de Merma — {sol.Folio}",
                Size = new Size(540, 475),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblTit = new Label
            {
                Text = $"{sol.Insumo} ({sol.CantidadTexto})",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = $"Folio: {sol.Folio}  |  Lote PEPS: {sol.CodigoLote}  |  Impacto: {sol.PorcentajeLote}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TemaCafeteria.CafePrimario,
                Location = new Point(24, 48),
                AutoSize = true
            };

            Panel pnlDatos = new Panel
            {
                Location = new Point(24, 78),
                Size = new Size(476, 175),
                BackColor = ColorTranslator.FromHtml("#FAF8F5"),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblDetalle = new Label
            {
                Text = $"• Reportado por: {sol.ReportadoPor}\n" +
                       $"• Fecha y hora: {sol.FechaRegistro}\n" +
                       $"• Motivo registrado: {sol.Motivo}\n" +
                       $"• Costo estimado de pérdida: {sol.CostoEstimado.ToString("C2", _culturaMxn)}\n\n" +
                       $"Observaciones del personal:\n\"{sol.ObservacionesOperador}\"",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(14, 12),
                Size = new Size(446, 150)
            };
            pnlDatos.Controls.Add(lblDetalle);

            Label lblNotaTit = new Label
            {
                Text = "Comentario de auditoría del Administrador (obligatorio para rechazar):",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 266),
                AutoSize = true
            };

            TextBox txtNotaAdmin = new TextBox
            {
                Multiline = true,
                Location = new Point(24, 288),
                Size = new Size(476, 70),
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "Escribe observaciones de autorización o motivo de rechazo..."
            };

            Button btnRechazar = new Button
            {
                Text = "✕  Rechazar solicitud",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#C62828"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 376),
                Size = new Size(210, 42),
                Cursor = Cursors.Hand
            };
            btnRechazar.FlatAppearance.BorderSize = 0;
            btnRechazar.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtNotaAdmin.Text))
                {
                    MessageBox.Show("Indica el motivo por el cual se rechaza la merma.", "Comentario obligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNotaAdmin.Focus();
                    return;
                }

                // Abre la segunda ventanita solicitando el NIP
                if (!SolicitarNipAdministrador(dlg, $"Rechazar merma {sol.Folio}"))
                    return;

                _solicitudesPendientes.Remove(sol);
                ActualizarTablaYContadores();
                dlg.Close();
                MessageBox.Show($"La solicitud {sol.Folio} fue RECHAZADA. No se descontó el lote {sol.CodigoLote}.", "Solicitud Rechazada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnAutorizar = new Button
            {
                Text = "✓  Autorizar merma",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#1F4E35"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(250, 376),
                Size = new Size(250, 42),
                Cursor = Cursors.Hand
            };
            btnAutorizar.FlatAppearance.BorderSize = 0;
            btnAutorizar.Click += (s, e) =>
            {
                // Abre la segunda ventanita solicitando el NIP
                if (!SolicitarNipAdministrador(dlg, $"Autorizar merma {sol.Folio}"))
                    return;

                _solicitudesPendientes.Remove(sol);
                ActualizarTablaYContadores();
                dlg.Close();
                MessageBox.Show(
                    $"Merma {sol.Folio} AUTORIZADA correctamente.\nSe aplicó el descuento de {sol.CantidadTexto} al lote {sol.CodigoLote}.",
                    "Merma Autorizada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };

            dlg.Controls.AddRange(new Control[] { lblTit, lblSub, pnlDatos, lblNotaTit, txtNotaAdmin, btnRechazar, btnAutorizar });
            dlg.ShowDialog(this);
        }

        private bool SolicitarNipAdministrador(IWin32Window owner, string accionConfirmar)
        {
            bool autorizado = false;

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
                Text = $"Ingresa tu NIP de Administrador para confirmar:\n{accionConfirmar} ",
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
                if (txtNip.Text.Trim() != NIP_AUTORIZACION_ADMIN)
                {
                    MessageBox.Show("NIP incorrecto. Intenta nuevamente.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtNip.Clear();
                    txtNip.Focus();
                    return;
                }

                autorizado = true;
                dlgNip.Close();
            };

            dlgNip.AcceptButton = btnConfirmar;
            dlgNip.Shown += (s, e) => txtNip.Focus();
            dlgNip.Controls.AddRange(new Control[] { lblIcono, lblInstruccion, txtNip, btnCancelar, btnConfirmar });
            dlgNip.ShowDialog(owner);

            return autorizado;
        }
        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 32;
            int anchoUtil = Math.Max(780, ClientSize.Width - (margenX * 2));

            // Posicionar tarjeta amarilla de pendientes a la derecha de la barra superior
            _cardContadorPendientes.Location = new Point(_pnlTopBar.Width - _cardContadorPendientes.Width - margenX, 20);

            // Barra de herramientas
            int topToolbar = _pnlTopBar.Height + 18;
            _pnlToolbar.Bounds = new Rectangle(margenX, topToolbar, anchoUtil, 46);

            _btnFiltrar.Location = new Point(anchoUtil - _btnFiltrar.Width, 4);
            _cmbFiltroMotivo.Location = new Point(_btnFiltrar.Left - _cmbFiltroMotivo.Width - 12, 6);

            // Tabla y pie de tabla
            int yTabla = topToolbar + 54;
            int altoTablaCard = Math.Max(340, ClientSize.Height - yTabla - 22);
            _cardTabla.Bounds = new Rectangle(margenX, yTabla, anchoUtil, altoTablaCard);

            int altoPie = 38;
            _dgvSolicitudes.Bounds = new Rectangle(12, 12, anchoUtil - 24, altoTablaCard - altoPie - 16);
            _lblPieConteo.Location = new Point(16, altoTablaCard - 28);
        }
    }
}
