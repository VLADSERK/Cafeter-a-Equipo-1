-- ============================================================================
-- MIGRACIÓN 02 · Rol Barista
-- Versión 1.0 · 17/11/2026 · RI-06 (scripts SQL versionados)
--
-- Motivo: RIU-09 habilita los módulos según el rol (Barista, Cajero o
--         Administrador). Los requisitos asignan los mismos privilegios a
--         "Barista/Cajero": punto de venta, recepción, consulta de inventario
--         y registro de mermas. La aplicación los trata igual (CapaLogica/Permisos.cs).
--
-- Crea el rol y un usuario de prueba "barista" (contraseña 123456, mismo hash
-- bcrypt que "cajero"). Cambiar la contraseña antes de producción.
-- Seguro de ejecutar varias veces: no duplica el rol ni el usuario.
-- ============================================================================

USE cafeteria_db;

INSERT INTO Rol (nombre, descripcion)
SELECT 'Barista', 'Preparación de bebidas, punto de venta, recepción de mercancía y registro de mermas operativas'
WHERE NOT EXISTS (SELECT 1 FROM Rol WHERE nombre = 'Barista' COLLATE utf8mb4_unicode_ci);

INSERT INTO Usuario (id_rol, nombre, usuario, contrasena_hash, nip_hash, estado)
SELECT r.id_rol, 'Diego Hernández', 'barista',
       '$2a$11$M6DeZ3VibR1dw2gx5MP79O7ysFgNzREDbvTttlaBrp2QiuW9WGRIu', NULL, 'Activo'
FROM Rol r
WHERE r.nombre = 'Barista' COLLATE utf8mb4_unicode_ci
  AND NOT EXISTS (SELECT 1 FROM Usuario WHERE usuario = 'barista' COLLATE utf8mb4_unicode_ci);

-- Verificación: roles y usuarios por rol
SELECT r.nombre AS rol, COUNT(u.id_usuario) AS usuarios
FROM Rol r
LEFT JOIN Usuario u ON u.id_rol = r.id_rol
GROUP BY r.id_rol, r.nombre
ORDER BY r.id_rol;
