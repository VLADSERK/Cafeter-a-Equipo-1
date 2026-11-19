-- ============================================================================
-- MIGRACIÓN 03 · Turno del usuario (Matutino / Vespertino)
-- Versión 1.1 · 19/11/2026 · RI-06 (scripts SQL versionados)
--
-- Motivo: la Configuración Global (RF-12) muestra en la tarjeta "Configuración del
--         Rol de Cajero" el selector "Datos Generales / Turno" con dos opciones:
--         Matutino y Vespertino. IS2.sql ya crea la columna; esta migración la
--         agrega (o la convierte) en bases creadas con versiones anteriores.
--
-- v1.0 creaba la columna como varchar(100); v1.1 la deja como ENUM. Si ya existía
-- como texto libre, los valores que no sean Matutino/Vespertino quedan en NULL.
--
-- La aplicación también crea la columna si no existe (UsuarioDatos.AsegurarColumnaTurno),
-- así que este script es opcional; sirve para dejar todas las bases iguales.
-- Seguro de ejecutar varias veces. MySQL no admite ADD COLUMN IF NOT EXISTS,
-- por eso se consulta information_schema.
-- ============================================================================

USE cafeteria_db;

SET @existe := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Usuario'
      AND COLUMN_NAME = 'turno'
);

-- 1. Si existía como texto libre, normalizar antes de convertir a ENUM
SET @sql := IF(@existe = 1,
    'UPDATE Usuario
        SET turno = CASE
                        WHEN turno LIKE ''%matutino%'' THEN ''Matutino''
                        WHEN turno LIKE ''%vespertino%'' THEN ''Vespertino''
                        ELSE NULL
                    END
      WHERE turno IS NOT NULL',
    'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 2. Crear la columna o dejarla como ENUM
SET @sql := IF(@existe = 0,
    'ALTER TABLE Usuario ADD COLUMN turno ENUM(''Matutino'', ''Vespertino'') NULL COMMENT ''Turno del personal (RF-12, Configuración Global)'' AFTER estado',
    'ALTER TABLE Usuario MODIFY COLUMN turno ENUM(''Matutino'', ''Vespertino'') NULL COMMENT ''Turno del personal (RF-12, Configuración Global)''');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Verificación
SELECT u.usuario, r.nombre AS rol, u.turno
FROM Usuario u
INNER JOIN Rol r ON r.id_rol = u.id_rol
ORDER BY r.id_rol, u.usuario;
