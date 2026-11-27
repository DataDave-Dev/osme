## Context

Primer código de OSME. No hay nada existente que respetar salvo `documents/`. Lo que aquí se elija sobre cómo se declara un documento y cómo se carga un paquete lo heredan todos los módulos, así que importa más que su tamaño.

Estado al 2026-11-27, con el change corriendo. Se separa lo **verificado ejecutándolo** de lo que sigue siendo **supuesto**.

**Verificado ejecutándolo** (pruebas en `tests/Osme.Tests`, `src/web/src` y `src/web/e2e`, más el servidor levantado a mano):

- Decisión 1 y 7: de la declaración salen tabla, API y pantalla. Un campo temporal agregado al ticket apareció en formulario y lista sin tocar `src/web`.
- Decisión 2: SQL propio sobre Npgsql, con parámetros, contra PostgreSQL 17 real.
- Decisión 3: la segunda carga con un campo más conserva los registros y deja el campo vacío en los anteriores.
- Decisión 4: cada paquete en su propio contexto de carga. **El supuesto se cumplió:** la reflexión sobre atributos funciona entre contextos, a condición de que el paquete tome del Host los ensamblados que el Host ya tiene (el núcleo sobre todo) y no una copia propia.
- Decisión 5: el servidor escucha en `127.0.0.1` y `[::1]`; una petición a la IP de red de la misma máquina no conecta.
- Decisión 6: mismas rutas y misma forma de respuesta para todo documento.
- Decisión 8: la prueba de frontera pasa con ItDesk y falla, nombrando paquete y tipo, con un paquete de prueba que usa código no publicado del núcleo.

**Cambió respecto a lo escrito:**

- **Lo publicado por un paquete es su espacio de nombres `<Ensamblado>.Contracts`.** La decisión 8 hablaba de "tipos no públicos"; como el Host necesita usar el motor, esos tipos son públicos para el compilador y lo que la prueba revisa es el espacio de nombres.
- **Las opciones llevan etiqueta.** La descripción publica cada opción como valor y etiqueta (`low` / "Baja"); sin eso la pantalla mostraría el valor interno.
- **Los rechazos viajan como código, no como frase** (`required`, `too_long`, `invalid_option`, `unknown_field`, `invalid_type`). La interfaz los redacta, para que ningún texto visible quede fijo en el servidor.
- **Se agregó `GET /api/meta`** (lista de documentos disponibles), que la decisión 6 no tenía: la interfaz la necesita para saber qué ofrecer sin conocer ningún documento.
- **Se agregó una pantalla genérica de consulta de un registro**, que `specs/tickets-de-ti` pide ("abrir un ticket para ver todos sus datos") y la decisión 7 no nombraba.
- **Fecha y hora solo existe como campo del sistema** (`created_at`). Un paquete todavía no puede declarar un campo de ese tipo.
- **Un paquete omitido se descarga de memoria**, así que su contexto es recolectable.

**Sigue siendo supuesto:**

- Que el flujo de integración continua pasa en GitHub: está escrito y no se ha subido (tarea 7.3).
- Que el idioma de origen de los textos sea el español. Etiquetas y mensajes están en español en un solo lugar por lado (`Ticket.cs`, `src/web/src/messages.ts`); el change `c0-4-1` decide el mecanismo de traducción.
- Que la restricción `CHECK` de un campo de opción baste: se escribe una vez, al crear la columna. Cambiar las opciones de un campo ya creado necesitará migraciones versionadas.

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
