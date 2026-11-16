DROP DATABASE IF EXISTS cafeteria_db;

CREATE DATABASE cafeteria_db
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE cafeteria_db;

-- ============================================================================
-- 1. SEGURIDAD Y CONFIGURACIÓN
-- ============================================================================

CREATE TABLE `Rol` (
  `id_rol` int PRIMARY KEY AUTO_INCREMENT,
  `nombre` varchar(50) UNIQUE NOT NULL,
  `descripcion` varchar(150)
);

CREATE TABLE `Usuario` (
  `id_usuario` int PRIMARY KEY AUTO_INCREMENT,
  `id_rol` int NOT NULL,
  `nombre` varchar(100) NOT NULL,
  `usuario` varchar(50) UNIQUE NOT NULL,
  `contrasena_hash` varchar(255) NOT NULL,
  `nip_hash` varchar(255) NULL COMMENT 'Solo para rol Administrador (autorización de mermas, cancelaciones y config)',
  `estado` ENUM ('Activo', 'Inactivo') NOT NULL DEFAULT 'Activo'
);

CREATE TABLE `ConfiguracionGeneral` (
  `id_configuracion` int PRIMARY KEY AUTO_INCREMENT,
  `verde_minimo` decimal(5,2) NOT NULL DEFAULT 50.00,
  `amarillo_minimo` decimal(5,2) NOT NULL DEFAULT 21.00,
  `amarillo_maximo` decimal(5,2) NOT NULL DEFAULT 49.00,
  `rojo_maximo` decimal(5,2) NOT NULL DEFAULT 20.00,
  `limite_economico_merma` decimal(12,2) NOT NULL DEFAULT 100.00
);

-- ============================================================================
-- 2. INVENTARIO, PROVEEDORES Y LOTES PEPS
-- ============================================================================

CREATE TABLE `UnidadMedida` (
  `id_unidad` int PRIMARY KEY AUTO_INCREMENT,
  `nombre` varchar(50) NOT NULL,
  `abreviatura` varchar(10) UNIQUE NOT NULL,
  `tipo` varchar(20) NOT NULL COMMENT 'Masa, volumen o unidad'
);

CREATE TABLE `Insumo` (
  `id_insumo` int PRIMARY KEY AUTO_INCREMENT,
  `id_unidad` int NOT NULL,
  `categoria` varchar(60) NOT NULL DEFAULT 'General',
  `nombre` varchar(100) UNIQUE NOT NULL,
  `stock_minimo` decimal(12,3) NOT NULL DEFAULT 1.000,
  `stock_maximo` decimal(12,3) NOT NULL,
  `estado` boolean NOT NULL DEFAULT true
);

CREATE TABLE `Proveedor` (
  `id_proveedor` int PRIMARY KEY AUTO_INCREMENT,
  `nombre` varchar(120) NOT NULL,
  `telefono` varchar(20),
  `correo` varchar(100),
  `direccion` varchar(200),
  `estado` boolean NOT NULL DEFAULT true
);

CREATE TABLE `Recepcion` (
  `id_recepcion` int PRIMARY KEY AUTO_INCREMENT,
  `id_proveedor` int NOT NULL,
  `id_usuario` int NOT NULL,
  `fecha_recepcion` datetime NOT NULL,
  `observaciones` varchar(255)
);

CREATE TABLE `Lote` (
  `id_lote` int PRIMARY KEY AUTO_INCREMENT,
  `id_recepcion` int NOT NULL,
  `id_insumo` int NOT NULL,
  `codigo_lote` varchar(100) UNIQUE NOT NULL,
  `codigo_barras` varchar(100) UNIQUE NULL,
  `fecha_recepcion` datetime NOT NULL,
  `fecha_caducidad` date NULL,
  `cantidad_inicial` decimal(12,3) NOT NULL,
  `cantidad_disponible` decimal(12,3) NOT NULL,
  `costo_unitario` decimal(12,2) NOT NULL
);

-- ============================================================================
-- 3. CATÁLOGO DE PRODUCTOS Y RECETAS VERSIONADAS
-- ============================================================================

CREATE TABLE `Categoria` (
  `id_categoria` int PRIMARY KEY AUTO_INCREMENT,
  `nombre` varchar(60) UNIQUE NOT NULL
);

