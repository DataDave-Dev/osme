# OSME — arquitectura del ERP (diseño)

Fecha: 2026-11-27 · Estado: **aprobado por DataDave-Dev el 2026-11-27** · Nombre: **OSME** (Open Source Modular Enterprise)

Marcas: **[D]** decidido por DataDave-Dev · **[P]** propuesta pendiente de confirmar, corregible.

El porqué de cada decisión y lo que se descartó está en [decisiones](../decisiones/README.md), una por archivo. Este documento describe cómo queda el sistema.

## 1. Intención

ERP de código abierto, completo, dividido en módulos que se instalan por separado
(como CONTPAQi vende Contabilidad, Nóminas y Comercial aparte, o como SAP activa módulos). **[D]**

Dos principios rectores **[D]**:

1. **Módulos aparte.** Cada módulo funciona instalado solo y se integra con los demás cuando están presentes. Razón de fondo: una empresa que ya tiene otro ERP (SAP, CONTPAQi) y le falta un módulo puede usar solo ese módulo de este proyecto.
2. **El sistema se adapta al cliente, no el cliente al sistema.** Módulos profundos y muy configurables, no versiones a medias.

## 2. Metas, no-metas, anti-metas

**Metas**

- Global y multi-país: núcleo fiscalmente neutro; lo fiscal de cada país va en un paquete de localización. México es la primera. **[D]**
- Núcleo chico compartido + módulos instalables. **[D]**
- Adaptación en dos capas: configuración desde pantalla para lo común, extensiones de código instalables para lo raro. **[D]**
- Mismo código en servidor propio o en nube contratada por el cliente; multi-empresa. **[D]**
- Tienda de extensiones: cada módulo tiene su menú donde se descargan extensiones, muchas de pago; es la vía para lucrar. **[D]**
- Instalación en servidor propio por un usuario no técnico en Windows (instalador de doble clic, como CONTPAQi). **[D]**
- Convivir con sistemas ajenos: cada módulo trae API pública documentada, avisos de eventos (webhooks) e importar/exportar Excel/CSV. Los conectores para sistemas concretos (CONTPAQi, SAP, ...) van como extensiones en la tienda. **[D]**
- Celular: la misma interfaz web se adapta a pantalla chica; sin app nativa. **[D]**
- Proyecto de comunidad abierta desde el inicio: guías de contribución y reglas claras desde el día 1. **[D]**
- Licencia LGPL para núcleo y módulos oficiales; las extensiones pueden ser cerradas y de pago. **[D]**
- Código en inglés (`Invoice`, `JournalEntry`); documentación en español e inglés; pantallas traducibles. **[D]** (Los nombres en español de este documento son ilustrativos.)

**Qué lo distingue de Odoo y ERPNext [D]** — guía de prioridades:

1. Instalación sencilla: doble clic y funciona, sin consultor.
2. Localización profunda: lo fiscal de México y Latinoamérica completo y al día.
3. Ligero y rápido: corre bien en equipos modestos.
4. Sin edición de pago: todos los módulos oficiales son libres y completos; el negocio está en extensiones y servicios.

**No-metas** (en este documento)

- Lista final de módulos, orden de fases y flujo de la fase 1: se definen después, por decisión de DataDave-Dev.
- Constructor de aplicaciones sin código (se descartó "todo desde pantalla"). **[D]**
- Microservicios o un programa/base por módulo (se descartó el estilo "programas separados"). **[D]**
- Ser una variante de Odoo/ERPNext. **[D]**

**Anti-metas** (sería un fracaso aunque funcione)

- Adaptar a un cliente exige modificar el código de un módulo original.
- Una actualización de versión borra o rompe la configuración o las extensiones del cliente.
- Conceptos de un país (RFC, CFDI, SAT) dentro del núcleo o de un módulo genérico.
- Un módulo lee o escribe directamente las tablas de otro.
- Módulos "lite": pocas opciones, flujo único rígido.

## 3. Enfoques considerados

| | Enfoque | A favor | En contra |
|---|---|---|---|
| A | Módulos a mano: tablas, pantallas y API escritas por módulo | Primera función de negocio rápido | Lo configurable se rehace en cada módulo y queda desigual |
| **B** | **Documentos declarados**: cada documento se declara una vez y de ahí salen tabla, formulario, API, permisos | Adaptabilidad pareja en todos los módulos; campos del cliente usan el mismo mecanismo | Semanas de base antes de la primera función de negocio |
| C | Montarse en ERPNext/Odoo Community | Lo más rápido | Python, sus reglas y licencia; sería variante, no sistema propio |

