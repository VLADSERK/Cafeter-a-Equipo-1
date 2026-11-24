# Manual de Instalación y Revisión — SistemaCafeteria

Consulta el manual completo ilustrado paso a paso aquí:
**[Abrir Manual de Instalación con capturas (PDF)](Documentación/Manual_Instalacion.pdf)**

---

## Resumen rápido de configuración

1. **Base de datos (MySQL):** Ejecuta en orden `01_IS2.sql`, `02_SemillaCatalogo.sql` y `03_Contraseña.sql` (script temporal de pruebas).
2. **Cadena de conexión (`CapaDatos/ConexionMySql.cs`):**
   ```text
   Server=localhost;Port=3306;Database=cafeteria_db;User ID=root;Password=123456;SslMode=Preferred;
