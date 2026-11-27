# OSME — instrucciones para Claude Code

Las instrucciones del proyecto están en `AGENTS.md` y aplican completas:

@AGENTS.md

## Lo que no se puede saltar

- **Prefijo de los changes de OpenSpec.** Toda carpeta en `openspec/changes/` se llama `c<FASE>-<SPRINT>-<CHANGE>-<nombre>` (ejemplo: `c0-1-1-un-ticket-de-punta-a-punta`). Al usar `/opsx:propose` o `openspec new change`, pasa siempre el nombre con su prefijo y su clave. No renumeres ni reutilices claves. Regla completa en `documents/proceso/openspec.md`.
- **Commits sin atribución de IA**, en formato `<tipo>: <descripción>`.
- **Dudas y decisiones se preguntan**, no se suponen.
