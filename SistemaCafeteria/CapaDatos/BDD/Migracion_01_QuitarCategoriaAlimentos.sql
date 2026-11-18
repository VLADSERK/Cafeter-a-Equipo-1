-- ============================================================================
-- MIGRACIÓN 01 · Quitar la categoría "Alimentos"
-- Versión 1.0 · 17/11/2026 · RI-06 (scripts SQL versionados)
--
-- Motivo: RF-04 define solo tres categorías de producto
--         (Bebidas Calientes, Bebidas Frías y Repostería). IS2.sql ya no la crea;
--         esta migración la elimina de las bases creadas con la versión anterior.
--
-- Seguro de ejecutar varias veces. Solo borra la categoría si ningún producto
-- la usa; si alguno la usa, no se toca y la consulta final lo muestra.
-- ============================================================================

USE cafeteria_db;

DELETE FROM Categoria
WHERE nombre = 'Alimentos' COLLATE utf8mb4_unicode_ci
  AND NOT EXISTS (
      SELECT 1 FROM Producto p WHERE p.id_categoria = Categoria.id_categoria
  );

-- Verificación: debe listar solo las 3 categorías de RF-04.
-- Si "Alimentos" sigue apareciendo, mueve primero sus productos a otra categoría.
SELECT c.id_categoria, c.nombre, COUNT(p.id_producto) AS productos
FROM Categoria c
LEFT JOIN Producto p ON p.id_categoria = c.id_categoria
GROUP BY c.id_categoria, c.nombre
ORDER BY c.nombre;
