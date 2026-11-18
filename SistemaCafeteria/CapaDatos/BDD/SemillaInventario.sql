-- ============================================================================
-- SEMILLA DE INVENTARIO: usuarios iniciales, recepciones y lotes PEPS
-- Versión 1.0 · 17/11/2026 · RI-06 (scripts SQL versionados)
--
-- Orden de ejecución en una base nueva:
--   1. IS2.sql               (esquema + catálogos base)
--   2. SemillaCatalogo.sql   (20 productos con receta v1)
--   3. SemillaInventario.sql (este script)
-- En una base existente basta con ejecutar este script (y Migracion_01 si
-- la base todavía tiene la categoría "Alimentos").
--
-- Se puede ejecutar varias veces sin duplicar: cada recepción, lote y
-- movimiento se inserta solo si todavía no existe.
--
-- Las fechas son relativas al día de ejecución, para que ningún lote esté
-- caducado y el tablero muestre los cuatro estados del semáforo (RF-08):
--   Verde >= 50 %, Amarillo 21-49 %, Rojo <= 20 %, Blanco = 0 % (sin lotes).
--
-- Demostración de PEPS (RF-02): "Café en Grano Espresso" tiene dos lotes.
--   SEM-CAF-001 se recibió hace 20 días y caduca en ~160 días.
--   SEM-CAF-002 se recibió hace 3 días y caduca en ~87 días.
--   Las ventas deben consumir primero SEM-CAF-001 (recepción más antigua),
--   aunque SEM-CAF-002 caduque antes.
-- ============================================================================

USE cafeteria_db;

-- ----------------------------------------------------------------------------
-- 1. Usuarios iniciales (solo si la tabla está vacía)
--    Mismas credenciales que crea AuthNegocio.AsegurarUsuariosIniciales():
--      admin  / 123456  (NIP 1234)  · Administrador
--      cajero / 123456              · Cajero
--      barista / 123456             · Barista (en bases existentes: Migracion_02)
--    Hashes bcrypt (RNF-09 / RI-04). Cambiar las contraseñas antes de producción.
-- ----------------------------------------------------------------------------
INSERT INTO Usuario (id_rol, nombre, usuario, contrasena_hash, nip_hash, estado)
SELECT r.id_rol, u.nombre, u.usuario, u.contrasena_hash, u.nip_hash, 'Activo'
FROM (
    SELECT 'Administrador' AS rol, 'Administrador General' AS nombre, 'admin' AS usuario,
           '$2a$11$GWjaPCBo0VOZZrgqfgXfuu3pyX0UhfEpIKHmeDHJEll05SSoW07N2' AS contrasena_hash,
           '$2a$11$BZ1mu.9KzZ2BqFZhD5YO0OkRooOCWk9CEoDWOydV52fJf29a/xYpy' AS nip_hash
    UNION ALL
    SELECT 'Cajero', 'Sofía Ramírez', 'cajero',
           '$2a$11$M6DeZ3VibR1dw2gx5MP79O7ysFgNzREDbvTttlaBrp2QiuW9WGRIu', NULL
    UNION ALL
    SELECT 'Barista', 'Diego Hernández', 'barista',
           '$2a$11$M6DeZ3VibR1dw2gx5MP79O7ysFgNzREDbvTttlaBrp2QiuW9WGRIu', NULL
) u
INNER JOIN Rol r ON r.nombre = u.rol COLLATE utf8mb4_unicode_ci
WHERE NOT EXISTS (SELECT 1 FROM Usuario);

-- ----------------------------------------------------------------------------
-- 2. Recepciones (cabecera). Se identifican por el texto de observaciones.
-- ----------------------------------------------------------------------------
INSERT INTO Recepcion (id_proveedor, id_usuario, fecha_recepcion, observaciones)
SELECT p.id_proveedor,
       COALESCE(
           (SELECT u.id_usuario FROM Usuario u WHERE u.usuario = r.recibe COLLATE utf8mb4_unicode_ci LIMIT 1),
           (SELECT u.id_usuario FROM Usuario u
              INNER JOIN Rol ro ON ro.id_rol = u.id_rol
             WHERE ro.nombre = 'Administrador'
             ORDER BY u.id_usuario LIMIT 1)
       ),
       TIMESTAMP(DATE_SUB(CURDATE(), INTERVAL r.dias DAY), r.hora),
       r.observaciones
