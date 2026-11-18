using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaLogica
{
    /// <summary>Operaciones del sistema sujetas a control de privilegios (RNF-10).</summary>
    public enum Operacion
    {
        RegistrarVenta,              // RIU-01 · Punto de venta
        RegistrarRecepcion,          // RF-01
        ConsultarInventario,         // RF-03 · existencias y kardex
        RegistrarMerma,              // RF-09
        AutorizarMerma,              // RF-09 · bandeja de autorizaciones
        ConsultarVentas,             // Historial de ventas
        CancelarVenta,               // RF-07
        AdministrarCatalogo,         // RF-04 / RF-05 · productos y recetas
        AdministrarInsumosProveedores, // RF-04 · altas de insumos y proveedores
        GenerarReportes,             // RF-10 / RIU-06 · reportes y dashboard
        ConfigurarParametros         // RF-12
    }

    /// <summary>
    /// Matriz única de privilegios por rol, según los roles del Preanálisis v1.2.
    /// Las clases de negocio la consultan antes de ejecutar cada operación, de modo que
    /// una operación no autorizada se rechaza sin tocar la base aunque la pantalla la permitiera.
    /// Barista y Cajero comparten privilegios ("Barista/Cajero" en los requisitos).
    /// </summary>
    public static class Permisos
    {
        public const string RolAdministrador = "Administrador";
        public const string RolCajero = "Cajero";
        public const string RolBarista = "Barista";

        private static readonly string[] Todos = { RolAdministrador, RolCajero, RolBarista };
        private static readonly string[] SoloAdmin = { RolAdministrador };

        private static readonly Dictionary<Operacion, (string[] Roles, string Descripcion)> Matriz = new()
        {
            [Operacion.RegistrarVenta] = (Todos, "registrar ventas"),
            [Operacion.RegistrarRecepcion] = (Todos, "registrar recepciones de mercancía"),
            [Operacion.ConsultarInventario] = (Todos, "consultar el inventario"),
            [Operacion.RegistrarMerma] = (Todos, "registrar mermas"),
            [Operacion.AutorizarMerma] = (SoloAdmin, "autorizar o rechazar mermas"),
            [Operacion.ConsultarVentas] = (SoloAdmin, "consultar el historial de ventas"),
            [Operacion.CancelarVenta] = (SoloAdmin, "cancelar ventas"),
            [Operacion.AdministrarCatalogo] = (SoloAdmin, "modificar productos y recetas"),
            [Operacion.AdministrarInsumosProveedores] = (SoloAdmin, "dar de alta insumos o proveedores"),
            [Operacion.GenerarReportes] = (SoloAdmin, "consultar reportes financieros"),
            [Operacion.ConfigurarParametros] = (SoloAdmin, "modificar los parámetros globales")
        };

        public static bool EsAdministrador => Coincide(SesionActual.Rol, RolAdministrador);

        /// <summary>Cajero o Barista: entran a la ventana operativa.</summary>
        public static bool EsOperativo(string? rol) => Coincide(rol, RolCajero) || Coincide(rol, RolBarista);

        /// <summary>Roles que pueden ejecutar la operación (para documentación y pruebas).</summary>
        public static IReadOnlyList<string> RolesPermitidos(Operacion op) => Matriz[op].Roles;

        public static bool Puede(Operacion op)
            => SesionActual.HaySesionActiva && Matriz[op].Roles.Any(r => Coincide(SesionActual.Rol, r));

        /// <summary>Verifica la sesión y el rol; devuelve el mensaje de rechazo si no procede.</summary>
        public static (bool Exito, string Mensaje) Verificar(Operacion op)
        {
            if (!SesionActual.HaySesionActiva)
                return (false, "No hay una sesión activa. Inicia sesión para continuar.");

            if (!Puede(op))
            {
                string rol = string.IsNullOrWhiteSpace(SesionActual.Rol) ? "sin rol" : SesionActual.Rol;
                string requeridos = string.Join(" o ", Matriz[op].Roles.Distinct());
                return (false, $"Acceso denegado: tu rol ({rol}) no tiene permiso para {Matriz[op].Descripcion}. Se requiere {requeridos}.");
            }

            return (true, string.Empty);
        }

        private static bool Coincide(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
    }
}
