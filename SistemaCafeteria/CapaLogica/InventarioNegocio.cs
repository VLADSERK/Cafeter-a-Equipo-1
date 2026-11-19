using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CapaDatos;
using CapaEntidades;

namespace CapaLogica
{
    public class ResumenInsumoInventario
    {
        public Insumo Insumo { get; set; } = new();
        public decimal PorcentajeStock { get; set; }
        public NivelSemaforo Semaforo { get; set; }

        /// <summary>Verde | Amarillo | Rojo | Blanco (RIU-03).</summary>
        public string EtiquetaEstado { get; set; } = string.Empty;

        /// <summary>Abasto óptimo | Abasto bajo | Nivel crítico | Sin existencias.</summary>
        public string DescripcionEstado { get; set; } = string.Empty;

        /// <summary>Aviso de reabastecimiento con su urgencia; null en Verde.</summary>
        public string? AvisoReabastecimiento { get; set; }

        public string LotePepsActivo { get; set; } = "Sin lote activo";
        public DateTime? ProximaCaducidad { get; set; }
        public decimal ValorTotalInventario { get; set; }

        /// <summary>Lotes del insumo en orden PEPS (fecha y hora de recepción más antigua primero).</summary>
        public List<Lote> LotesActivos { get; set; } = new();
    }

    public class InventarioNegocio
    {
        private readonly InventarioDatos _inventarioDatos = new();
        private readonly ConfiguracionDatos _configDatos = new();

        public List<UnidadMedida> ObtenerUnidades() => _inventarioDatos.ObtenerUnidadesMedida();
        public List<Proveedor> ObtenerProveedores() => _inventarioDatos.ObtenerProveedoresActivos();
        public List<Lote> ObtenerLotesPeps(bool soloDisponibles = true)
        {
            ExigirPermiso(Operacion.ConsultarInventario);
            return _inventarioDatos.ObtenerLotesPeps(soloDisponibles);
        }

        private static void ExigirPermiso(Operacion op)
        {
            var permiso = Permisos.Verificar(op);
            if (!permiso.Exito)
                throw new UnauthorizedAccessException(permiso.Mensaje);
        }

        /// <summary>
        /// Insumos activos para capturar una recepción, en orden alfabético por nombre
        /// (sin acentos ni mayúsculas de por medio), para que el orden no cambie con las existencias.
        /// </summary>
        public List<Insumo> ObtenerInsumosParaRecepcion()
        {
            ExigirPermiso(Operacion.RegistrarRecepcion);

            var comparador = StringComparer.Create(CultureInfo.GetCultureInfo("es-MX"), ignoreCase: true);
            return _inventarioDatos.ObtenerInsumosConStock()
                .OrderBy(i => i.Nombre, comparador)
                .ToList();
        }

        /// <summary>
        /// Evalúa el nivel del semáforo usando los parámetros guardados en ConfiguracionGeneral de MySQL.
        /// </summary>
        public NivelSemaforo CalcularSemaforo(decimal stockActual, decimal stockMaximo, ConfiguracionGeneral cfg)
            => SemaforoExistencias.Evaluar(stockActual, stockMaximo, cfg).Nivel;

