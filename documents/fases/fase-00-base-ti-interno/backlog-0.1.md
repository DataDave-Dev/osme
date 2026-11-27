# Fase 0 · backlog de la entrega 0.1 — esqueleto que camina

Fecha: 2026-11-27 · Relacionado: [diseño de la fase 0](diseno.md), [Scrum](../../proceso/scrum.md)

## Meta de la entrega

Alguien instala OSME en Windows con doble clic, crea su empresa y su cuenta, entra y atiende un ticket de sistemas de principio a fin, en español o inglés, desde computadora o celular.

## Cómo se lleva

El backlog vive en OpenSpec, en `openspec/changes/`. Cada change es una rebanada vertical que funciona de punta a punta y mide más o menos un sprint.

| Scrum | OpenSpec |
|---|---|
| Elemento del backlog | Un change |
| Por qué, qué entra y qué no | `proposal.md` |
| Historia de usuario | Un Requirement en `specs/` |
| Criterios de aceptación | Sus Scenarios |
| Decisiones técnicas | `design.md` |
| Tareas del sprint | `tasks.md` |
| Terminado | `openspec archive <change>` |

## Clave de cada change

Regla completa en [proceso/openspec.md](../../proceso/openspec.md). Cada change lleva una clave **FASE-SPRINT-CHANGE**. Ejemplo: `0-1-1` es fase 0, sprint 1, change 1.

- El sprint es aquel en que el change **empieza**, contado dentro de la fase. Si un change se alarga a otro sprint, conserva su clave: las claves no se renumeran, porque los documentos se citan por ellas.
- La carpeta antepone una `c` a la clave (`c0-1-1-...`) porque OpenSpec no acepta nombres que empiecen con número.
- Hoy está planeado un change por sprint. Si en un sprint caben dos, el segundo será `0-1-2`.

## Orden

| Clave | Change | Qué funciona al terminar | Estado |
|---|---|---|---|
| 0-1-1 | `c0-1-1-un-ticket-de-punta-a-punta` | El servidor carga núcleo y TI interno; se levanta un ticket desde el navegador y queda guardado | Terminado y archivado el 2026-11-27 |
| 0-2-1 | `c0-2-1-entrar-al-sistema` | Primer arranque crea empresa y administrador; inicio de sesión; roles editables y permisos por módulo; se abre a la red local | Diseño, specs y tareas listos el 2026-11-27, sin preguntas abiertas; código sin empezar |
| 0-3-1 | `c0-3-1-el-ticket-se-atiende` | Asignar, cambiar estado, comentar, adjuntar, cerrar; bitácora de cambios | Propuesta con historias |
| 0-4-1 | `c0-4-1-dos-idiomas-y-celular` | Todo en español e inglés; interfaz adaptable a celular | Propuesta con historias |
| 0-5-1 | `c0-5-1-se-instala-con-doble-clic` | Instalador de Windows; paquetes instalables desde archivo | Propuesta con historias |

Los changes 0-2-1 a 0-5-1 se detallan (design, specs, tasks) al acercarse su sprint, con lo aprendido en los anteriores.

## Estimación

Sin estimar todavía. El change 0-1-1 tiene 26 tareas. La velocidad real se conoce al cerrar el primer sprint; con ese dato se estiman los demás. Capacidad: una persona a tiempo parcial.

## Riesgos de la entrega

- **Primer contacto con .NET.** El SDK no está instalado y es lenguaje nuevo para DataDave-Dev. El change 1 absorbe esa curva.
- **Carga de paquetes en contextos separados.** Supuesto sin verificar; es de lo primero que prueba el change 1.
- **El instalador se prueba en Windows** y el entorno de desarrollo es Linux. Hace falta una máquina o máquina virtual de Windows limpia para el change 5.
- **Tres changes tocan seguridad** (acceso, archivos subidos, instalar código): cada uno pide revisión de seguridad antes de archivarse.

## Comandos

```bash
openspec list                                   # changes activos
openspec show c0-1-1-un-ticket-de-punta-a-punta        # ver un change
openspec validate c0-1-1-un-ticket-de-punta-a-punta    # validar
openspec status --change <nombre>               # qué artefactos faltan
```
