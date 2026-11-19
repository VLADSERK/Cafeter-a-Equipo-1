using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CapaPresentacion.Estilos;

namespace CapaPresentaciòn.Formularios
{
    /// <summary>Un campo de texto del <see cref="DialogoCaptura"/>.</summary>
    public sealed record CampoCaptura(
        string Etiqueta,
        string ValorInicial = "",
        bool Secreto = false,
        int LongitudMaxima = 100,
        bool SoloDigitos = false);

    /// <summary>
    /// Ventana emergente con uno o varios campos de texto. Al presionar "Continuar" llama a
    /// <c>validar</c> (normalmente un método Validar* de la capa de lógica) y, si hay error,
    /// lo muestra dentro de la misma ventana sin cerrarla. Devuelve los valores capturados o null si se canceló.
    /// </summary>
    public static class DialogoCaptura
    {
        public static string[]? Solicitar(
            IWin32Window owner,
            string titulo,
            string detalle,
            IReadOnlyList<CampoCaptura> campos,
            Func<string[], (bool Exito, string Mensaje)> validar,
            string textoAceptar = "Continuar")
        {
            const int anchoInterno = 392;
            const int pasoCampo = 64;
            const int yPrimerCampo = 104;

            string[]? resultado = null;
            int yError = yPrimerCampo + campos.Count * pasoCampo;
            int yBotones = yError + 46;

            using Form dlg = new Form
            {
                Text = titulo,
                ClientSize = new Size(440, yBotones + 60),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = Color.White
            };

            Label lblTitulo = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                Location = new Point(24, 18),
                Size = new Size(anchoInterno, 28),
                AutoEllipsis = true
            };

            Label lblDetalle = new Label
            {
                Text = detalle,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TemaCafeteria.TextoSecundario,
                Location = new Point(24, 52),
                Size = new Size(anchoInterno, 44)
            };

            dlg.Controls.AddRange(new Control[] { lblTitulo, lblDetalle });

            var cajas = new List<TextBox>();
            for (int i = 0; i < campos.Count; i++)
            {
                var campo = campos[i];
                int y = yPrimerCampo + i * pasoCampo;

                Label lbl = new Label
                {
                    Text = campo.Etiqueta,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = TemaCafeteria.TextoOscuro,
                    Location = new Point(24, y),
                    AutoSize = true
                };

                TextBox txt = new TextBox
                {
                    Text = campo.ValorInicial,
                    Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                    Location = new Point(24, y + 20),
                    Width = anchoInterno,
                    MaxLength = campo.LongitudMaxima,
                    UseSystemPasswordChar = campo.Secreto
                };

                if (campo.SoloDigitos)
                {
                    txt.KeyPress += (s, e) =>
                    {
                        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                            e.Handled = true;
                    };
                }

                cajas.Add(txt);
                dlg.Controls.AddRange(new Control[] { lbl, txt });
            }

            Label lblError = new Label
            {
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.ErrorRojo,
                Location = new Point(24, yError),
                Size = new Size(anchoInterno, 40),
                Text = string.Empty
            };

            foreach (var txt in cajas)
                txt.TextChanged += (s, e) => lblError.Text = string.Empty;

            Button btnCancelar = new Button
            {
                Text = "Cancelar",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TemaCafeteria.TextoOscuro,
                BackColor = ColorTranslator.FromHtml("#EFECE8"),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(24, yBotones),
                Size = new Size(190, 42),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancelar.FlatAppearance.BorderSize = 0;

            Button btnAceptar = new Button
            {
                Text = textoAceptar,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TemaCafeteria.CafePrimario,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(226, yBotones),
                Size = new Size(190, 42),
                Cursor = Cursors.Hand
            };
            btnAceptar.FlatAppearance.BorderSize = 0;
            btnAceptar.Click += (s, e) =>
            {
                string[] valores = cajas.Select(c => c.Text).ToArray();

                (bool Exito, string Mensaje) validacion;
                try
                {
                    validacion = validar(valores);
                }
                catch (Exception ex)
                {
                    validacion = (false, ex.Message);
                }

                if (!validacion.Exito)
                {
                    lblError.Text = "⚠ " + validacion.Mensaje;
                    return;
                }

                resultado = valores;
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] { lblError, btnCancelar, btnAceptar });
            dlg.AcceptButton = btnAceptar;
            dlg.CancelButton = btnCancelar;
            dlg.Shown += (s, e) =>
            {
                if (cajas.Count > 0)
                {
                    cajas[0].Focus();
                    cajas[0].SelectAll();
                }
            };

            dlg.ShowDialog(owner);
            return resultado;
        }
    }
}