CREATE TABLE `Producto` (
  `id_producto` int PRIMARY KEY AUTO_INCREMENT,
  `id_categoria` int NOT NULL,
  `nombre` varchar(100) NOT NULL,
  `precio` decimal(12,2) NOT NULL,
  `codigo_barras` varchar(100) UNIQUE NULL,
  `imagen_ruta` varchar(255) NULL,
  `estado` ENUM ('Activo', 'Inactivo') NOT NULL DEFAULT 'Activo'
);

CREATE TABLE `VersionReceta` (
  `id_version_receta` int PRIMARY KEY AUTO_INCREMENT,
  `id_producto` int NOT NULL,
  `numero_version` int NOT NULL,
  `fecha_creacion` datetime NOT NULL,
  `estado` boolean NOT NULL DEFAULT true
);

CREATE TABLE `DetalleReceta` (
  `id_detalle_receta` int PRIMARY KEY AUTO_INCREMENT,
  `id_version_receta` int NOT NULL,
  `id_insumo` int NOT NULL,
  `id_unidad` int NOT NULL,
  `cantidad` decimal(12,3) NOT NULL
);

-- ============================================================================
-- 4. VENTAS, TRAZABILIDAD PEPS, MERMAS Y KARDEX
-- ============================================================================

CREATE TABLE `Venta` (
  `id_venta` int PRIMARY KEY AUTO_INCREMENT,
  `id_usuario` int NOT NULL,
  `folio` varchar(50) UNIQUE NOT NULL,
  `fecha_venta` datetime NOT NULL,
  `total` decimal(12,2) NOT NULL,
  `estado` ENUM ('Confirmada', 'Cancelada') NOT NULL DEFAULT 'Confirmada',
  `metodo_pago` varchar(30),
  `monto_recibido` decimal(12,2),
  `cambio` decimal(12,2),
  `motivo_cancelacion` varchar(255)
);

CREATE TABLE `DetalleVenta` (
  `id_detalle_venta` int PRIMARY KEY AUTO_INCREMENT,
  `id_venta` int NOT NULL,
  `id_producto` int NOT NULL,
  `id_version_receta` int NOT NULL,
  `cantidad` int NOT NULL,
  `precio_unitario` decimal(12,2) NOT NULL,
  `subtotal` decimal(12,2) NOT NULL
);

CREATE TABLE `HistorialRecetaVenta` (
  `id_historial` int PRIMARY KEY AUTO_INCREMENT,
  `id_detalle_venta` int NOT NULL COMMENT 'Vincula el detalle de la venta con los insumos',
  `id_insumo` int NOT NULL,
  `id_lote` int NOT NULL COMMENT 'Lote exacto del que se descontó (PEPS)',
  `cantidad_consumida` decimal(12,3) NOT NULL,
  `costo_unitario` decimal(12,2) NOT NULL
);

CREATE TABLE `Merma` (
  `id_merma` int PRIMARY KEY AUTO_INCREMENT,
  `id_insumo` int NOT NULL,
  `id_lote` int NULL,
  `id_usuario_registra` int NOT NULL,
  `id_usuario_autoriza` int NULL,
  `cantidad` decimal(12,3) NOT NULL,
  `motivo` varchar(100) NOT NULL,
  `descripcion` varchar(255),
  `fecha_merma` datetime NOT NULL,
  `costo_estimado` decimal(12,2) NOT NULL,
  `estado` ENUM ('Pendiente', 'Autorizada', 'Rechazada') NOT NULL DEFAULT 'Pendiente'
);

CREATE TABLE `MovimientoInventario` (
  `id_movimiento` int PRIMARY KEY AUTO_INCREMENT,
  `id_lote` int NOT NULL,
  `id_usuario` int NOT NULL,
  `id_detalle_venta` int NULL COMMENT 'Referencia directa a la venta',
  `id_merma` int NULL,
  `id_movimiento_origen` int NULL COMMENT 'Para reversiones o ajustes',
  `tipo` ENUM ('ENTREGA', 'CONSUMO_VENTA', 'MERMA', 'REVERSION') NOT NULL,
  `cantidad` decimal(12,3) NOT NULL,
  `fecha_movimiento` datetime NOT NULL,
  `observaciones` varchar(255)
);

-- ============================================================================
-- 5. ÍNDICES Y LLAVES FORÁNEAS
-- ============================================================================

CREATE UNIQUE INDEX `VersionReceta_index_0` ON `VersionReceta` (`id_producto`, `numero_version`);