        /// <summary>
        /// Devuelve el tablero completo de insumos con su semáforo (RF-08), porcentaje de abasto,
        /// aviso de reabastecimiento (RIU-03) y sus lotes en orden PEPS (RF-02, RIU-02).
        /// Los insumos vienen ordenados por urgencia (Blanco, Rojo, Amarillo, Verde) y luego por nombre.
        /// </summary>
        /// <param name="incluirLotesAgotados">
        /// true para incluir también los lotes con existencia 0 (para consultar su kardex).
        /// El semáforo y la valorización no cambian, porque esos lotes suman 0.
        /// </param>
        public List<ResumenInsumoInventario> ObtenerResumenInventarioCompleto(bool incluirLotesAgotados = false)
        {
            ExigirPermiso(Operacion.ConsultarInventario);

            var config = _configDatos.ObtenerConfiguracion();
            var insumos = _inventarioDatos.ObtenerInsumosConStock();
            var lotesPorInsumo = _inventarioDatos.ObtenerLotesPeps(soloConDisponibilidad: !incluirLotesAgotados)
                .GroupBy(l => l.IdInsumo)
                .ToDictionary(g => g.Key, g => g.ToList());   // GroupBy conserva el orden PEPS de MySQL

            var resultado = new List<ResumenInsumoInventario>();

            foreach (var ins in insumos)
            {
                var lotesInsumo = lotesPorInsumo.TryGetValue(ins.IdInsumo, out var l) ? l : new List<Lote>();
                var lotesConExistencia = lotesInsumo.Where(x => x.CantidadDisponible > 0).ToList();

                decimal stockReal = lotesConExistencia.Sum(x => x.CantidadDisponible);
                ins.StockActualTotal = stockReal;

                var evaluacion = SemaforoExistencias.Evaluar(stockReal, ins.StockMaximo, config);

                // Primer lote con existencia en orden PEPS = el siguiente que se consumirá
                Lote? primerLotePeps = lotesConExistencia.FirstOrDefault();
                decimal valorTotal = Redondeo.Importe(lotesConExistencia.Sum(x => x.CantidadDisponible * x.CostoUnitario));

                // El lote PEPS no es necesariamente el que caduca primero: se busca la caducidad más cercana
                DateTime? proximaCaducidad = lotesConExistencia
                    .Where(x => x.FechaCaducidad.HasValue)
                    .Select(x => x.FechaCaducidad)
                    .Min();

                resultado.Add(new ResumenInsumoInventario
                {
                    Insumo = ins,
                    PorcentajeStock = evaluacion.Porcentaje,
                    Semaforo = evaluacion.Nivel,
                    EtiquetaEstado = evaluacion.Etiqueta,
                    DescripcionEstado = evaluacion.Descripcion,
                    AvisoReabastecimiento = evaluacion.AvisoReabastecimiento,
                    LotePepsActivo = primerLotePeps != null ? primerLotePeps.CodigoLote : "Sin existencias",
                    ProximaCaducidad = proximaCaducidad,
                    ValorTotalInventario = valorTotal,
                    LotesActivos = lotesInsumo
                });
            }

            return resultado
                .OrderBy(r => SemaforoExistencias.PrioridadUrgencia(r.Semaforo))
                .ThenBy(r => r.Insumo.Nombre)
                .ToList();
        }

        /// <summary>
        /// Kardex del lote (RF-03): movimientos en orden cronológico con tipo, cantidad con signo,
        /// saldo acumulado y referencia del documento de origen.
        /// </summary>
        public (bool Exito, string Mensaje, List<MovimientoKardex> Movimientos) ObtenerKardexLote(int idLote)
        {
            var permiso = Permisos.Verificar(Operacion.ConsultarInventario);
            if (!permiso.Exito)
                return (false, permiso.Mensaje, new());

            if (idLote <= 0)
                return (false, "Selecciona un lote válido.", new());

            try
            {
                var movimientos = _inventarioDatos.ObtenerKardexLote(idLote);
                decimal saldo = 0m;

                foreach (var m in movimientos)
                {
                    m.EsEntrada = m.Tipo is "ENTREGA" or "REVERSION";
                    saldo = Redondeo.Cantidad(saldo + (m.EsEntrada ? m.Cantidad : -m.Cantidad));
                    m.Saldo = saldo;

                    (m.TipoDescripcion, m.Referencia) = m.Tipo switch
                    {
                        "ENTREGA" => ("Entrada por recepción",
                            $"Recepción R-{m.IdRecepcion:000}{(string.IsNullOrWhiteSpace(m.Proveedor) ? "" : $" · {m.Proveedor}")}"),
                        "CONSUMO_VENTA" => ("Salida por venta",
                            $"Venta {m.FolioVenta ?? "—"}{(string.IsNullOrWhiteSpace(m.Producto) ? "" : $" · {m.Producto}")}"),
                        "REVERSION" => ("Reintegro por cancelación",
                            $"Cancelación de venta {m.FolioVenta ?? "—"}{(string.IsNullOrWhiteSpace(m.Producto) ? "" : $" · {m.Producto}")}"),
                        "MERMA" => ("Salida por merma",
                            $"Merma MRM-{m.IdMerma:000}{(string.IsNullOrWhiteSpace(m.MotivoMerma) ? "" : $" · {m.MotivoMerma}")}"),
                        _ => (m.Tipo, m.Observaciones ?? "—")
                    };
                }

                return (true, string.Empty, movimientos);
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo consultar el historial del lote en MySQL:\n{ex.Message}", new());
            }
        }

        /// <summary>Días hacia atrás que se aceptan al capturar una recepción con retraso.</summary>
        public const int DiasMaximosRecepcionAtrasada = 30;

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
            var r = RegistrarRecepcion(idProveedor, idInsumo, cantidadRecibida, costoUnitario, null,
                fechaCaducidad, codigoLoteManual, codigoBarrasManual, observaciones);
            return (r.Exito, r.Mensaje);
        }

