using CapaEntidades;

namespace CapaLogica
{
    /// <summary>Estados del semáforo de existencias (RF-08).</summary>
    public enum NivelSemaforo
    {
        Verde,      // ≥ VerdeMinimo (50 %)
        Amarillo,   // AmarilloMinimo (21 %) – VerdeMinimo
        Rojo,       // ≤ RojoMaximo (20 %)
        Blanco      // Sin existencias (0)
    }

    /// <summary>Resultado de clasificar la existencia de un insumo.</summary>
    public sealed class EvaluacionSemaforo
    {
        public NivelSemaforo Nivel { get; init; }

        /// <summary>Existencia / stock máximo × 100, redondeado Half-Up a 1 decimal.</summary>
        public decimal Porcentaje { get; init; }

        /// <summary>Etiqueta textual del estado: Verde, Amarillo, Rojo o Blanco (RIU-03).</summary>
        public string Etiqueta => Nivel.ToString();

        /// <summary>Significado del estado (Abasto óptimo, Abasto bajo, ...).</summary>
        public string Descripcion => SemaforoExistencias.Descripcion(Nivel);

        /// <summary>Aviso de reabastecimiento según la urgencia; null cuando el insumo está en Verde.</summary>
        public string? AvisoReabastecimiento => SemaforoExistencias.AvisoReabastecimiento(Nivel);

        public bool RequiereReabastecimiento => Nivel != NivelSemaforo.Verde;
    }

    /// <summary>
    /// Única regla del semáforo de existencias (RF-08, RIU-03, Diseño 4.1).
    /// La usan el inventario del cajero, el del Administrador, el registro de mermas,
    /// el dashboard y el reporte 5.3, para que todos muestren el mismo estado.
    /// </summary>
    public static class SemaforoExistencias
    {
        /// <summary>
        /// Clasifica la existencia de un insumo con los umbrales de ConfiguracionGeneral.
        /// El estado se decide con el mismo porcentaje (1 decimal) que se muestra en pantalla,
        /// para que el número y el color nunca se contradigan.
        /// </summary>
        public static EvaluacionSemaforo Evaluar(decimal disponible, decimal stockMaximo, ConfiguracionGeneral cfg)
        {
            if (disponible <= 0)
                return new EvaluacionSemaforo { Nivel = NivelSemaforo.Blanco, Porcentaje = 0m };

            // Sin stock máximo no hay referencia para el porcentaje; si hay existencia no se marca como faltante.
            if (stockMaximo <= 0)
                return new EvaluacionSemaforo { Nivel = NivelSemaforo.Verde, Porcentaje = 0m };

            decimal porcentaje = Redondeo.A(disponible / stockMaximo * 100m, 1);

            NivelSemaforo nivel = porcentaje >= cfg.VerdeMinimo ? NivelSemaforo.Verde
                : porcentaje >= cfg.AmarilloMinimo ? NivelSemaforo.Amarillo
                : NivelSemaforo.Rojo;

            return new EvaluacionSemaforo { Nivel = nivel, Porcentaje = porcentaje };
        }

        public static string Descripcion(NivelSemaforo nivel) => nivel switch
        {
            NivelSemaforo.Verde => "Abasto óptimo",
            NivelSemaforo.Amarillo => "Abasto bajo",
            NivelSemaforo.Rojo => "Nivel crítico",
            _ => "Sin existencias"
        };

        /// <summary>Etiqueta de reabastecimiento con la urgencia según el nivel de stock (RIU-03).</summary>
        public static string? AvisoReabastecimiento(NivelSemaforo nivel) => nivel switch
        {
            NivelSemaforo.Amarillo => "Reabastecer pronto",
            NivelSemaforo.Rojo => "Reabastecer urgente",
            NivelSemaforo.Blanco => "Reabastecer de inmediato",
            _ => null
        };

        /// <summary>Orden de urgencia para listar primero lo que más falta: Blanco, Rojo, Amarillo, Verde.</summary>
        public static int PrioridadUrgencia(NivelSemaforo nivel) => nivel switch
        {
            NivelSemaforo.Blanco => 0,
            NivelSemaforo.Rojo => 1,
            NivelSemaforo.Amarillo => 2,
            _ => 3
        };
    }
}
