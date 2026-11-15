using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaPresentaciòn.Estilos 
{
    public record InsumoBase(
        int Id,
        string Nombre,
        string Categoria,
        string UnidadDefault,
        decimal StockMinimo
    );

    public record LoteInventarioMock(
        string CodigoLote,
        string Insumo,
        string Categoria,
        decimal CantidadActual,
        decimal StockMinimo,
        string Unidad,
        DateTime FechaCaducidad,
        string EstadoSemaforo // "Optimo", "Preventivo", "Critico"
    );

    public static class CatalogoInsumosMock
    {
        // Lista completa de los 27 ingredientes del sistema
        public static readonly List<InsumoBase> ListaIngredientes = new()
        {
            new(1,  "Harina de trigo",               "Harinas y Secos",     "kg", 5.00m),
            new(2,  "Mantequilla",                   "Lácteos y Grasas",    "kg", 3.00m),
            new(3,  "Azúcar blanca",                 "Endulzantes",         "kg", 4.00m),
            new(4,  "Azúcar mascabado",              "Endulzantes",         "kg", 3.00m),
            new(5,  "Azúcar glass",                  "Endulzantes",         "kg", 2.00m),
            new(6,  "Huevos",                        "Frescos y Perecederos","pz", 30.00m),
            new(7,  "Leche",                         "Lácteos y Grasas",    "L",  10.00m),
            new(8,  "Crema para batir",              "Lácteos y Grasas",    "L",  4.00m),
            new(9,  "Queso crema",                   "Lácteos y Grasas",    "kg", 2.50m),
            new(10, "Chocolate semiamargo",          "Chocolates y Cacao",  "kg", 2.00m),
            new(11, "Chispas de chocolate semiamargo","Chocolates y Cacao", "kg", 2.00m),
            new(12, "Cocoa en polvo",                "Chocolates y Cacao",  "kg", 1.50m),
            new(13, "Esencia de vainilla",           "Saborizantes y Especias","ml", 500.00m),
            new(14, "Polvo para hornear",            "Polvos y Leudantes",  "g",  500.00m),
            new(15, "Bicarbonato de sodio",          "Polvos y Leudantes",  "g",  400.00m),
            new(16, "Levadura seca",                 "Polvos y Leudantes",  "g",  300.00m),
            new(17, "Sal",                           "Harinas y Secos",     "kg", 1.00m),
            new(18, "Canela en polvo",               "Saborizantes y Especias","g", 250.00m),
            new(19, "Plátanos maduros",              "Frutas y Toppings",   "kg", 3.00m),
            new(20, "Nuez",                          "Frutas y Toppings",   "kg", 1.50m),
            new(21, "Fresas",                        "Frutas y Toppings",   "kg", 2.00m),
            new(22, "Arándanos",                     "Frutas y Toppings",   "kg", 1.50m),
            new(23, "Mermelada de fresa",            "Frutas y Toppings",   "kg", 2.00m),
            new(24, "Galletas María",                "Galletas y Bases",    "kg", 2.00m),
            new(25, "Jugo de limón",                 "Frutas y Toppings",   "L",  1.50m),
            new(26, "Aceite vegetal",                "Lácteos y Grasas",    "L",  3.00m),
            new(27, "Agua",                          "Líquidos Base",       "L",  20.00m)
        };

        // Lotes iniciales simulados para la tabla PEPS de Inventario (Pantalla 5.0)
        public static List<LoteInventarioMock> ObtenerLotesIniciales()
        {
            DateTime hoy = DateTime.Now;
            return new List<LoteInventarioMock>
            {
                new("LOT-INS-20261101-001", "Harina de trigo",                "Harinas y Secos",      18.00m, 5.00m,  "kg", hoy.AddMonths(5),  "Optimo"),
                new("LOT-INS-20261110-002", "Mantequilla",                    "Lácteos y Grasas",     2.20m,  3.00m,  "kg", hoy.AddDays(12),   "Preventivo"),
                new("LOT-INS-20261105-003", "Azúcar blanca",                  "Endulzantes",          14.50m, 4.00m,  "kg", hoy.AddMonths(8),  "Optimo"),
                new("LOT-INS-20261106-004", "Azúcar mascabado",               "Endulzantes",          9.00m,  3.00m,  "kg", hoy.AddMonths(7),  "Optimo"),
                new("LOT-INS-20261107-005", "Azúcar glass",                   "Endulzantes",          1.10m,  2.00m,  "kg", hoy.AddMonths(6),  "Critico"),
                new("LOT-INS-20261113-006", "Huevos",                         "Frescos y Perecederos",90.00m, 30.00m, "pz", hoy.AddDays(10),   "Optimo"),
                new("LOT-INS-20261114-007", "Leche",                          "Lácteos y Grasas",     7.00m,  10.00m, "L",  hoy.AddDays(4),    "Preventivo"),
                new("LOT-INS-20261112-008", "Crema para batir",               "Lácteos y Grasas",     1.50m,  4.00m,  "L",  hoy.AddDays(5),    "Critico"),
                new("LOT-INS-20261111-009", "Queso crema",                    "Lácteos y Grasas",     4.50m,  2.50m,  "kg", hoy.AddDays(18),   "Optimo"),
                new("LOT-INS-20261103-010", "Chocolate semiamargo",           "Chocolates y Cacao",   6.00m,  2.00m,  "kg", hoy.AddMonths(4),  "Optimo"),
                new("LOT-INS-20261104-011", "Chispas de chocolate semiamargo","Chocolates y Cacao",   1.40m,  2.00m,  "kg", hoy.AddMonths(4),  "Preventivo"),
                new("LOT-INS-20261102-012", "Cocoa en polvo",                 "Chocolates y Cacao",   3.80m,  1.50m,  "kg", hoy.AddMonths(6),  "Optimo"),
                new("LOT-INS-20261025-013", "Esencia de vainilla",            "Saborizantes y Especias",1200.00m,500.00m,"ml",hoy.AddMonths(9),"Optimo"),
                new("LOT-INS-20261020-014", "Polvo para hornear",             "Polvos y Leudantes",   1500.00m,500.00m,"g", hoy.AddMonths(6), "Optimo"),
                new("LOT-INS-20261021-015", "Bicarbonato de sodio",           "Polvos y Leudantes",   950.00m, 400.00m,"g", hoy.AddMonths(10),"Optimo"),
                new("LOT-INS-20261028-016", "Levadura seca",                  "Polvos y Leudantes",   180.00m, 300.00m,"g", hoy.AddMonths(3), "Preventivo"),
                new("LOT-INS-20261015-017", "Sal",                            "Harinas y Secos",      5.00m,  1.00m,  "kg", hoy.AddYears(1),   "Optimo"),
                new("LOT-INS-20261018-018", "Canela en polvo",                "Saborizantes y Especias",600.00m,250.00m,"g", hoy.AddMonths(7), "Optimo"),
                new("LOT-INS-20261114-019", "Plátanos maduros",               "Frutas y Toppings",    2.00m,  3.00m,  "kg", hoy.AddDays(2),    "Critico"),
                new("LOT-INS-20261108-020", "Nuez",                           "Frutas y Toppings",    3.20m,  1.50m,  "kg", hoy.AddMonths(3),  "Optimo"),
                new("LOT-INS-20261114-021", "Fresas",                         "Frutas y Toppings",    1.50m,  2.00m,  "kg", hoy.AddDays(3),    "Preventivo"),
                new("LOT-INS-20261113-022", "Arándanos",                      "Frutas y Toppings",    2.50m,  1.50m,  "kg", hoy.AddDays(6),    "Optimo"),
                new("LOT-INS-20261109-023", "Mermelada de fresa",             "Frutas y Toppings",    4.00m,  2.00m,  "kg", hoy.AddMonths(5),  "Optimo"),
                new("LOT-INS-20261102-024", "Galletas María",                 "Galletas y Bases",     6.50m,  2.00m,  "kg", hoy.AddMonths(4),  "Optimo"),
                new("LOT-INS-20261113-025", "Jugo de limón",                  "Frutas y Toppings",    0.90m,  1.50m,  "L",  hoy.AddDays(5),    "Critico"),
                new("LOT-INS-20261101-026", "Aceite vegetal",                 "Lácteos y Grasas",     8.00m,  3.00m,  "L",  hoy.AddMonths(6),  "Optimo"),
                new("LOT-INS-20261115-027", "Agua",                           "Líquidos Base",        60.00m, 20.00m, "L",  hoy.AddMonths(6),  "Optimo")
            };
        }
    }
}