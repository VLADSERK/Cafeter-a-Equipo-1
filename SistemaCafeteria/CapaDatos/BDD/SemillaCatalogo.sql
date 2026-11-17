-- ============================================================================
-- SEMILLA DEL CATÁLOGO: 20 productos con su receta versión 1
-- Ejecutar DESPUÉS de IS2.sql. Se puede ejecutar varias veces sin duplicar.
-- Cantidades en la unidad de cada insumo (kg, L, ml o pz).
-- ============================================================================

USE cafeteria_db;

-- 1. Productos
INSERT INTO Producto (id_categoria, nombre, precio, codigo_barras, imagen_ruta, estado)
SELECT c.id_categoria, p.nombre, p.precio, p.codigo, p.imagen, 'Activo'
FROM (
    SELECT 'Bebidas Calientes' AS categoria, 'Cappuccino' AS nombre, 58.00 AS precio, NULL AS codigo, 'capuchino.png' AS imagen
    UNION ALL SELECT 'Bebidas Calientes', 'Latte', 60.00, NULL, 'latte.png'
    UNION ALL SELECT 'Bebidas Calientes', 'Americano', 42.00, NULL, 'americano.png'
    UNION ALL SELECT 'Bebidas Calientes', 'Espresso', 38.00, NULL, 'expresso.png'
    UNION ALL SELECT 'Bebidas Calientes', 'Moka Caliente', 68.00, NULL, 'mokaC.png'
    UNION ALL SELECT 'Bebidas Calientes', 'Chocolate Caliente', 56.00, NULL, 'chocolateC.png'
    UNION ALL SELECT 'Bebidas Calientes', 'Matcha Latte', 65.00, NULL, 'MatchaLatte.png'
    UNION ALL SELECT 'Bebidas Frías', 'Latte Frío', 64.00, NULL, 'LatteF.jpeg'
    UNION ALL SELECT 'Bebidas Frías', 'Cold Brew', 55.00, '7501002348112', 'ColdBrew.png'
    UNION ALL SELECT 'Bebidas Frías', 'Frappé Moka', 74.00, NULL, 'FrappeMoka.png'
    UNION ALL SELECT 'Bebidas Frías', 'Matcha Iced Latte', 68.00, NULL, 'MatchaIcedLatte.png'
    UNION ALL SELECT 'Bebidas Frías', 'Chocolate Frío', 58.00, NULL, 'ChocolateF.jpeg'
    UNION ALL SELECT 'Panadería y Repostería', 'Galleta de Chocolate', 32.00, '7501234567817', 'GalletaCh.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Brownie de Chocolate', 48.00, '7501234567824', 'BrowniedeCh.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Cheesecake de Fresa', 65.00, '7501234567831', 'CheesecakeFresa.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Croissant Mantequilla', 45.00, '7501234567893', 'CroissantM.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Dona Glaseada', 35.00, '7501234567855', 'DonaGlaseada.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Muffin de Arándanos', 44.00, '7501234567862', 'MuffinArandanos.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Panqué de Plátano', 40.00, '7501234567879', 'PanquedePlatano.png'
    UNION ALL SELECT 'Panadería y Repostería', 'Rol de Canela', 52.00, '7501234567886', 'RoldeC.png'
) p
INNER JOIN Categoria c ON c.nombre = p.categoria COLLATE utf8mb4_unicode_ci
WHERE NOT EXISTS (
    SELECT 1 FROM Producto x WHERE x.nombre = p.nombre COLLATE utf8mb4_unicode_ci
);

-- 2. Versión 1 de receta para cada producto que todavía no tenga versiones
INSERT INTO VersionReceta (id_producto, numero_version, fecha_creacion, estado)
SELECT p.id_producto, 1, NOW(), TRUE
FROM Producto p
WHERE NOT EXISTS (
    SELECT 1 FROM VersionReceta v WHERE v.id_producto = p.id_producto
);

