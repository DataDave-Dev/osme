# 0001 — PostgreSQL y no SQL Server

Fecha: 2026-11-27 · Estado: decidido por DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.0

## Decisión

OSME usa **PostgreSQL** como única base de datos. **SQL Server queda descartado** por ahora.

## Contexto

SQL Server era candidato serio: DataDave-Dev lo domina de años y es la base que el mercado mexicano conoce por CONTPAQi. Se comparó contra las decisiones ya tomadas para el proyecto, no en abstracto.

## Por qué se descartó SQL Server

Choca con cuatro decisiones del proyecto:

1. **Sin edición de pago.** SQL Server es propietario. Solo la edición Express es gratis y tiene topes (alrededor de 10 GB por base, 1 GB de memoria útil y 4 núcleos). Un ERP con años de facturas y bitácora de eventos alcanza ese tope, y entonces el usuario tendría que comprar licencia a Microsoft. OSME sería gratis pero con un costo oculto, justo lo que se quiere evitar.
2. **Corre en Windows, Linux y Mac sin problema.** SQL Server no tiene versión nativa para Mac; solo funciona dentro de Docker.
3. **Ligero y rápido.** SQL Server consume más memoria y disco. En Linux Microsoft pide 2 GB de memoria como mínimo; PostgreSQL arranca con decenas de MB.
4. **Open source (LGPL) y comunidad abierta.** Un ERP libre que exige una base propietaria no puede instalarse con solo software libre. Eso aleja contribuidores y distribuciones de Linux.

Además:

- **Instalador de doble clic.** PostgreSQL se empaqueta dentro del instalador sin pedir permiso a nadie y ocupa poco. SQL Server Express se puede incluir, pero es una instalación grande y lenta, sujeta a las condiciones de Microsoft.
- **Campos personalizados.** El diseño los guarda en un "cajón flexible" (JSONB), que es uno de los puntos más fuertes de PostgreSQL. SQL Server manejó JSON como texto durante años y su tipo nativo es reciente.

## Lo que se pierde al descartarlo

Se documenta para no engañarse:

- Herramientas de administración y reportes más pulidas e integradas (SSMS, reportes, tareas programadas).
- La experiencia de años de DataDave-Dev con SQL Server.
- Familiaridad del mercado: muchos técnicos de PyMEs mexicanas ya saben administrarlo.

## Comparativa

| Criterio | PostgreSQL | SQL Server |
|---|---|---|
| Licencia | Libre, sin límites | Propietaria; gratis solo Express, con topes |
| Costo al crecer | $0 | Licencia Standard, pagada por el usuario |
| Windows / Linux / Mac | Nativo en los tres | Windows y Linux; Mac solo con Docker |
| Consumo de recursos | Bajo | Mayor |
| Empaquetar en el instalador | Libre y ligero | Posible, pesado, con condiciones |
| Campos flexibles (JSON) | JSONB maduro, con índices | Soporte más limitado y reciente |
| Zona de tablas por módulo | Sí | Sí |
| Trabajo con .NET | Muy bueno | Excelente |
| Herramientas | Buenas, repartidas | Muy pulidas e integradas |
| Aceptación en comunidad open source | Estándar | Baja |

## Alternativas consideradas

- **SQL Server como única base.** Descartada por las razones de arriba.
- **Ambas desde el inicio.** Descartada: duplica pruebas y obliga a usar solo lo que las dos hacen igual, lo que impediría el cajón flexible. Demasiado costo para un proyecto que empieza con una persona.
- **PostgreSQL ahora, SQL Server después.** No se adopta como compromiso, pero la puerta queda abierta (ver abajo).

## Cuándo revisar esta decisión

Si la comunidad o clientes grandes piden SQL Server con insistencia. Es viable agregarlo después porque los módulos no escriben SQL a mano: todo pasa por el motor de documentos, así que el soporte de otra base se concentra en el núcleo.

## Nota sobre las cifras

Los topes de Express y los mínimos de memoria son los de las ediciones recientes según memoria de quien redactó; Microsoft anunció subir el tope de Express a 50 GB en la versión 2025. Confirmar en la documentación oficial de Microsoft antes de citarlas hacia afuera. La decisión no depende de la cifra exacta: aunque el tope suba, sigue habiendo tope, licencia propietaria y falta de versión nativa para Mac.
