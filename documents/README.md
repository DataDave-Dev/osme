# OSME — documentación

**OSME** (Open Source Modular Enterprise): ERP de código abierto, completo y modular. Cada módulo se instala por separado y el sistema se adapta a la empresa, no al revés.

## Por dónde empezar

1. [Arquitectura](arquitectura/arquitectura.md) — cómo está armado el sistema. Aprobada.
2. [Hoja de ruta](hoja-de-ruta/hoja-de-ruta.md) — en qué orden se construye.
3. [Decisiones](decisiones/README.md) — por qué se eligió cada cosa y qué se descartó.

## Carpetas

| Carpeta | Contenido |
|---|---|
| [arquitectura/](arquitectura/arquitectura.md) | Diseño general del sistema y [convenciones de base de datos](arquitectura/base-de-datos.md) |
| [decisiones/](decisiones/README.md) | Una decisión por archivo, numeradas |
| [hoja-de-ruta/](hoja-de-ruta/hoja-de-ruta.md) | Fases y su orden |
| [fases/](fases/) | Diseño detallado de cada fase, una carpeta por fase |
| [proceso/](proceso/scrum.md) | Forma de trabajo: [Scrum](proceso/scrum.md) y [OpenSpec con la clave de los changes](proceso/openspec.md) |
| `../openspec/` | Backlog: un change por rebanada, con historias y criterios de aceptación |

## Fases

| Fase | Documento | Estado |
|---|---|---|
| 0 — Base + TI interno + instalador | [diseño](fases/fase-00-base-ti-interno/diseno.md) | Aprobado |
| 0 · entrega 0.1 | [backlog](fases/fase-00-base-ti-interno/backlog-0.1.md) | Change 1 listo para sprint |

## Convenciones

- En los documentos de diseño, **[D]** marca una decisión de DataDave-Dev. Una propuesta nueva todavía sin autorizar se marca **[P]** hasta que se confirme. Hoy no hay ninguna pendiente.
- La documentación está en español; la versión en inglés está pendiente.
- El código irá en inglés.
