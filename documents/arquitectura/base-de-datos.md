# OSME — convenciones de base de datos

Fecha: 2026-11-27 · Estado: **decidido**, todas las reglas autorizadas el 2026-11-27 · Relacionado: [arquitectura](arquitectura.md), [decisión 0019](../decisiones/0019-convenciones-de-base-de-datos.md)

Todo lo aquí escrito está decidido por DataDave-Dev. La marca **[D]** señala una decisión explícita; ya no quedan propuestas pendientes (autorizadas el 2026-11-27).

Estas reglas valen para el núcleo, los módulos oficiales, las localizaciones y las extensiones. Casi ninguna tabla se escribe a mano: el motor de documentos las crea a partir de la declaración, y es el motor quien aplica estas reglas. Un paquete que necesite una tabla propia fuera del motor las sigue igual.

## 1. Esquemas

Cada paquete tiene su propio esquema de PostgreSQL. **El esquema es el prefijo**: las tablas no repiten el nombre del módulo. **[D]**

| Tipo de paquete | Esquema | Ejemplos |
|---|---|---|
| Núcleo | `core` | `core.company` |
| Módulo oficial | nombre del módulo | `itdesk`, `inventory`, `sales`, `accounting` |
| Localización | `l10n_` + código de país ISO en minúsculas | `l10n_mx` |
| Extensión | `x_` + autor + `_` + nombre | `x_acme_whatsapp` |

- El prefijo `x_` impide que una extensión ocupe el nombre de un módulo oficial futuro o choque con otra extensión. **[D]**
- El esquema `public` no se usa. **[D]**
- El nombre del esquema se fija al crear el paquete y no cambia nunca. **[D]**

## 2. Nombres

- Inglés, minúsculas, palabras separadas por guion bajo (`snake_case`). **[D]**
- Tablas en **singular**, igual que el documento declarado: `Ticket` → `ticket`. **[D]**
- Máximo 63 caracteres (límite de PostgreSQL). **[D]**
- Sin palabras reservadas de SQL: `user_account`, no `user`; `sales_order`, no `order`. **[D]**
- Sin abreviaturas, salvo las de uso universal (`id`, `url`, `sku`). **[D]**
- Tabla hija de un documento: `<padre>_<hijo>`: `ticket_comment`, `sales_order_line`. **[D]**
- Tabla de relación muchos a muchos: `<a>_<b>` en orden alfabético: `asset_license`. **[D]**

```
core.company            itdesk.ticket             inventory.warehouse
core.branch             itdesk.ticket_comment     inventory.stock_move
core.user_account       itdesk.asset              l10n_mx.cfdi
```

## 3. Identificadores

- Toda tabla tiene una llave primaria `id` de tipo `uuid`, generada por el servidor como **UUID versión 7** (ordenable por fecha). **[D]**
- El identificador nunca se muestra al usuario como número de documento; para eso está el folio (sección 8). **[D]**
- Quien llama a la API no puede fijar el `id`. **[D]**

## 4. Columnas fijas

Toda tabla de documento lleva estas columnas, las declare o no el módulo. Las pone el motor. **[D]**

| Columna | Tipo | Para qué |
|---|---|---|
| `id` | `uuid` | Llave primaria |
| `company_id` | `uuid`, opcional | Empresa dueña del registro. Vacío en catálogos compartidos entre empresas |
| `created_at` | `timestamptz` | Cuándo se creó |
| `created_by` | `uuid` | Qué usuario lo creó |
| `updated_at` | `timestamptz` | Última modificación |
| `updated_by` | `uuid` | Qué usuario lo modificó |
| `version` | `integer` | Sube en cada edición; detecta dos ediciones simultáneas |
| `custom` | `jsonb` | Campos personalizados del cliente |

Columnas que el motor agrega solo si el documento las declara **[D]**:

| Columna | Cuándo |
|---|---|
| `is_active` | Catálogos que se desactivan en vez de borrarse |
| `status` | Documentos con estados |
| `doc_number` | Documentos con folio |

El detalle de cada cambio (qué campo, valor anterior y nuevo) no va en estas columnas: va en la bitácora de cambios del núcleo.

## 5. Columnas declaradas

