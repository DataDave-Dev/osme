# Forma de trabajo: OpenSpec y la clave de los changes

Fecha: 2026-11-27 · Estado: decidido por DataDave-Dev · Relacionado: [Scrum](scrum.md)

El backlog de OSME vive en OpenSpec, en `openspec/changes/`. Cada change es una rebanada vertical: algo que funciona de punta a punta y mide más o menos un sprint.

## Clave: FASE-SPRINT-CHANGE

Todo change lleva una clave de tres números separados por guiones.

| Parte | Qué es | Cuenta desde |
|---|---|---|
| FASE | Número de la fase en la [hoja de ruta](../hoja-de-ruta/hoja-de-ruta.md) | 0 |
| SPRINT | Sprint en que el change **empieza**, contado dentro de esa fase | 1 |
| CHANGE | Número del change dentro de ese sprint | 1 |

Ejemplo: `0-1-1` es fase 0, sprint 1, change 1. El segundo change del mismo sprint sería `0-1-2`.

## Prefijo de la carpeta: `c`

La carpeta del change se llama:

```
c<FASE>-<SPRINT>-<CHANGE>-<nombre-en-kebab-case>
```

```
openspec/changes/c0-1-1-un-ticket-de-punta-a-punta/
openspec/changes/c0-2-1-entrar-al-sistema/
```

La `c` inicial es obligatoria. OpenSpec exige que el nombre de un change empiece con letra: sin ella `openspec new change` lo rechaza, y `openspec status` e `instructions` fallan aunque la carpeta exista. La `c` no forma parte de la clave; la clave se escribe y se cita sin ella (`0-1-1`).

## Reglas

1. **Ningún change sin clave.** No se crea una carpeta en `openspec/changes/` que no empiece con `c<FASE>-<SPRINT>-<CHANGE>-`.
2. **Crear con el nombre completo.** `openspec new change c0-6-1-nombre`, nunca renombrando después.
3. **Las claves no se renumeran ni se reutilizan.** Los documentos se citan entre sí por clave. Si un change se alarga a otro sprint, conserva la suya. Si se cancela, su clave queda quemada.
4. **La clave va también dentro del change**, en la primera línea de `proposal.md`:
   ```
   > **Clave 0-1-1** · fase 0 · sprint 1 · change 1
   ```
5. **El nombre después de la clave** va en kebab-case, en el idioma vigente de las specs (español por ahora, ver [decisión 0011](../decisiones/0011-comunidad-e-idiomas.md)), y describe lo que funciona al terminar, no la capa técnica que se toca.
6. **Al archivar**, OpenSpec antepone la fecha y la carpeta queda como `archive/AAAA-MM-DD-c0-1-1-nombre`. No se toca a mano.
7. **Las capacidades** en `openspec/specs/` no llevan clave: nombran un comportamiento que perdura, no un trabajo que termina.

## Cómo elegir la clave siguiente

1. FASE: la fase en curso según la hoja de ruta.
2. SPRINT: `openspec list` y el backlog de la entrega muestran el último sprint usado en esa fase. Si el change empieza en un sprint nuevo, es el siguiente número.
3. CHANGE: 1 si es el primero de ese sprint; si no, el siguiente libre.
4. Comprobar que la clave no existe ni en `openspec/changes/` ni en `openspec/changes/archive/`.

## Correspondencia con Scrum

| Scrum | OpenSpec |
|---|---|
| Elemento del backlog | Un change |
| Por qué, qué entra y qué no | `proposal.md` |
| Historia de usuario | Un Requirement en `specs/` |
| Criterios de aceptación | Sus Scenarios |
| Decisiones técnicas | `design.md` |
| Tareas del sprint | `tasks.md` |
| Terminado | `openspec archive <change>` |

Las reglas de redacción de cada artefacto están en `openspec/config.yaml`.