        /// <summary>
        /// Recepción de mercancía (RF-01): crea la recepción, el lote y su movimiento ENTREGA en una transacción.
        /// - Fecha de recepción: la capturada (con la hora actual) o ahora; no futura ni de más de 30 días atrás.
        /// - Caducidad: opcional ("cuando corresponda"); si se captura debe ser posterior a la recepción.
        /// - Código de lote: el capturado o uno generado sin repetir (LOT-XXX-aaaammdd-nnn).
        /// - Código de barras: el capturado (validado como único) o uno interno único generado al confirmar.
        /// </summary>
        public (bool Exito, string Mensaje, string CodigoLote, string CodigoBarras) RegistrarRecepcion(
            int idProveedor,
            int idInsumo,
            decimal cantidadRecibida,
            decimal costoUnitario,
            DateTime? fechaRecepcion,
            DateTime? fechaCaducidad,
            string? codigoLoteManual,
            string? codigoBarrasManual,
            string? observaciones)
        {
            var permiso = Permisos.Verificar(Operacion.RegistrarRecepcion);
            if (!permiso.Exito)
                return (false, permiso.Mensaje, "", "");

            var v = ValidarRecepcion(idProveedor, idInsumo, cantidadRecibida, costoUnitario, fechaRecepcion, fechaCaducidad,
                codigoBarrasManual, out DateTime fechaReal);
            if (!v.Exito)
                return (false, v.Mensaje, "", "");

            try
            {
                string codigoLote;
                if (string.IsNullOrWhiteSpace(codigoLoteManual))
                {
                    var insumo = _inventarioDatos.ObtenerInsumosConStock().FirstOrDefault(i => i.IdInsumo == idInsumo);
                    if (insumo == null)
                        return (false, "El insumo seleccionado no existe o está inactivo.", "", "");
                    codigoLote = GenerarVistaPreviaCodigoLote(insumo, fechaReal);
                }
                else
                {
                    codigoLote = codigoLoteManual.Trim().ToUpperInvariant();
                    if (_inventarioDatos.ExisteCodigoLote(codigoLote))
                        return (false, $"El código de lote {codigoLote} ya existe. Captura otro o déjalo vacío para generarlo.", "", "");
                }

                string? codigoBarras = string.IsNullOrWhiteSpace(codigoBarrasManual) ? null : codigoBarrasManual.Trim();
                if (codigoBarras != null && _inventarioDatos.ExisteCodigoBarras(codigoBarras))
                    return (false, $"El código de barras {codigoBarras} ya está asignado a otro lote.", "", "");

                var recepcion = new Recepcion
                {
                    IdProveedor = idProveedor,
                    IdUsuario = SesionActual.IdUsuario,
                    FechaRecepcion = fechaReal,
                    Observaciones = observaciones?.Trim()
                };

                var lote = new Lote
                {
                    IdInsumo = idInsumo,
                    CodigoLote = codigoLote,
                    CodigoBarras = codigoBarras ?? string.Empty,   // vacío = la capa de datos genera uno único
                    FechaRecepcion = fechaReal,
                    FechaCaducidad = fechaCaducidad?.Date,
                    CantidadInicial = cantidadRecibida,
                    CantidadDisponible = cantidadRecibida,
                    CostoUnitario = costoUnitario
                };

                var (idLoteCreado, barrasGuardado) = _inventarioDatos.RegistrarRecepcionConLote(recepcion, lote);
                return (true,
                    $"Recepción registrada correctamente en MySQL.\nLote generado: {codigoLote} (ID #{idLoteCreado}).\n" +
                    $"Código de barras: {barrasGuardado}{(codigoBarras == null ? " (generado automáticamente)" : "")}.",
                    codigoLote, barrasGuardado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al registrar la recepción en MySQL:\n{ex.Message}", "", "");
            }
        }

        private static (bool Exito, string Mensaje) ValidarRecepcion(
            int idProveedor, int idInsumo, decimal cantidadRecibida, decimal costoUnitario,
            DateTime? fechaRecepcion, DateTime? fechaCaducidad, string? codigoBarrasManual, out DateTime fechaReal)
        {
            DateTime ahora = DateTime.Now;
            fechaReal = ahora;

            if (idProveedor <= 0)
                return (false, "Selecciona un proveedor válido.");

            if (idInsumo <= 0)
                return (false, "Selecciona un insumo válido.");

            if (cantidadRecibida <= 0)
                return (false, "La cantidad recibida debe ser mayor a 0.");

            if (Redondeo.ExcedeDecimales(cantidadRecibida, Redondeo.DecimalesCantidad))
                return (false, "La cantidad recibida admite como máximo 3 decimales.");

            if (costoUnitario <= 0)
                return (false, "El costo unitario debe ser mayor a cero.");

            if (Redondeo.ExcedeDecimales(costoUnitario, Redondeo.DecimalesImporte))
                return (false, "El costo unitario admite como máximo 2 decimales (centavos).");

            if (fechaRecepcion.HasValue)
            {
                DateTime dia = fechaRecepcion.Value.Date;
                if (dia > DateTime.Today)
                    return (false, "La fecha de recepción no puede ser futura.");
                if (dia < DateTime.Today.AddDays(-DiasMaximosRecepcionAtrasada))
                    return (false, $"La fecha de recepción no puede tener más de {DiasMaximosRecepcionAtrasada} días de antigüedad.");

                // Se conserva la hora de captura para ordenar PEPS por fecha y hora (RF-02)
                fechaReal = dia == DateTime.Today ? ahora : dia.Add(ahora.TimeOfDay);
            }

            if (fechaCaducidad.HasValue && fechaCaducidad.Value.Date < DateTime.Today)
                return (false, "No se puede dar entrada a un lote con fecha de caducidad vencida.");

            if (fechaCaducidad.HasValue && fechaCaducidad.Value.Date <= fechaReal.Date)
                return (false, "La fecha de caducidad debe ser posterior a la fecha de recepción.");

            if (!string.IsNullOrWhiteSpace(codigoBarrasManual))
            {
                string cb = codigoBarrasManual.Trim();
                if (cb.Length > 100 || !cb.All(char.IsLetterOrDigit))
                    return (false, "El código de barras solo admite letras y números (máximo 100).");
            }

            return (true, string.Empty);
        }

        public (bool Exito, string Mensaje) CrearNuevoInsumo(
     string nombre,
     int idUnidad,
     decimal stockMaximo,
     string categoria = "General",
     decimal stockMinimo = 1.000m)
        {
            var permiso = Permisos.Verificar(Operacion.AdministrarInsumosProveedores);
            if (!permiso.Exito)
                return permiso;

            if (string.IsNullOrWhiteSpace(nombre))
                return (false, "El nombre del insumo es obligatorio.");

            if (idUnidad <= 0)
                return (false, "Selecciona una unidad de medida.");

            if (stockMaximo <= 0)
                return (false, "El stock máximo debe ser mayor a 0.");

            if (Redondeo.ExcedeDecimales(stockMaximo, Redondeo.DecimalesCantidad) ||
                Redondeo.ExcedeDecimales(stockMinimo, Redondeo.DecimalesCantidad))
                return (false, "El stock máximo y mínimo admiten como máximo 3 decimales.");

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
            var permiso = Permisos.Verificar(Operacion.AdministrarInsumosProveedores);
            if (!permiso.Exito)
                return permiso;

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
        /// <summary>
        /// Código de lote LOT-XXX-aaaammdd-nnn. El consecutivo se calcula sobre el prefijo completo
        /// (no por insumo) y se verifica que no exista, porque dos insumos pueden compartir las
        /// mismas tres letras (Leche Entera / Leche Deslactosada → LEC) y codigo_lote es único.
        /// </summary>
        public string GenerarVistaPreviaCodigoLote(Insumo? insumo, DateTime fechaRecepcion)
        {
            if (insumo == null || insumo.IdInsumo <= 0)
                return $"LOT-INS-{fechaRecepcion:yyyyMMdd}-001";

            string sinAcentos = new string(insumo.Nombre
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());

            string prefijo = new string(sinAcentos
                .ToUpperInvariant()
                .Where(c => c is >= 'A' and <= 'Z')
                .Take(3)
                .ToArray())
                .PadRight(3, 'X');

            string baseCodigo = $"LOT-{prefijo}-{fechaRecepcion:yyyyMMdd}-";
            int consecutivo = _inventarioDatos.ContarLotesConPrefijo(baseCodigo) + 1;
            string codigo = $"{baseCodigo}{consecutivo:000}";
            while (_inventarioDatos.ExisteCodigoLote(codigo))
                codigo = $"{baseCodigo}{++consecutivo:000}";

            return codigo;
        }
    }
}