| Qué | Regla | |
|---|---|---|
| Llave foránea | `<tabla_destino>_id`: `company_id`, `assignee_id` cuando el nombre de rol aclara | **[D]** |
| Sí/no | Prefijo `is_` o `has_`: `is_active`, `has_warranty` | **[D]** |
| Fecha y hora | Sufijo `_at`, tipo `timestamptz`, siempre en UTC. La zona horaria se aplica al mostrar | **[D]** |
| Solo fecha | Sufijo `_on`, tipo `date`: `due_on`, `warranty_ends_on` | **[D]** |
| Dinero | `numeric(19,4)`, nunca punto flotante, siempre junto a su `currency_code` | **[D]** |
| Cantidades | `numeric(19,6)` | **[D]** |
| Texto | `text`; la longitud máxima la valida el motor según la declaración | **[D]** |
| Opciones y estados | `text` con valores en minúsculas y guion bajo (`in_progress`), no tipos `enum` de PostgreSQL, porque el cliente podrá agregar estados | **[D]** |

## 6. Relaciones

- **Dentro del mismo paquete**: llave foránea real. **[D]**
- **Hacia el núcleo** (`core.company`, `core.user_account`, etc.): llave foránea real. El núcleo está siempre instalado. **[D]**
- **Entre paquetes distintos**: se guarda solo el identificador, **sin llave foránea**. Lo valida el sistema a través del servicio público del otro paquete. Así un módulo se instala y desinstala sin que la base lo impida. **[D]**

Una referencia entre paquetes se declara en el documento indicando a qué documento apunta (`products.item`), para que el motor sepa validarla y la pantalla sepa dibujar el buscador. Si el paquete destino no está instalado, el campo no se ofrece. **[D]**

Toda llave foránea lleva índice. **[D]**

## 7. Eliminar

Cada documento declara su regla. **[D]**

| Regla | Qué pasa | Para qué |
|---|---|---|
| Desactivar | `is_active` pasa a falso; el registro sigue y deja de ofrecerse | Catálogos: artículos, clientes, categorías |
| Cancelar | El estado pasa a cancelado; el registro nunca se borra | Documentos con valor legal o contable: facturas, pólizas |
| Borrar | Se elimina el renglón | Borradores y datos sin consecuencia |

En los tres casos la bitácora guarda quién y cuándo. Si un documento no declara regla, vale la más segura: desactivar. **[D]**

## 8. Folios

El número visible de un documento (`142`, `A-1057`) lo da un **servicio de folios del núcleo**, no cada módulo. **[D]**

- Series configurables por empresa y sucursal: prefijo, consecutivo, reinicio anual.
- El documento declara que usa folio; la empresa configura su serie.
- Consecutivos sin huecos donde la ley lo pida.
- Se guarda en la columna `doc_number`.

El servicio se construye en el primer change que lo necesite. **[D]**

## 9. Restricciones e índices **[D]**

| Qué | Nombre |
|---|---|
| Llave primaria | `pk_<tabla>` |
| Llave foránea | `fk_<tabla>__<columna>` |
| Único | `uq_<tabla>__<columnas>` |
| Comprobación | `ck_<tabla>__<regla>` |
| Índice | `ix_<tabla>__<columnas>` |

- En tablas por empresa, `company_id` va primero en los índices de búsqueda.
- La unicidad de un dato de negocio (un folio, un código) se define **por empresa**, no global.

## 10. Empresa **[D]**

Cada documento declara su alcance (ver [decisión 0008](../decisiones/0008-multi-empresa.md)):

- **Por empresa**: `company_id` obligatorio. El motor lo filtra en toda consulta; ningún módulo escribe ese filtro.
- **Compartido**: `company_id` vacío; visible para todas las empresas de la instalación.

## 11. Cambios de estructura **[D]**

- Cada paquete trae sus migraciones, numeradas, y solo tocan su propio esquema.
- El núcleo registra cuáles se aplicaron a cada paquete.
- Mientras no existan migraciones versionadas (ver change 0-1-1), el motor solo crea lo que falta y nunca borra ni renombra.
- Desinstalar un paquete elimina su esquema completo, previa confirmación y respaldo.

## 12. Ejemplo

Lo que el motor crea para el ticket mínimo del change 0-1-1:

```sql
CREATE SCHEMA IF NOT EXISTS itdesk;

CREATE TABLE itdesk.ticket (
    id          uuid        NOT NULL,
    company_id  uuid,
    title       text        NOT NULL,
    description text,
    priority    text        NOT NULL DEFAULT 'normal',
    created_at  timestamptz NOT NULL,
    created_by  uuid,
    updated_at  timestamptz NOT NULL,
    updated_by  uuid,
    version     integer     NOT NULL DEFAULT 1,
    custom      jsonb       NOT NULL DEFAULT '{}',
    CONSTRAINT pk_ticket PRIMARY KEY (id),
    CONSTRAINT ck_ticket__priority CHECK (priority IN ('low', 'normal', 'high'))
);
```

`company_id` y `created_by` quedan sin llave foránea ni obligatoriedad hasta que el change 0-2-1 cree `core.company` y `core.user_account`.
