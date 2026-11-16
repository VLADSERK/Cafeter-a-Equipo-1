using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public enum NivelSemaforo
    {
        Verde,      // Óptimo
        Amarillo,   // Precaución
        Rojo,       // Crítico
        Blanco      // Agotado
    }

    public class ResumenInsumoInventario
    {
        public Insumo Insumo { get; set; } = new();
        public decimal PorcentajeStock { get; set; }
        public NivelSemaforo Semaforo { get; set; }
        public string EtiquetaEstado { get; set; } = string.Empty;
        public string LotePepsActivo { get; set; } = "Sin lote activo";
        public DateTime? ProximaCaducidad { get; set; }
        public decimal ValorTotalInventario { get; set; }
        public List<Lote> LotesActivos { get; set; } = new();
    }

    public class InventarioNegocio
    {
        private readonly InventarioDatos _inventarioDatos = new();
        private readonly ConfiguracionDatos _configDatos = new();

        public List<UnidadMedida> ObtenerUnidades() => _inventarioDatos.ObtenerUnidadesMedida();
        public List<Proveedor> ObtenerProveedores() => _inventarioDatos.ObtenerProveedoresActivos();
        public List<Lote> ObtenerLotesPeps(bool soloDisponibles = true) => _inventarioDatos.ObtenerLotesPeps(soloDisponibles);

        /// <summary>
        /// Evalúa el nivel del semáforo usando los parámetros guardados en ConfiguracionGeneral de MySQL.
        /// </summary>
        public NivelSemaforo CalcularSemaforo(decimal stockActual, decimal stockMaximo, ConfiguracionGeneral cfg)
        {
            if (stockActual <= 0 || stockMaximo <= 0)
                return NivelSemaforo.Blanco;

            decimal porcentaje = (stockActual / stockMaximo) * 100m;

            if (porcentaje >= cfg.VerdeMinimo)
                return NivelSemaforo.Verde;

            if (porcentaje >= cfg.AmarilloMinimo)
                return NivelSemaforo.Amarillo;

            return NivelSemaforo.Rojo;
        }

        /// <summary>
        /// Devuelve el tablero completo de insumos con su semáforo, lote PEPS prioritario y valorización.
        /// </summary>
        public List<ResumenInsumoInventario> ObtenerResumenInventarioCompleto()
        {
            var config = _configDatos.ObtenerConfiguracion();
            var insumos = _inventarioDatos.ObtenerInsumosConStock();
            var lotes = _inventarioDatos.ObtenerLotesPeps(soloConDisponibilidad: true);

            var resultado = new List<ResumenInsumoInventario>();

            foreach (var ins in insumos)
            {
                var lotesInsumo = lotes.Where(l => l.IdInsumo == ins.IdInsumo).ToList();
                decimal stockReal = lotesInsumo.Sum(l => l.CantidadDisponible);
                ins.StockActualTotal = stockReal;

                decimal porcentaje = ins.StockMaximo > 0
                    ? Math.Round((stockReal / ins.StockMaximo) * 100m, 1)
                    : 0m;

                NivelSemaforo nivel = CalcularSemaforo(stockReal, ins.StockMaximo, config);
                string etiqueta = nivel switch
                {
                    NivelSemaforo.Verde => "Óptimo",
                    NivelSemaforo.Amarillo => "Precaución",
                    NivelSemaforo.Rojo => "Crítico",
                    _ => "Agotado"
                };

                Lote? primerLotePeps = lotesInsumo.FirstOrDefault();
                decimal valorTotal = lotesInsumo.Sum(l => l.CantidadDisponible * l.CostoUnitario);

                resultado.Add(new ResumenInsumoInventario
                {
                    Insumo = ins,
                    PorcentajeStock = porcentaje,
                    Semaforo = nivel,
                    EtiquetaEstado = etiqueta,
                    LotePepsActivo = primerLotePeps != null ? primerLotePeps.CodigoLote : "Sin existencias",
                    ProximaCaducidad = primerLotePeps?.FechaCaducidad,
                    ValorTotalInventario = valorTotal,
                    LotesActivos = lotesInsumo
                });
            }

            return resultado;
        }

        public (bool Exito, string Mensaje) RegistrarEntregaProveedor(
            int idProveedor,
            int idInsumo,
            decimal cantidadRecibida,
            decimal costoUnitario,
            DateTime? fechaCaducidad,
            string? codigoLoteManual,
            string? codigoBarrasManual,
            string? observaciones)
        {
            if (!SesionActual.HaySesionActiva)
                return (false, "No hay una sesión de usuario activa.");

            if (idProveedor <= 0)
                return (false, "Selecciona un proveedor válido.");

            if (idInsumo <= 0)
                return (false, "Selecciona un insumo válido.");

            if (cantidadRecibida <= 0)
                return (false, "La cantidad recibida debe ser mayor a 0.");

            if (costoUnitario < 0)
                return (false, "El costo unitario no puede ser negativo.");

            if (fechaCaducidad.HasValue && fechaCaducidad.Value.Date < DateTime.Today)
                return (false, "No se puede dar entrada a un lote con fecha de caducidad vencida.");

            DateTime ahora = DateTime.Now;
            string sufijoUnico = ahora.ToString("HHmmss");

            string codigoLote = string.IsNullOrWhiteSpace(codigoLoteManual)
                ? $"LOT-{ahora:yyyyMMdd}-{sufijoUnico}"
                : codigoLoteManual.Trim().ToUpperInvariant();

            string codigoBarras = string.IsNullOrWhiteSpace(codigoBarrasManual)
                ? $"750{ahora:yyMMddHHmmss}"
                : codigoBarrasManual.Trim();

            var recepcion = new Recepcion
            {
                IdProveedor = idProveedor,
                IdUsuario = SesionActual.IdUsuario,
                FechaRecepcion = ahora,
                Observaciones = observaciones?.Trim()
            };

            var lote = new Lote
            {
                IdInsumo = idInsumo,
                CodigoLote = codigoLote,
                CodigoBarras = codigoBarras,
                FechaRecepcion = ahora,
                FechaCaducidad = fechaCaducidad,
                CantidadInicial = cantidadRecibida,
                CantidadDisponible = cantidadRecibida,
                CostoUnitario = costoUnitario
            };

            try
            {
                int idLoteCreado = _inventarioDatos.RegistrarRecepcionConLote(recepcion, lote);
                return (true, $"Recepción registrada correctamente en MySQL.\nLote generado: {codigoLote} (ID #{idLoteCreado}).");
            }
            catch (Exception ex)
            {
                return (false, $"Error al registrar la recepción en MySQL:\n{ex.Message}");
            }
        }

        public (bool Exito, string Mensaje) CrearNuevoInsumo(
     string nombre,
     int idUnidad,
     decimal stockMaximo,
     string categoria = "General",
     decimal stockMinimo = 1.000m)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return (false, "El nombre del insumo es obligatorio.");

            if (idUnidad <= 0)
                return (false, "Selecciona una unidad de medida.");

            if (stockMaximo <= 0)
                return (false, "El stock máximo debe ser mayor a 0.");

            try
            {
                _inventarioDatos.InsertarInsumo(new Insumo
                {
                    Nombre = nombre.Trim(),
                    Categoria = string.IsNullOrWhiteSpace(categoria) ? "General" : categoria.Trim(),
                    IdUnidad = idUnidad,
                    StockMinimo = stockMinimo > 0 ? stockMinimo : 1.000m,
                    StockMaximo = stockMaximo
                });
                return (true, $"Insumo '{nombre.Trim()}' registrado en el catálogo.");
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo registrar el insumo:\n{ex.Message}");
            }
        }

        public (bool Exito, string Mensaje) CrearNuevoProveedor(string nombre, string? telefono, string? correo, string? direccion)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return (false, "El nombre del proveedor es obligatorio.");

            try
            {
                _inventarioDatos.InsertarProveedor(new Proveedor
                {
                    Nombre = nombre.Trim(),
                    Telefono = telefono?.Trim(),
                    Correo = correo?.Trim(),
                    Direccion = direccion?.Trim()
                });
                return (true, $"Proveedor '{nombre.Trim()}' registrado correctamente.");
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo registrar el proveedor:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Carga proveedores, insumos y lotes iniciales en MySQL solo si la tabla Insumo está vacía.
        /// </summary>
        public void AsegurarDatosInicialesInventario()
        {
            if (_inventarioDatos.ObtenerInsumosConStock().Count > 0)
                return;

            // Unidades según script semilla: 1=g, 2=kg, 3=ml, 4=L, 5=pz
            int idProvCafe = _inventarioDatos.InsertarProveedor(new Proveedor
            {
                Nombre = "Tostadores del Sur S.A.",
                Telefono = "55-4812-9034",
                Correo = "ventas@tostadoresdelsur.mx",
                Direccion = "Col. Roma Norte, CDMX"
            });

            int idProvLacteos = _inventarioDatos.InsertarProveedor(new Proveedor
            {
                Nombre = "Lácteos y Derivados Alpura Distribución",
                Telefono = "55-9011-2240",
                Correo = "pedidos@lacteosdist.mx",
                Direccion = "Tlalnepantla, Edo. Méx."
            });

            int idCafe = _inventarioDatos.InsertarInsumo(new Insumo { IdUnidad = 2, Nombre = "Café en Grano Espresso", StockMaximo = 20.000m });
            int idLecheEntera = _inventarioDatos.InsertarInsumo(new Insumo { IdUnidad = 4, Nombre = "Leche Entera", StockMaximo = 60.000m });
            int idLecheDeslact = _inventarioDatos.InsertarInsumo(new Insumo { IdUnidad = 4, Nombre = "Leche Deslactosada", StockMaximo = 40.000m });
            int idJarabeVainilla = _inventarioDatos.InsertarInsumo(new Insumo { IdUnidad = 3, Nombre = "Jarabe de Vainilla", StockMaximo = 5000.000m });
            int idVasos12 = _inventarioDatos.InsertarInsumo(new Insumo { IdUnidad = 5, Nombre = "Vaso Térmico 12 oz", StockMaximo = 500.000m });

            // Lotes PEPS iniciales (uno próximo a caducar y otro más reciente)
            RegistrarEntregaProveedor(idProvCafe, idCafe, 8.000m, 280.00m, DateTime.Today.AddDays(45), "LOT-CAF-001", "750100100001", "Inventario inicial");
            RegistrarEntregaProveedor(idProvCafe, idCafe, 6.500m, 290.00m, DateTime.Today.AddDays(90), "LOT-CAF-002", "750100100002", "Reposición semanal");
            RegistrarEntregaProveedor(idProvLacteos, idLecheEntera, 18.000m, 24.50m, DateTime.Today.AddDays(6), "LOT-LEC-014", "750100200014", "Lote refrigerado prioritario");
            RegistrarEntregaProveedor(idProvLacteos, idLecheDeslact, 6.000m, 26.00m, DateTime.Today.AddDays(4), "LOT-DES-009", "750100200009", "Stock en nivel rojo");
            RegistrarEntregaProveedor(idProvCafe, idJarabeVainilla, 2500.000m, 0.14m, DateTime.Today.AddDays(120), "LOT-JAR-003", "750100300003", "Botellas 750ml");
            RegistrarEntregaProveedor(idProvCafe, idVasos12, 320.000m, 1.85m, null, "LOT-VAS-010", "750100400010", "Empaque sin caducidad");
        }
        public string GenerarVistaPreviaCodigoLote(Insumo? insumo, DateTime fechaRecepcion)
        {
            if (insumo == null || insumo.IdInsumo <= 0)
                return $"LOT-INS-{fechaRecepcion:yyyyMMdd}-001";

            string prefijo = new string(insumo.Nombre
                .ToUpperInvariant()
                .Where(char.IsLetter)
                .Take(3)
                .ToArray())
                .PadRight(3, 'X');

            int consecutivo = _inventarioDatos.ContarLotesHoyPorInsumo(insumo.IdInsumo) + 1;
            return $"LOT-{prefijo}-{fechaRecepcion:yyyyMMdd}-{consecutivo:000}";
        }
    }
}