FROM (
    SELECT 'Tostadores del Sur S.A.' AS proveedor, 20 AS dias, '08:15:00' AS hora, 'admin' AS recibe,
           'Semilla inicial R01: café, matcha y cacao' AS observaciones
    UNION ALL SELECT 'Distribuidora Azucarera y Jarabes MX', 15, '09:40:00', 'admin',
           'Semilla inicial R02: jarabes, bases y repostería'
    UNION ALL SELECT 'Insumos y Empaques del Centro', 30, '11:05:00', 'cajero',
           'Semilla inicial R03: vasos, tapas y servilletas'
    UNION ALL SELECT 'Lácteos y Derivados Alpura Distribución', 6, '07:30:00', 'cajero',
           'Semilla inicial R04: lácteos'
    UNION ALL SELECT 'Tostadores del Sur S.A.', 3, '08:20:00', 'cajero',
           'Semilla inicial R05: reposición de café en grano'
    UNION ALL SELECT 'Lácteos y Derivados Alpura Distribución', 1, '07:45:00', 'cajero',
           'Semilla inicial R06: reposición de leche y huevo'
    UNION ALL SELECT 'Insumos y Empaques del Centro', 1, '10:10:00', 'cajero',
           'Semilla inicial R07: hielo'
) r
INNER JOIN Proveedor p ON p.nombre = r.proveedor COLLATE utf8mb4_unicode_ci
WHERE NOT EXISTS (
    SELECT 1 FROM Recepcion x WHERE x.observaciones = r.observaciones COLLATE utf8mb4_unicode_ci
);

-- ----------------------------------------------------------------------------
-- 3. Lotes. Cantidades en la unidad del insumo (kg, L, ml o pz).
--    vida = días de vida útil desde la recepción; NULL = sin caducidad.
--    Costos unitarios en MXN por unidad del insumo.
--    Sin lote a propósito (estado Blanco): Leche de Almendra, Popotes, Bolsa Kraft.
-- ----------------------------------------------------------------------------
INSERT INTO Lote (id_recepcion, id_insumo, codigo_lote, codigo_barras,
                  fecha_recepcion, fecha_caducidad, cantidad_inicial, cantidad_disponible, costo_unitario)
SELECT rc.id_recepcion, i.id_insumo, l.codigo, l.barras,
       rc.fecha_recepcion,
       DATE_ADD(DATE(rc.fecha_recepcion), INTERVAL l.vida DAY),
       l.cantidad, l.cantidad, l.costo
FROM (
    --              recepción  insumo                         código          código de barras  cantidad   costo    vida
    SELECT 'R01' AS rec, 'Café en Grano Espresso' AS insumo, 'SEM-CAF-001' AS codigo, '7509900100011' AS barras, 6.000 AS cantidad, 285.00 AS costo, 180 AS vida
    UNION ALL SELECT 'R01', 'Café Descafeinado',          'SEM-DES-001', '7509900100028',    3.000,  330.00, 180
    UNION ALL SELECT 'R01', 'Té Matcha en Polvo',         'SEM-MAT-001', '7509900100035',    1.200,  920.00, 240
    UNION ALL SELECT 'R01', 'Cacao en Polvo',             'SEM-CAC-001', '7509900100042',    5.000,  185.00, 300

    UNION ALL SELECT 'R02', 'Jarabe de Vainilla',         'SEM-JVA-001', '7509900200018', 1500.000,    0.19, 365
    UNION ALL SELECT 'R02', 'Jarabe de Caramelo',         'SEM-JCA-001', '7509900200025', 2800.000,    0.19, 365
    UNION ALL SELECT 'R02', 'Jarabe de Chocolate',        'SEM-JCH-001', '7509900200032', 3200.000,    0.17, 365
    UNION ALL SELECT 'R02', 'Mermelada de Fresa',         'SEM-MER-001', '7509900200049',    1.500,   86.00, 180
    UNION ALL SELECT 'R02', 'Base Neutro Frappé',         'SEM-BFR-001', '7509900200056',    9.000,  165.00, 270
    UNION ALL SELECT 'R02', 'Azúcar Refinada',            'SEM-AZU-001', '7509900200063',   18.000,   28.50, 730
    UNION ALL SELECT 'R02', 'Harina de Trigo',            'SEM-HAR-001', '7509900200070',   25.000,   23.00, 150
    UNION ALL SELECT 'R02', 'Chispas de Chocolate',       'SEM-CHI-001', '7509900200087',    4.000,  155.00, 240
    UNION ALL SELECT 'R02', 'Canela Molida',              'SEM-CAN-001', '7509900200094',    1.500,  310.00, 365

    UNION ALL SELECT 'R03', 'Vaso Térmico 12 oz',         'SEM-VTE-001', '7509900300015',  300.000,    1.85, NULL
    UNION ALL SELECT 'R03', 'Vaso Plástico Frío 16 oz',   'SEM-VFR-001', '7509900300022',  250.000,    2.10, NULL
    UNION ALL SELECT 'R03', 'Tapa para Vaso',             'SEM-TAP-001', '7509900300039',  350.000,    0.65, NULL
    UNION ALL SELECT 'R03', 'Servilletas',                'SEM-SER-001', '7509900300046',  150.000,    0.15, NULL

    UNION ALL SELECT 'R04', 'Leche Entera',               'SEM-LEC-001', '7509900400012',    8.000,   26.50,  10
    UNION ALL SELECT 'R04', 'Leche Deslactosada',         'SEM-LDE-001', '7509900400029',    6.000,   29.90,  10
    UNION ALL SELECT 'R04', 'Crema Batida',               'SEM-CRE-001', '7509900400036',    3.000,   92.00,  14
    UNION ALL SELECT 'R04', 'Queso Crema',                'SEM-QUE-001', '7509900400043',    4.000,  145.00,  25
    UNION ALL SELECT 'R04', 'Mantequilla Sin Sal',        'SEM-MAN-001', '7509900400050',    7.000,  198.00,  60

    UNION ALL SELECT 'R05', 'Café en Grano Espresso',     'SEM-CAF-002', '7509900100059',    8.000,  292.50,  90

    UNION ALL SELECT 'R06', 'Leche Entera',               'SEM-LEC-002', '7509900400067',   24.000,   27.00,  12
    UNION ALL SELECT 'R06', 'Huevo Blanco',               'SEM-HUE-001', '7509900400074',   90.000,    3.25,  21

    UNION ALL SELECT 'R07', 'Hielo en Cubos',             'SEM-HIE-001', '7509900300053',   40.000,    8.50, NULL
) l
INNER JOIN Recepcion rc ON rc.observaciones LIKE CONCAT('Semilla inicial ', l.rec, ':%') COLLATE utf8mb4_unicode_ci
INNER JOIN Insumo i     ON i.nombre = l.insumo COLLATE utf8mb4_unicode_ci
WHERE NOT EXISTS (
    SELECT 1 FROM Lote x WHERE x.codigo_lote = l.codigo COLLATE utf8mb4_unicode_ci
);

