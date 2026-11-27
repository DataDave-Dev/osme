# OSME — instrucciones para Claude Code

Las instrucciones del proyecto están en `AGENTS.md` y aplican completas:

@AGENTS.md

## Lo que no se puede saltar

- **Prefijo de los changes de OpenSpec.** Toda carpeta en `openspec/changes/` se llama `c<FASE>-<SPRINT>-<CHANGE>-<nombre>` (ejemplo: `c0-1-1-un-ticket-de-punta-a-punta`). Al usar `/opsx:propose` o `openspec new change`, pasa siempre el nombre con su prefijo y su clave. No renumeres ni reutilices claves. Regla completa en `documents/proceso/openspec.md`.
- **Commits sin atribución de IA**, en formato `<tipo>: <descripción>`.
- **La IA no supone nada.** Toda decisión sale de un desarrollador: ante cualquier duda se pregunta antes de escribir, con opciones y recomendación, y la respuesta queda documentada. Ni "lo habitual" ni "lo más seguro" sustituyen una decisión. Regla completa en `AGENTS.md`, sección "La IA no supone nada".
