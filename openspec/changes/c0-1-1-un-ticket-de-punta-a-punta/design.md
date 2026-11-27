## Context

Primer código de OSME. No hay nada existente que respetar salvo `documents/`. Lo que aquí se elija sobre cómo se declara un documento y cómo se carga un paquete lo heredan todos los módulos, así que importa más que su tamaño.

Nada de este documento está **verificado ejecutándolo**: todo es **supuesto** hasta que el change corra. Donde una decisión dependa de una comprobación, se dice.

## Goals / Non-Goals

**Goals**

- Probar de punta a punta que declaración → tabla + API + pantalla funciona.
- Probar que un módulo vive en su propio esquema y que la frontera se puede verificar con una prueba.
- Dejar convenciones mínimas que no haya que deshacer en el siguiente change.

**Non-Goals**

- Un motor de documentos completo. Solo los tipos de campo que el ticket mínimo usa.
- Rendimiento, caché, paginación avanzada.
- Seguridad de acceso: no hay usuarios todavía (ver decisión 5).

## Decisions

Las ocho decisiones fueron autorizadas por DataDave-Dev el 2026-11-27.

### 1. La declaración de un documento es una clase C# con atributos

Un documento se declara como una clase con propiedades anotadas (etiqueta, obligatorio, opciones). El núcleo la lee por reflexión al cargar el paquete.

- **Descartado: declaración en archivo (JSON/YAML).** Se parece a lo que el cliente guardará después como campo personalizado, pero pierde el chequeo del compilador y el autocompletado para quien escribe módulos.
- **Descartado: API fluida de construcción.** Más flexible, más código por documento.
- La declaración se traduce a un **modelo interno de descripción** (lista de campos con tipo y reglas). Ese modelo, no la clase, es lo que usan el generador de tablas, la validación y la API. Así los campos personalizados de la entrega 0.5 entran al mismo modelo sin tocar clases.

### 2. El motor escribe su propio SQL sobre Npgsql; sin ORM por documento

El núcleo genera `CREATE TABLE`, `INSERT` y `SELECT` a partir del modelo de descripción, siempre con parámetros.

- **Descartado: Entity Framework Core con una entidad por documento.** Es lo habitual en .NET, pero su modelo se fija al compilar y los documentos de OSME cambian en ejecución (campos personalizados, extensiones instaladas). Se pelearía contra la herramienta.
- **Riesgo aceptado:** el motor carga con escribir SQL correcto. Mitigación: pocas operaciones, todas pasan por un solo generador, con pruebas contra Postgres real.
- `// ponytail:` en este change no hay filtros, orden configurable ni paginación; la lista trae las filas más recientes con un tope fijo. Se amplía cuando una pantalla real lo pida.

### 3. Migración mínima: crear lo que falta, nunca borrar

Las tablas siguen `documents/arquitectura/base-de-datos.md`: esquema del paquete sin prefijo en la tabla, nombre en singular, `id` UUID versión 7 y las columnas fijas. En este change `company_id`, `created_by` y `updated_by` existen pero quedan vacías y sin llave foránea, porque `core.company` y `core.user_account` nacen en el change `c0-2-1-entrar-al-sistema`.

Al cargar un paquete, el núcleo crea su esquema y sus tablas si no existen, y agrega las columnas que falten. No borra ni cambia columnas existentes.

- **Descartado: migraciones versionadas por paquete desde ya.** Hacen falta (arquitectura 4.2) pero aún no hay nada que migrar; se introducen cuando un cambio real de estructura lo exija.
- Consecuencia conocida: renombrar o quitar un campo de una declaración no se refleja en la base. Es aceptable mientras no haya datos de nadie.

### 4. Cada paquete es un ensamblado con un manifiesto; se cargan desde carpeta

El Host busca ensamblados de paquetes en una carpeta configurada, lee su manifiesto (nombre, versión, dependencias) y los carga en orden de dependencias.

- **Descartado: referenciar los módulos directo desde el Host.** Es más simple hoy, pero entonces el Host conoce a los módulos y "instalar un módulo" exigiría recompilar. Contradice `documents/decisiones/0002`.
- Cada paquete se carga en su propio contexto de carga de ensamblados, que es lo que después permite instalar y actualizar desde archivo. **Supuesto, a verificar en este change:** que la reflexión sobre atributos funciona entre contextos sin tropiezos.

### 5. Sin acceso todavía: el servidor escucha solo en la propia máquina

Hasta que exista inicio de sesión, el servidor acepta conexiones únicamente desde `localhost`.

- **Descartado: un usuario fijo de desarrollo.** Deja una puerta que hay que acordarse de quitar.
- Es una limitación deliberada y temporal; el change `c0-2-1-entrar-al-sistema` la retira.

### 6. API uniforme por documento

```
GET  /api/meta/{paquete}.{documento}        descripción del documento
GET  /api/docs/{paquete}.{documento}        lista
GET  /api/docs/{paquete}.{documento}/{id}   uno
POST /api/docs/{paquete}.{documento}        alta
```

Mismas rutas para cualquier documento de cualquier paquete. Los identificadores son UUID versión 7 generados por el servidor.

- **Descartado: rutas escritas por cada módulo.** Es justo el trabajo repetido que el enfoque de documentos declarados evita.

### 7. La interfaz dibuja a partir de la descripción

`src/web` tiene un componente por tipo de campo y dos pantallas genéricas: lista y formulario. Piden la descripción a `/api/meta/...` y arman la pantalla.

- Tipos en este change: texto, texto largo, opción de una lista, fecha y hora (solo lectura).
- El servidor sirve la interfaz compilada; en desarrollo, Vite hace de intermediario hacia el servidor.

### 8. La frontera entre paquetes es una prueba

Una prueba de arquitectura recorre los ensamblados y falla si un módulo referencia tipos no públicos de otro paquete, o cualquier tipo de otro módulo que no esté en su espacio de contratos.

- Se agrega antes de que exista un segundo módulo, a propósito: la frontera se pone antes que el código que la respeta.

## Risks / Trade-offs

- **El modelo de descripción se queda corto** para documentos reales (renglones de detalle, relaciones). → Este change no los necesita; el primero que los necesite es Inventario. Se revisa entonces, con dos módulos reales a la vista.
- **SQL propio** (decisión 2) → pruebas contra Postgres real, no simulaciones.
- **Carga de ensamblados en contextos separados** (decisión 4) puede complicar tipos compartidos entre núcleo y paquete. → Es lo primero que se prueba; si falla, se cae a un solo contexto y se anota como deuda para el change del instalador.
- **Pantalla genérica que se siente "generada"** (anti-meta de la fase). → Fuera de alcance afinarla aquí; se deja una lista y un formulario limpios y se evalúa con el ticket completo.