ALTER TABLE `Usuario` ADD FOREIGN KEY (`id_rol`) REFERENCES `Rol` (`id_rol`);
ALTER TABLE `Producto` ADD FOREIGN KEY (`id_categoria`) REFERENCES `Categoria` (`id_categoria`);
ALTER TABLE `Insumo` ADD FOREIGN KEY (`id_unidad`) REFERENCES `UnidadMedida` (`id_unidad`);
ALTER TABLE `VersionReceta` ADD FOREIGN KEY (`id_producto`) REFERENCES `Producto` (`id_producto`);
ALTER TABLE `DetalleReceta` ADD FOREIGN KEY (`id_version_receta`) REFERENCES `VersionReceta` (`id_version_receta`);
ALTER TABLE `DetalleReceta` ADD FOREIGN KEY (`id_insumo`) REFERENCES `Insumo` (`id_insumo`);
ALTER TABLE `DetalleReceta` ADD FOREIGN KEY (`id_unidad`) REFERENCES `UnidadMedida` (`id_unidad`);
ALTER TABLE `Recepcion` ADD FOREIGN KEY (`id_proveedor`) REFERENCES `Proveedor` (`id_proveedor`);
ALTER TABLE `Recepcion` ADD FOREIGN KEY (`id_usuario`) REFERENCES `Usuario` (`id_usuario`);
ALTER TABLE `Lote` ADD FOREIGN KEY (`id_recepcion`) REFERENCES `Recepcion` (`id_recepcion`);
ALTER TABLE `Lote` ADD FOREIGN KEY (`id_insumo`) REFERENCES `Insumo` (`id_insumo`);
ALTER TABLE `Venta` ADD FOREIGN KEY (`id_usuario`) REFERENCES `Usuario` (`id_usuario`);
ALTER TABLE `DetalleVenta` ADD FOREIGN KEY (`id_venta`) REFERENCES `Venta` (`id_venta`);
ALTER TABLE `DetalleVenta` ADD FOREIGN KEY (`id_producto`) REFERENCES `Producto` (`id_producto`);
ALTER TABLE `DetalleVenta` ADD FOREIGN KEY (`id_version_receta`) REFERENCES `VersionReceta` (`id_version_receta`);
ALTER TABLE `HistorialRecetaVenta` ADD FOREIGN KEY (`id_detalle_venta`) REFERENCES `DetalleVenta` (`id_detalle_venta`);
ALTER TABLE `HistorialRecetaVenta` ADD FOREIGN KEY (`id_insumo`) REFERENCES `Insumo` (`id_insumo`);
ALTER TABLE `HistorialRecetaVenta` ADD FOREIGN KEY (`id_lote`) REFERENCES `Lote` (`id_lote`);
ALTER TABLE `Merma` ADD FOREIGN KEY (`id_insumo`) REFERENCES `Insumo` (`id_insumo`);
ALTER TABLE `Merma` ADD FOREIGN KEY (`id_usuario_registra`) REFERENCES `Usuario` (`id_usuario`);
ALTER TABLE `Merma` ADD FOREIGN KEY (`id_lote`) REFERENCES `Lote` (`id_lote`);
ALTER TABLE `Merma` ADD FOREIGN KEY (`id_usuario_autoriza`) REFERENCES `Usuario` (`id_usuario`);
ALTER TABLE `MovimientoInventario` ADD FOREIGN KEY (`id_lote`) REFERENCES `Lote` (`id_lote`);
ALTER TABLE `MovimientoInventario` ADD FOREIGN KEY (`id_usuario`) REFERENCES `Usuario` (`id_usuario`);
ALTER TABLE `MovimientoInventario` ADD FOREIGN KEY (`id_detalle_venta`) REFERENCES `DetalleVenta` (`id_detalle_venta`);
ALTER TABLE `MovimientoInventario` ADD FOREIGN KEY (`id_merma`) REFERENCES `Merma` (`id_merma`);
ALTER TABLE `MovimientoInventario` ADD FOREIGN KEY (`id_movimiento_origen`) REFERENCES `MovimientoInventario` (`id_movimiento`);

-- ============================================================================
-- 6. DATOS SEMILLA INICIALES
-- ============================================================================

INSERT INTO `ConfiguracionGeneral` (`verde_minimo`, `amarillo_minimo`, `amarillo_maximo`, `rojo_maximo`, `limite_economico_merma`)
VALUES (50.00, 21.00, 49.00, 20.00, 100.00);

