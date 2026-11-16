using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;
using CapaLogica;
using CapaEntidades;

namespace CapaPresentaciòn.Controles.Admin
{
    public partial class UC_AutorizacionMermas : UserControl
    {
        private readonly AutorizacionMermaNegocio _mermaNegocio = new();
        private List<Merma> _solicitudesPendientes = new();
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
          
            ConfigurarInterfaz();
            CargarSolicitudesDesdeMysql();
        }

        private void CargarSolicitudesDesdeMysql()
        {
            try
            {
                _solicitudesPendientes = _mermaNegocio.ObtenerPendientes();
                ActualizarTablaYContadores();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar las solicitudes pendientes desde MySQL:\n{ex.Message}",
                    "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                Text = "0 Solicitudes pendientes",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#5C3A00"),
                Location = new Point(16, 30),
                AutoSize = true
            };

            _cardContadorPendientes.Controls.AddRange(new Control[] { lblTitPend, _lblConteoPendientesVal });
            _pnlTopBar.Controls.AddRange(new Control[] { _lblRuta, _lblTitulo, _lblSubtitulo, _cardContadorPendientes });

            // 2. Barra de herramientas
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
                "Caducidad vencida",
                "Derrame o accidente en barra",
                "Producto dañado en almacén",
                "Error de preparación en receta",
                "Mal estado / Calidad no óptima"
            });
            _cmbFiltroMotivo.SelectedIndex = 0;
            _cmbFiltroMotivo.SelectedIndexChanged += (s, e) => ActualizarTablaYContadores();

            _btnFiltrar = new Button
            {
                Text = "Refrescar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(96, 38),
                Cursor = Cursors.Hand
            };
            _btnFiltrar.FlatAppearance.BorderColor = TemaCafeteria.BordeSuave;
            _btnFiltrar.FlatAppearance.BorderSize = 1;
            _btnFiltrar.Click += (s, e) => CargarSolicitudesDesdeMysql();

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
                Text = "Mostrando 0 de 0 solicitudes",
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

            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIdMerma", Visible = false });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInsumo", HeaderText = "INSUMO", FillWeight = 24 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCantidad", HeaderText = "CANTIDAD", FillWeight = 12 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLote", HeaderText = "LOTE PEPS", FillWeight = 15 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMotivo", HeaderText = "MOTIVO", FillWeight = 22 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCosto", HeaderText = "COSTO ESTIMADO", FillWeight = 14 });
            _dgvSolicitudes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFecha", HeaderText = "FECHA DE REGISTRO", FillWeight = 15 });

            var colAccion = new DataGridViewButtonColumn
            {
                Name = "colAccion",
                HeaderText = "ACCIONES",
                Text = "Revisar",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 10
            };
            colAccion.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1E64D8");
            colAccion.DefaultCellStyle.ForeColor = Color.White;
            colAccion.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            colAccion.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvSolicitudes.Columns.Add(colAccion);

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
                    if (int.TryParse(_dgvSolicitudes.Rows[e.RowIndex].Cells["colIdMerma"].Value?.ToString(), out int idMerma))
                    {
                        var solicitud = _solicitudesPendientes.FirstOrDefault(x => x.IdMerma == idMerma);
                        if (solicitud != null)
                        {
                            AbrirModalRevisionSolicitud(solicitud);
                        }
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
                    bool coincide = sol.NombreInsumo.ToLowerInvariant().Contains(busqueda) ||
                                    sol.CodigoLote.ToLowerInvariant().Contains(busqueda);
                    if (!coincide) continue;
                }

                _dgvSolicitudes.Rows.Add(
                    sol.IdMerma,
                    $"{sol.NombreInsumo}",
                    $"{sol.Cantidad:N2} {sol.UnidadAbreviatura}",
                    sol.CodigoLote,
                    sol.Motivo,
                    sol.CostoEstimado.ToString("C2", _culturaMxn),
                    sol.FechaMerma.ToString("dd MMM yyyy, hh:mm tt"),
                    "Revisar"
                );
                mostradas++;
            }

            int total = _solicitudesPendientes.Count;
            _lblConteoPendientesVal.Text = $"{total} {(total == 1 ? "Solicitud pendiente" : "Solicitudes pendientes")}";
            _lblPieConteo.Text = $"Mostrando {mostradas} de {total} solicitudes";
        }
        private bool SolicitarNipAdministrador(IWin32Window owner, string accionConfirmar)
        {
            bool autorizado = false;

            using Form dlgNip = new Form
            {
                Text = "Confirmación de Seguridad",
                Size = new Size(390, 275),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblIcono = new Label
            {
                Text = "🔒  NIP de Administrador",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblInstruccion = new Label
            {
                Text = $"Ingresa el NIP de 4 dígitos para confirmar:\n{accionConfirmar}",
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
                Location = new Point(24, 166),
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
                Location = new Point(190, 166),
                Size = new Size(162, 40),
                Cursor = Cursors.Hand
            };
            btnConfirmar.FlatAppearance.BorderSize = 0;
            btnConfirmar.Click += (s, e) =>
            {
                string nipIngresado = txtNip.Text.Trim();
                if (nipIngresado.Length != 4)
                {
                    MessageBox.Show("El NIP de administrador consta de 4 dígitos.", "Nip incompleto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var auth = new AuthNegocio();
                var resultadoNip = auth.ValidarNipAdministrador(nipIngresado);

                if (!resultadoNip.Autorizado)
                {
                    MessageBox.Show("NIP incorrecto o el administrador no cuenta con privilegios activos.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private void AbrirModalRevisionSolicitud(Merma sol)
        {
            using Form dlg = new Form
            {
                Text = $"Revisión de Solicitud de Merma — #MRM-{sol.IdMerma:000}",
                Size = new Size(540, 485),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblTit = new Label
            {
                Text = $"{sol.NombreInsumo} ({sol.Cantidad:N2} {sol.UnidadAbreviatura})",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = $"Folio ID: #MRM-{sol.IdMerma:000}  |  Lote PEPS: {sol.CodigoLote}",
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
                Text = $"• Reportado por Usuario ID: {sol.IdUsuarioRegistra}\n" +
                       $"• Fecha y hora: {sol.FechaMerma:dd/MM/yyyy hh:mm tt}\n" +
                       $"• Motivo registrado: {sol.Motivo}\n" +
                       $"• Costo estimado de pérdida: {sol.CostoEstimado.ToString("C2", _culturaMxn)}\n\n" +
                       $"Observaciones del personal:\n\"{sol.Descripcion}\"",
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

                // 1. EXIGIR NIP/VALIDACIÓN DE ADMINISTRADOR PARA RECHAZAR
                if (!SolicitarNipAdministrador(dlg, $"Rechazar merma #MRM-{sol.IdMerma:000}"))
                    return;

                var resultado = _mermaNegocio.ResolverSolicitud(sol.IdMerma, aprobar: false, txtNotaAdmin.Text.Trim());
                if (!resultado.Exito)
                {
                    MessageBox.Show(resultado.Mensaje, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                CargarSolicitudesDesdeMysql();
                dlg.Close();
                MessageBox.Show(resultado.Mensaje, "Solicitud Rechazada", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                // 2. EXIGIR NIP/VALIDACIÓN DE ADMINISTRADOR PARA AUTORIZAR
                if (!SolicitarNipAdministrador(dlg, $"Autorizar merma #MRM-{sol.IdMerma:000}"))
                    return;

                var resultado = _mermaNegocio.ResolverSolicitud(sol.IdMerma, aprobar: true, string.IsNullOrWhiteSpace(txtNotaAdmin.Text) ? null : txtNotaAdmin.Text.Trim());
                if (!resultado.Exito)
                {
                    MessageBox.Show(resultado.Mensaje, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                CargarSolicitudesDesdeMysql();
                dlg.Close();
                MessageBox.Show(resultado.Mensaje, "Merma Autorizada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            dlg.Controls.AddRange(new Control[] { lblTit, lblSub, pnlDatos, lblNotaTit, txtNotaAdmin, btnRechazar, btnAutorizar });
            dlg.ShowDialog(this);
        }

        private void AjustarLayoutResponsivo()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int margenX = 32;
            int anchoUtil = Math.Max(780, ClientSize.Width - (margenX * 2));

            _cardContadorPendientes.Location = new Point(_pnlTopBar.Width - _cardContadorPendientes.Width - margenX, 20);

            int topToolbar = _pnlTopBar.Height + 18;
            _pnlToolbar.Bounds = new Rectangle(margenX, topToolbar, anchoUtil, 46);

            _btnFiltrar.Location = new Point(anchoUtil - _btnFiltrar.Width, 4);
            _cmbFiltroMotivo.Location = new Point(_btnFiltrar.Left - _cmbFiltroMotivo.Width - 12, 6);

            int yTabla = topToolbar + 54;
            int altoTablaCard = Math.Max(340, ClientSize.Height - yTabla - 22);
            _cardTabla.Bounds = new Rectangle(margenX, yTabla, anchoUtil, altoTablaCard);

            int altoPie = 38;
            _dgvSolicitudes.Bounds = new Rectangle(12, 12, anchoUtil - 24, altoTablaCard - altoPie - 16);
            _lblPieConteo.Location = new Point(16, altoTablaCard - 28);
        }
    }
}