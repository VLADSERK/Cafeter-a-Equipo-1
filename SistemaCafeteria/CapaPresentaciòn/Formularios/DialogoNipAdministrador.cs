using System;
using System.Drawing;
using System.Windows.Forms;
using CapaLogica;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Formularios
{
    /// <summary>
    /// Ventana emergente que pide el NIP de un Administrador (4 dígitos).
    /// Valida el NIP en el momento para dar retroalimentación inmediata y devuelve
    /// el NIP capturado, o null si se cerró sin autorizar. La capa de lógica lo vuelve a validar.
    /// </summary>
    public static class DialogoNipAdministrador
    {
        public static string? Solicitar(IWin32Window owner, string titulo, string detalle, string textoCancelar = "Cancelar")
        {
            string? nipValido = null;

            using Form dlg = new Form
            {
                Text = "Autorización de Administrador",
                ClientSize = new Size(440, 300),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = Color.White
            };

            Label lblTitulo = new Label
            {
                Text = $"🔒  {titulo}",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                Size = new Size(392, 28)
            };

            Label lblDetalle = new Label
            {
                Text = detalle,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 52),
                Size = new Size(392, 84)
            };

            Label lblNip = new Label
            {
                Text = "NIP de Administrador (4 dígitos)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 142),
                AutoSize = true
            };

            TextBox txtNip = new TextBox
            {
                Location = new Point(130, 164),
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
                Text = textoCancelar,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = ColorTranslator.FromHtml("#EFECE8"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, 234),
                Size = new Size(190, 42),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancelar.FlatAppearance.BorderSize = 0;

            Button btnConfirmar = new Button
            {
                Text = "Autorizar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(226, 234),
                Size = new Size(190, 42),
                Cursor = Cursors.Hand
            };
            btnConfirmar.FlatAppearance.BorderSize = 0;
            btnConfirmar.Click += (s, e) =>
            {
                string nip = txtNip.Text.Trim();
                if (nip.Length != 4)
                {
                    MessageBox.Show(dlg, "El NIP de Administrador consta de 4 dígitos.", "NIP incompleto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNip.Focus();
                    return;
                }

                try
                {
                    var validacion = new AuthNegocio().ValidarNipAdministrador(nip);
                    if (!validacion.Autorizado)
                    {
                        MessageBox.Show(dlg, "NIP incorrecto o sin privilegios de Administrador.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        txtNip.Clear();
                        txtNip.Focus();
                        return;
                    }

                    nipValido = nip;
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(dlg, $"No se pudo validar el NIP en MySQL:\n{ex.Message}", "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            dlg.AcceptButton = btnConfirmar;
            dlg.CancelButton = btnCancelar;
            dlg.Shown += (s, e) => txtNip.Focus();
            dlg.Controls.AddRange(new Control[] { lblTitulo, lblDetalle, lblNip, txtNip, btnCancelar, btnConfirmar });
            dlg.ShowDialog(owner);

            return nipValido;
        }
    }
}