INSERT INTO `Rol` (`nombre`, `descripcion`) VALUES
('Administrador', 'Control total: usuarios, recetas, entradas de almacén y autorización de mermas'),
('Cajero', 'Operación de punto de venta, cobro, recepción de mercancía y registro de mermas operativas');

INSERT INTO `UnidadMedida` (`nombre`, `abreviatura`, `tipo`) VALUES
('Gramo', 'g', 'Masa'),
('Kilogramo', 'kg', 'Masa'),
('Mililitro', 'ml', 'Volumen'),
('Litro', 'L', 'Volumen'),
('Pieza', 'pz', 'Unidad');

INSERT INTO `Categoria` (`nombre`) VALUES
('Bebidas Calientes'),
('Bebidas Frías'),
('Panadería y Repostería'),
('Alimentos');

INSERT INTO `Proveedor` (`nombre`, `telefono`, `correo`, `direccion`, `estado`) VALUES
('Tostadores del Sur S.A.', '55-4812-9034', 'ventas@tostadoresdelsur.mx', 'Col. Roma Norte, CDMX', 1),
('Lácteos y Derivados Alpura Distribución', '55-9011-2240', 'pedidos@lacteosdist.mx', 'Tlalnepantla, Edo. Méx.', 1),
('Distribuidora Azucarera y Jarabes MX', '55-4433-2211', 'contacto@azucareramx.com', 'Central de Abasto Bodega 42, CDMX', 1),
('Insumos y Empaques del Centro', '55-9988-7766', 'ventas@empaquescentro.mx', 'Col. Industrial Vallejo, CDMX', 1);

-- 27 Insumos completos (id_unidad: 2=kg, 3=ml, 4=L, 5=pz)
INSERT INTO `Insumo` (`id_unidad`, `categoria`, `nombre`, `stock_minimo`, `stock_maximo`, `estado`) VALUES
(2, 'Café e Infusiones', 'Café en Grano Espresso', 5.000, 20.000, 1),
(4, 'Lácteos', 'Leche Entera', 12.000, 60.000, 1),
(4, 'Lácteos', 'Leche Deslactosada', 8.000, 40.000, 1),
(3, 'Jarabes y Saborizantes', 'Jarabe de Vainilla', 1000.000, 5000.000, 1),
(5, 'Desechables', 'Vaso Térmico 12 oz', 100.000, 500.000, 1),
(2, 'Café e Infusiones', 'Café Descafeinado', 2.000, 10.000, 1),
(2, 'Café e Infusiones', 'Té Matcha en Polvo', 0.500, 3.000, 1),
(2, 'Café e Infusiones', 'Cacao en Polvo', 1.500, 8.000, 1),
(4, 'Lácteos', 'Leche de Almendra', 4.000, 20.000, 1),
(4, 'Lácteos', 'Crema Batida', 2.000, 10.000, 1),
(2, 'Lácteos', 'Queso Crema', 2.000, 10.000, 1),
(2, 'Lácteos', 'Mantequilla Sin Sal', 2.500, 12.000, 1),
(3, 'Jarabes y Saborizantes', 'Jarabe de Caramelo', 1000.000, 5000.000, 1),
(3, 'Jarabes y Saborizantes', 'Jarabe de Chocolate', 1000.000, 5000.000, 1),
(2, 'Jarabes y Saborizantes', 'Mermelada de Fresa', 1.500, 8.000, 1),
(2, 'Polvos y Bases', 'Base Neutro Frappé', 3.000, 15.000, 1),
(2, 'Polvos y Bases', 'Azúcar Refinada', 5.000, 25.000, 1),
(2, 'Repostería y Panadería', 'Harina de Trigo', 8.000, 40.000, 1),
(2, 'Repostería y Panadería', 'Chispas de Chocolate', 2.000, 10.000, 1),
(2, 'Repostería y Panadería', 'Canela Molida', 0.500, 3.000, 1),
(5, 'Repostería y Panadería', 'Huevo Blanco', 30.000, 180.000, 1),
(2, 'Congelados', 'Hielo en Cubos', 15.000, 80.000, 1),
(5, 'Desechables', 'Vaso Plástico Frío 16 oz', 80.000, 400.000, 1),
(5, 'Desechables', 'Tapa para Vaso', 100.000, 500.000, 1),
(5, 'Desechables', 'Servilletas', 200.000, 1000.000, 1),
(5, 'Desechables', 'Popotes Biodegradables', 100.000, 500.000, 1),
(5, 'Desechables', 'Bolsa Kraft para Llevar', 50.000, 250.000, 1);