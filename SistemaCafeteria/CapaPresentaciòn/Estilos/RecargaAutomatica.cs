using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace CapaPresentacion.Estilos
{
    /// <summary>
    /// Carga una pantalla desde MySQL cuando se muestra por primera vez (evento Load) y la vuelve
    /// a cargar cada vez que se oculta y se muestra de nuevo.
    ///
    /// No basta con VisibleChanged: una pantalla nueva ya tiene Visible = true antes de incrustarse,
    /// así que al agregarla al panel de la ventana su visibilidad no "cambia" y el evento no se dispara.
    /// Load sí se dispara siempre que la pantalla se crea dentro de una ventana visible.
    /// </summary>
    public static class RecargaAutomatica
    {
        public static void AlMostrar(UserControl pantalla, Action cargar)
        {
            bool cargada = false;

            bool EnDisenador() => LicenseManager.UsageMode == LicenseUsageMode.Designtime || pantalla.Site?.DesignMode == true;

            pantalla.Load += (s, e) =>
            {
                if (EnDisenador()) return;
                cargada = true;
                cargar();
            };

            pantalla.VisibleChanged += (s, e) =>
            {
                // Solo recargas posteriores; la primera carga la hace Load
                if (!cargada || !pantalla.Visible || EnDisenador()) return;
                cargar();
            };
        }
    }
}