-- ----------------------------------------------------------------------------
-- 4. Kardex: movimiento ENTREGA por cada lote de la semilla (RI-05)
-- ----------------------------------------------------------------------------
INSERT INTO MovimientoInventario (id_lote, id_usuario, tipo, cantidad, fecha_movimiento, observaciones)
SELECT l.id_lote, rc.id_usuario, 'ENTREGA', l.cantidad_inicial, l.fecha_recepcion,
       CONCAT('Entrada de lote ', l.codigo_lote, ' (semilla inicial)')
FROM Lote l
INNER JOIN Recepcion rc ON rc.id_recepcion = l.id_recepcion
WHERE l.codigo_lote LIKE 'SEM-%'
  AND NOT EXISTS (
      SELECT 1 FROM MovimientoInventario m
      WHERE m.id_lote = l.id_lote AND m.tipo = 'ENTREGA'
  );

-- ----------------------------------------------------------------------------
-- 5. Verificación: existencias y estado del semáforo por insumo
-- ----------------------------------------------------------------------------
SELECT i.nombre                                   AS insumo,
       u.abreviatura                              AS unidad,
       COUNT(l.id_lote)                           AS lotes,
       COALESCE(SUM(l.cantidad_disponible), 0)    AS existencia,
       i.stock_maximo,
       ROUND(COALESCE(SUM(l.cantidad_disponible), 0) / i.stock_maximo * 100, 1) AS porcentaje,
       CASE
           WHEN COALESCE(SUM(l.cantidad_disponible), 0) <= 0 THEN 'Blanco'
           WHEN COALESCE(SUM(l.cantidad_disponible), 0) / i.stock_maximo * 100 >= cfg.verde_minimo THEN 'Verde'
           WHEN COALESCE(SUM(l.cantidad_disponible), 0) / i.stock_maximo * 100 >= cfg.amarillo_minimo THEN 'Amarillo'
           ELSE 'Rojo'
       END                                        AS semaforo
FROM Insumo i
INNER JOIN UnidadMedida u ON u.id_unidad = i.id_unidad
CROSS JOIN (SELECT verde_minimo, amarillo_minimo FROM ConfiguracionGeneral ORDER BY id_configuracion LIMIT 1) cfg
LEFT JOIN Lote l ON l.id_insumo = i.id_insumo AND l.cantidad_disponible > 0
WHERE i.estado = TRUE
GROUP BY i.id_insumo, i.nombre, u.abreviatura, i.stock_maximo, cfg.verde_minimo, cfg.amarillo_minimo
ORDER BY semaforo, i.nombre;
