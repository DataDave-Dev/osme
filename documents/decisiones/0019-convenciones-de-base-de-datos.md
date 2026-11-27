# 0019 — Convenciones de base de datos

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [convenciones de base de datos](../arquitectura/base-de-datos.md), [0017](0017-comunicacion-entre-modulos.md), [0005](0005-adaptacion-configuracion-y-extensiones.md)

## Decisión

Antes de escribir la primera tabla se fijan las reglas que siguen todas:

1. **El esquema es el prefijo.** Las tablas no repiten el nombre del módulo: `itdesk.ticket`.
2. **Esquemas por tipo de paquete:** `core`, el nombre del módulo oficial, `l10n_<país>` para localizaciones y `x_<autor>_<nombre>` para extensiones.
3. **Tablas en singular**, en inglés, minúsculas con guion bajo.
4. **Identificador UUID versión 7**, generado por el servidor.
5. **Columnas fijas en toda tabla de documento:** `id`, `company_id`, `created_at`, `created_by`, `updated_at`, `updated_by`, `version`, `custom`.
6. **Llaves foráneas reales dentro del paquete y hacia el núcleo; entre paquetes distintos, solo el identificador**, validado por el sistema.
7. **Eliminar según el documento:** desactivar, cancelar o borrar.
8. **Folios dados por un servicio del núcleo**, con series configurables por empresa y sucursal.

## Por qué

- El motor de documentos crea las tablas; lo que genere el primer día lo heredan todos los módulos. Cambiar una convención después exige migrar todas las tablas de todas las instalaciones.
- Singular coincide con el nombre del documento declarado y evita plurales irregulares.
- UUID permite importar datos de otro sistema y juntar instalaciones sin choques, y no revela volúmenes. El usuario ve el folio, no el identificador.
- Sin llaves foráneas entre paquetes, un módulo se instala y desinstala en cualquier orden. Es lo que hace real la independencia de los módulos.
- El prefijo `x_` en extensiones impide que una ocupe el nombre de un módulo oficial futuro.
- Un solo servicio de folios da el mismo comportamiento en todos los módulos y permite consecutivos sin huecos donde la ley lo pida.

## Alternativas descartadas

- **Esquema más prefijo en la tabla** (`itdesk.itd_ticket`): redundante.
- **Un solo esquema con prefijos**: contradice la decisión 0017.
- **Tablas en plural**: obliga a manejar plurales irregulares al generar nombres.
- **Identificador numérico consecutivo**: choca al juntar datos de dos instalaciones y revela volúmenes.
- **UUID más consecutivo interno**: duplica la complejidad del motor.
- **Llaves foráneas siempre**: ata los módulos entre sí; no se podría desinstalar uno mientras otro lo referencie.
- **Sin llaves foráneas nunca**: un error de programación deja registros apuntando a nada sin que la base avise.
- **Nunca borrar nada**: la base crece siempre y toda consulta debe filtrar eliminados.
- **Borrado real siempre**: inaceptable para documentos fiscales.
- **Folios por módulo**: trabajo repetido y comportamiento distinto en cada uno.

## Alcance

Todas las reglas del documento de convenciones (tipos de dato, nombres de restricciones e índices, migraciones, regla por omisión al eliminar, unicidad por empresa) quedaron autorizadas el 2026-11-27.
