# 0005 — Adaptación al cliente: configuración + extensiones

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.3

## Decisión

El sistema se adapta a la empresa en dos capas. Lo común se configura desde pantalla sin código (campos personalizados, catálogos, flujos de estados y aprobaciones, series, formatos, reglas). Lo raro se resuelve con extensiones de código que se instalan como un paquete más y nunca modifican el módulo original.

## Por qué

- Principio de DataDave-Dev: el sistema debe adaptarse al cliente, no el cliente al sistema; módulos completos, no a medias.
- Como la configuración es dato y las extensiones son paquetes aparte, actualizar un módulo no las pisa.
- Los campos personalizados se guardan en un cajón flexible (columna JSONB) por documento; DataDave-Dev pidió recomendación y fue esa. Agregar un campo es instantáneo y no modifica la estructura de la base con usuarios trabajando. Límite: reportes pesados sobre esos campos; los muy usados se promueven a columna real.

## Alternativas descartadas

- **Todo desde pantalla**: plataforma sin código donde un implementador arma todo. Máxima adaptabilidad, pero el núcleo se vuelve un constructor de aplicaciones: el proyecto más grande y lento.
- **Solo extensiones de código**: núcleo más simple, pero cada cliente distinto necesitaría un programador.
- **Campos personalizados como columnas reales**: reportes rápidos, pero modifica la base en plena operación.