**Elegido: B [D]**, porque es el único donde los dos principios salen de la estructura y no de disciplina.

## 4. Estructura

### 4.0 Stack

Requisitos **[D]**: corre en Windows, Linux y Mac sin problema; rápido; interfaz moderna; bajo consumo de recursos.

| Pieza | Elección | |
|---|---|---|
| Servidor | **.NET (C#)**, ejecutable autocontenido por sistema operativo | **[D]** |
| Base de datos | Postgres | **[D]** |
| Cliente | Navegador; sin instalar nada en la máquina del usuario | **[P]** |
| Interfaz | React + TypeScript + Vite, servida por el mismo servidor | **[P]** |

Por qué este stack y qué se descartó: [0004 stack](../decisiones/0004-stack-dotnet-react.md) y [0001 base de datos](../decisiones/0001-base-de-datos.md).

Consecuencias de .NET **[P]**:

- Un solo proceso desplegable (monolito modular). Cada paquete es un ensamblado que el servidor carga al arrancar o al instalarlo.
- Para no escribir cada documento dos veces (C# y TypeScript), la declaración vive solo en el servidor, que la publica como metadatos; la interfaz es un dibujador genérico de formularios y listas a partir de esos metadatos. Los campos personalizados del cliente llegan por la misma vía.
- Un paquete que necesite pantallas propias incluye además un paquete de interfaz que el navegador carga bajo demanda.
- Ventana de escritorio propia (Tauri): posible después, no ahora.

```
osme/
├── src/
│   ├── Core/            núcleo
│   ├── Modules/         Contabilidad, Ventas, Compras, Inventario, ...
│   ├── Localizations/   Mx, ...
│   ├── Host/            servidor que carga núcleo + paquetes instalados
│   └── web/             interfaz (React)
└── documents/           esta documentación
```

**Repositorios [D]**

Un solo repositorio (`osme`) para núcleo, módulos oficiales, localizaciones oficiales e interfaz. "Módulos aparte" se refiere a cómo se instalan y entregan, no a dónde vive el código: cada módulo es su propia carpeta y se publica como paquete independiente con su versión.

- Una verificación automática falla si un módulo usa código interno de otro; solo se permiten servicios públicos y eventos.
- Cada cambio corre las pruebas de todos los módulos.
- En repositorios aparte: las extensiones de terceros (cada autor el suyo) y la tienda (otro producto, maneja cobros). Instalador de Windows: repositorio aparte o carpeta, por decidir.
- Si un módulo llega a tener equipo y ritmo propios, se saca a su repositorio; como solo se comunica por servicios públicos, es mover una carpeta.

Tres tipos de paquete, mismo formato: **módulo**, **localización**, **extensión**.

### 4.1 Núcleo **[P]**

Solo lo que todo módulo necesita:

- **Motor de documentos**: recibe declaraciones y produce tabla, validación, API, formulario, lista, permisos y bitácora de cambios.
- **Registro de paquetes**: instalar, actualizar y desinstalar; resuelve dependencias y corre migraciones por paquete.
- **Organización**: empresas, sucursales, usuarios, roles y permisos.
- **Catálogos neutros**: monedas y tipos de cambio, países, idiomas, unidades de medida, terceros (una sola entidad para cliente/proveedor/empleado, con identificadores fiscales genéricos).
- **Configuración**: campos personalizados, flujos de estados y aprobaciones, series y folios, formatos de impresión, parámetros por empresa.
- **Eventos y puntos de extensión**: cómo se hablan los paquetes entre sí. Los eventos se guardan en una bitácora, no solo se disparan.
- **Cola de tareas**: para todo lo que llama a servicios externos o puede reintentarse.
- **Tienda y licencias**: catálogo de extensiones por módulo, descarga, instalación y verificación de compra.

El núcleo no contiene lógica contable, fiscal ni comercial.

**Usuarios [D]**: una sola tabla de usuarios, en el núcleo, compartida por todos los módulos. Una persona tiene una cuenta y una contraseña aunque use varios módulos. Cada módulo declara sus propios permisos y roles, y el administrador los asigna a la cuenta. Ningún módulo tiene usuarios propios. Como el núcleo se instala siempre junto con cualquier módulo, esto vale también para quien usa un solo módulo al lado de otro ERP.

Tres conceptos distintos: **usuario** (cuenta para entrar), **empleado** (persona de la empresa, en el directorio del núcleo; puede no tener usuario) y **cuenta de portal** (usuario ligero que solo ve el autoservicio).

### 4.2 Contrato de un paquete **[P]**

Cada paquete declara:

- **Manifiesto**: nombre, versión, dependencias obligatorias y opcionales.
- **Documentos**: campos, relaciones, estados, permisos.
- **Servicios públicos**: las únicas funciones que otros paquetes pueden llamar.
- **Eventos** que emite y a los que se suscribe.
- **Puntos de extensión** que ofrece (dónde otros pueden agregar o cambiar comportamiento).
- **Migraciones** y **pantallas** propias más allá de las generadas.

Reglas:

1. Cada paquete tiene su propio esquema de Postgres. Nadie toca tablas ajenas; solo servicios públicos y eventos. **[D]**
2. Las dependencias entre módulos de negocio son opcionales siempre que se pueda. Ejemplo: Ventas emite `factura.emitida`; si Contabilidad está instalada, se suscribe y genera la póliza; si no, Ventas funciona igual.
3. Eventos dentro del mismo proceso y la misma transacción: factura y póliza se guardan juntas o ninguna. Sin sincronización entre bases.
4. Como los eventos quedan guardados, un módulo instalado después puede procesar la historia desde la fecha que el cliente elija (ejemplo: Contabilidad instalada tras un año de facturas genera esas pólizas).
5. Los servicios externos no entran en la transacción. Ejemplo: la factura se guarda como "pendiente de timbrar" y una tarea la timbra con reintentos; el estado fiscal es un estado más del documento.

Reglas 4 y 5 aceptadas por DataDave-Dev. **[D]**

### 4.3 Adaptación al cliente

**Capa 1 — configuración desde pantalla** (se guarda como datos del cliente):
campos personalizados en cualquier documento, catálogos, flujos de estados y aprobaciones, series, formatos de impresión, reglas de generación de pólizas, parámetros por empresa.

Los campos personalizados se guardan en una columna JSONB del documento ("cajón flexible") **[P]** — DataDave-Dev pidió recomendación y esta es: simple y sin modificar la estructura de la base con usuarios trabajando. Límite conocido: reportes pesados sobre esos campos; ruta de mejora: promoverlos a columnas reales.

**Capa 2 — extensiones**: un paquete más, que depende del módulo que extiende. Puede agregar documentos y campos, suscribirse a eventos y usar puntos de extensión. Nunca edita el paquete original.

Como la configuración es dato y las extensiones son paquetes aparte, actualizar un módulo no las pisa.

**Tienda de extensiones [D]**: cada módulo muestra un menú con las extensiones disponibles para él, gratuitas o de pago, que se descargan e instalan desde ahí.

Publican el proyecto y terceros; toda extensión se revisa antes de aparecer y el proyecto cobra comisión sobre las de pago. **[D]**

Propuestas sobre la tienda **[P]**:

- Tras la revisión la extensión se firma, y la instalación solo acepta paquetes firmados. Así el código descargado se trata como de confianza y no hace falta aislarlo. Límite conocido: no escala a miles de autores; ruta de mejora: aislamiento de extensiones.
- Las extensiones de pago se validan con una licencia ligada a la instalación.
- Para que terceros publiquen, los servicios públicos y puntos de extensión de cada módulo son una interfaz versionada: no se rompen dentro de una versión mayor.

### 4.4 Localización

Paquete de país que aporta: impuestos, plantilla de catálogo de cuentas, documentos fiscales, facturación electrónica, reglas de nómina, reportes a la autoridad. Los módulos genéricos ofrecen puntos de extensión (por ejemplo "al emitir factura") y la localización los usa. `mx` aporta CFDI, timbrado y SAT.

### 4.5 Despliegue y aislamiento

- El proyecto no opera nube ni hospeda a nadie. Quien quiera usar el software lo instala donde decida: su oficina o una nube que contrate por su cuenta. **[D]**
- Por lo tanto una instalación = una organización = una base de datos Postgres. No hay varios clientes dentro de una instalación; quien quiera dar servicio a varios levanta una instalación por cada uno. **[P]**
- Única excepción: la tienda (catálogo, revisión, firma, cobro y licencias de extensiones) es un servicio central que opera el proyecto. **[D]**
- Dentro de una base, varias empresas separadas por `empresa_id`, aplicado por el motor de documentos y no por cada módulo. **[P]**
- Multi-empresa **[D]**: cada documento declara su alcance. Catálogos (terceros, productos) compartidos entre empresas por defecto, y configurables como exclusivos de una empresa. Contabilidad, folios e inventario siempre por empresa. Deja abierta la puerta a operaciones entre empresas y consolidación.
- Cada instalación descarga solo los paquetes que usa.
- **Instalador de Windows [D]**: doble clic, sin conocimientos técnicos. Empaqueta la aplicación, Postgres, el servicio de Windows y un actualizador. Es un subproyecto propio; el resto del diseño no depende de él, pero obliga a que instalar o actualizar un paquete no requiera línea de comandos.

### 4.6 Módulos oficiales **[D]**

Contabilidad · Ventas/Facturación · Compras · Inventario · Bancos/Tesorería · Nómina/RH · CRM · Producción · Activos fijos · Punto de venta · Proyectos/Obra · Mantenimiento · TI interno (tickets de sistemas, inventario de equipos) · Soporte a clientes (tickets de clientes).

Orden de construcción: ver [hoja de ruta](../hoja-de-ruta/hoja-de-ruta.md).

Paquetes base, chicos y fuera del núcleo: **Productos** (artículos, listas de precios) e **Impuestos** (motor de cálculo neutro; las tasas y reglas las pone la localización). Ventas, Compras e Inventario dependen de ellos, y así ninguno necesita a otro para funcionar solo.

### 4.7 Inteligencia artificial

OSME soporta integración con IA (ChatGPT, Claude, etc.) por API desde el inicio. **[D]**

Decidido **[D]**:

- **Paquete opcional**, no parte del núcleo. El ERP funciona completo sin IA y sin internet.
- **Tres usos**:
  1. Asistente dentro del ERP: chat que consulta y opera por el usuario.
  2. Funciones de IA en módulos: leer una factura en PDF y capturarla, sugerir cuenta contable, clasificar tickets, redactar cobranza.
  3. IA externa que opera el ERP: el usuario conecta su ChatGPT o Claude desde fuera.
- **Cada instalación pone su propia llave** del proveedor que elija y le paga directo. El proyecto no revende IA ni opera nada.
- **Qué puede hacer sola la IA es configurable por empresa**, por tipo de documento: qué requiere confirmación humana y qué no.

Propuestas **[P]**:

- El paquete de IA es neutral al proveedor: una sola interfaz interna y un adaptador por proveedor (OpenAI, Anthropic, modelos locales). Agregar otro proveedor es agregar un adaptador.
- La IA no ve tablas: usa lo mismo que el resto del sistema, las descripciones de documentos y los servicios públicos de cada módulo. Así un módulo o extensión nueva queda disponible para la IA sin trabajo extra.
- La IA actúa siempre con los permisos del usuario que la usa, nunca más.
- Valor de fábrica: toda acción que cambia datos pide confirmación, hasta que la empresa lo relaje.
- La bitácora marca qué hizo una IA y a petición de quién.
- Para el uso 3 (IA externa) se expone el estándar MCP además de la API pública.
- Aviso claro al activar: con un proveedor externo, los datos que la IA consulta salen de la instalación hacia ese proveedor. Con un modelo local no salen.
- Los módulos declaran sus funciones de IA como opcionales: si el paquete de IA no está instalado, esas funciones simplemente no aparecen.

## 5. Pendiente de DataDave-Dev

- Búsqueda formal de marca para OSME (IMPI, USPTO) y compra de dominio.
- Porcentaje de comisión de la tienda.
- Relación Ventas–Inventario, a definir bien al diseñar esos módulos: cómo se vende cuando Inventario no está instalado o no hay existencia (apartar al pedir o descontar al facturar, permitir o no venta sin existencia). DataDave-Dev lo marcó como punto delicado: no tiene sentido vender sin control de inventario.

## 6. Queda al constructor

- Forma exacta de las declaraciones de documentos.
- Cómo se generan formularios y listas.
- Librerías concretas (acceso a datos, autenticación, impresión/PDF, colas, componentes de interfaz).
- Mecánica de migraciones por paquete y por cliente.

## 7. Forma de trabajo

Scrum. Ver [proceso/scrum.md](../proceso/scrum.md).