-- 3. Ingredientes de la versión 1 (solo si esa versión aún no tiene ingredientes)
INSERT INTO DetalleReceta (id_version_receta, id_insumo, id_unidad, cantidad)
SELECT vr.id_version_receta, i.id_insumo, i.id_unidad, r.cantidad
FROM (
    SELECT 'Cappuccino' AS producto, 'Café en Grano Espresso' AS insumo, 0.018 AS cantidad
    UNION ALL SELECT 'Cappuccino', 'Leche Entera', 0.220
    UNION ALL SELECT 'Cappuccino', 'Vaso Térmico 12 oz', 1
    UNION ALL SELECT 'Cappuccino', 'Tapa para Vaso', 1

    UNION ALL SELECT 'Latte', 'Café en Grano Espresso', 0.018
    UNION ALL SELECT 'Latte', 'Leche Entera', 0.260
    UNION ALL SELECT 'Latte', 'Vaso Térmico 12 oz', 1

    UNION ALL SELECT 'Americano', 'Café en Grano Espresso', 0.018
    UNION ALL SELECT 'Americano', 'Vaso Térmico 12 oz', 1
    UNION ALL SELECT 'Americano', 'Tapa para Vaso', 1

    UNION ALL SELECT 'Espresso', 'Café en Grano Espresso', 0.018

    UNION ALL SELECT 'Moka Caliente', 'Café en Grano Espresso', 0.018
    UNION ALL SELECT 'Moka Caliente', 'Leche Entera', 0.220
    UNION ALL SELECT 'Moka Caliente', 'Jarabe de Chocolate', 30

    UNION ALL SELECT 'Chocolate Caliente', 'Cacao en Polvo', 0.025
    UNION ALL SELECT 'Chocolate Caliente', 'Leche Entera', 0.250

    UNION ALL SELECT 'Matcha Latte', 'Té Matcha en Polvo', 0.012
    UNION ALL SELECT 'Matcha Latte', 'Leche Entera', 0.240

    UNION ALL SELECT 'Latte Frío', 'Café en Grano Espresso', 0.018
    UNION ALL SELECT 'Latte Frío', 'Leche Entera', 0.220
    UNION ALL SELECT 'Latte Frío', 'Hielo en Cubos', 0.150
    UNION ALL SELECT 'Latte Frío', 'Vaso Plástico Frío 16 oz', 1

    UNION ALL SELECT 'Cold Brew', 'Café Descafeinado', 0.025
    UNION ALL SELECT 'Cold Brew', 'Hielo en Cubos', 0.180

    UNION ALL SELECT 'Frappé Moka', 'Base Neutro Frappé', 0.045
    UNION ALL SELECT 'Frappé Moka', 'Jarabe de Chocolate', 30
    UNION ALL SELECT 'Frappé Moka', 'Crema Batida', 0.035
    UNION ALL SELECT 'Frappé Moka', 'Hielo en Cubos', 0.200

    UNION ALL SELECT 'Matcha Iced Latte', 'Té Matcha en Polvo', 0.012
    UNION ALL SELECT 'Matcha Iced Latte', 'Leche Deslactosada', 0.220
    UNION ALL SELECT 'Matcha Iced Latte', 'Hielo en Cubos', 0.150

    UNION ALL SELECT 'Chocolate Frío', 'Cacao en Polvo', 0.025
    UNION ALL SELECT 'Chocolate Frío', 'Leche Entera', 0.230
    UNION ALL SELECT 'Chocolate Frío', 'Hielo en Cubos', 0.150

    UNION ALL SELECT 'Galleta de Chocolate', 'Harina de Trigo', 0.060
    UNION ALL SELECT 'Galleta de Chocolate', 'Mantequilla Sin Sal', 0.025
    UNION ALL SELECT 'Galleta de Chocolate', 'Chispas de Chocolate', 0.030

    UNION ALL SELECT 'Brownie de Chocolate', 'Cacao en Polvo', 0.030
    UNION ALL SELECT 'Brownie de Chocolate', 'Mantequilla Sin Sal', 0.035
    UNION ALL SELECT 'Brownie de Chocolate', 'Huevo Blanco', 1

    UNION ALL SELECT 'Cheesecake de Fresa', 'Queso Crema', 0.085
    UNION ALL SELECT 'Cheesecake de Fresa', 'Mermelada de Fresa', 0.030

    UNION ALL SELECT 'Croissant Mantequilla', 'Harina de Trigo', 0.080
    UNION ALL SELECT 'Croissant Mantequilla', 'Mantequilla Sin Sal', 0.045

    UNION ALL SELECT 'Dona Glaseada', 'Harina de Trigo', 0.065
    UNION ALL SELECT 'Dona Glaseada', 'Azúcar Refinada', 0.025

    UNION ALL SELECT 'Muffin de Arándanos', 'Harina de Trigo', 0.070
    UNION ALL SELECT 'Muffin de Arándanos', 'Huevo Blanco', 1

    UNION ALL SELECT 'Panqué de Plátano', 'Harina de Trigo', 0.075
    UNION ALL SELECT 'Panqué de Plátano', 'Canela Molida', 0.003

    UNION ALL SELECT 'Rol de Canela', 'Harina de Trigo', 0.085
    UNION ALL SELECT 'Rol de Canela', 'Canela Molida', 0.005
    UNION ALL SELECT 'Rol de Canela', 'Queso Crema', 0.030
) r
INNER JOIN Producto p       ON p.nombre = r.producto COLLATE utf8mb4_unicode_ci
INNER JOIN VersionReceta vr ON vr.id_producto = p.id_producto AND vr.numero_version = 1
INNER JOIN Insumo i         ON i.nombre = r.insumo COLLATE utf8mb4_unicode_ci
WHERE NOT EXISTS (
    SELECT 1 FROM DetalleReceta d WHERE d.id_version_receta = vr.id_version_receta
);

-- 4. Verificación: cada producto debe mostrar su número de ingredientes
SELECT p.nombre, c.nombre AS categoria, p.precio, COUNT(dr.id_detalle_receta) AS ingredientes
FROM Producto p
INNER JOIN Categoria c ON c.id_categoria = p.id_categoria
LEFT JOIN VersionReceta vr ON vr.id_producto = p.id_producto AND vr.estado = TRUE
LEFT JOIN DetalleReceta dr ON dr.id_version_receta = vr.id_version_receta
GROUP BY p.id_producto, p.nombre, c.nombre, p.precio
ORDER BY c.nombre, p.nombre;
