# OSME — instrucciones para agentes

OSME (Open Source Modular Enterprise) es un ERP de código abierto, completo y modular. Estas instrucciones valen para cualquier agente de IA que trabaje en este repositorio.

## Antes de tocar nada

Lee, en este orden:

1. `documents/README.md` — índice.
2. `documents/arquitectura/arquitectura.md` — cómo está armado el sistema.
3. `documents/decisiones/` — por qué se eligió cada cosa.
4. `documents/proceso/openspec.md` — cómo se nombran y llevan los changes.
5. El change en curso, en `openspec/changes/`.

`documents/` es la fuente de verdad. Un change no reabre una decisión de `documents/decisiones/`; si hay que cambiarla, se escribe una decisión nueva que la reemplaza.

## La IA no supone nada (obligatorio)

Todo lo que se construye o se escribe sale de una decisión de un desarrollador. Un agente de IA propone y pregunta; no decide.

- **Ante cualquier duda, pregunta antes de escribir.** Alcance, comportamiento, nombres, reglas de negocio, valores (plazos, límites, tamaños), librerías, estructura de datos y seguridad: nada se da por sabido.
- **Pregunta con opciones.** Cada pregunta trae las alternativas, lo que implica cada una y cuál recomiendas. Recomendar no es decidir.
- **No llenes huecos con "lo habitual" ni con "lo más seguro".** Si `documents/` o el change en curso no lo dicen, falta una decisión: se pregunta.
- **Lo que `documents/` deja "al constructor" también se pregunta.** El agente trae la propuesta; el desarrollador la autoriza.
- **Nada de "asumido" en silencio.** Si un artefacto necesita un valor que nadie ha decidido, queda marcado como pregunta abierta, sin valor, hasta que un desarrollador responda.
- **Las preguntas van juntas y antes**, no repartidas a mitad del trabajo, y se hacen al terminar de leer, no después de escribir.
- **Cada decisión queda escrita** con quién la tomó y la fecha: en `documents/decisiones/` si vale para todo el proyecto, o en el `design.md` del change si solo vale ahí.
- **Una decisión tomada no se reabre** ni se reinterpreta. Si parece chocar con otra, se pregunta cuál manda.

## Clave y prefijo de los changes de OpenSpec (obligatorio)

Todo change lleva una clave **FASE-SPRINT-CHANGE** y su carpeta empieza con la letra `c` seguida de esa clave:

```
openspec/changes/c<FASE>-<SPRINT>-<CHANGE>-<nombre-en-kebab-case>/
openspec/changes/c0-1-1-un-ticket-de-punta-a-punta/
```

- **Nunca** crees un change sin ese prefijo. Créalo ya con el nombre completo: `openspec new change c0-6-1-nombre`.
- La `c` es obligatoria: OpenSpec rechaza nombres que empiezan con número.
- La clave se cita sin la `c` (`0-1-1`) y va en la primera línea de `proposal.md`: `> **Clave 0-1-1** · fase 0 · sprint 1 · change 1`.
- **No renumeres ni reutilices claves**, aunque el change se alargue o se cancele.
- Antes de asignar una clave, comprueba que no exista en `openspec/changes/` ni en `openspec/changes/archive/`.
- Si no sabes qué fase o sprint corresponde, pregunta; no inventes la clave.

Detalle completo: `documents/proceso/openspec.md`.

## Reglas del proyecto

- **Base de datos.** Toda tabla sigue `documents/arquitectura/base-de-datos.md`: el esquema es el prefijo (`itdesk.ticket`), tablas en singular, `id` UUID v7, columnas fijas, y sin llaves foráneas entre paquetes distintos. Las extensiones usan esquema `x_<autor>_<nombre>`.
- **Paquetes aparte.** Ningún módulo usa tablas ni código interno de otro paquete: solo servicios públicos y eventos. Cada paquete tiene su propio esquema de Postgres.
- **Documentos declarados.** Un documento se declara una vez en el servidor. No escribas tablas, rutas ni formularios a mano para algo que el motor de documentos ya genera.
- **Nada de un país en el núcleo** ni en módulos genéricos. Lo fiscal va en paquetes de localización.
- **Idioma.** Código en inglés. Specs y documentación en español por ahora; `SHALL`, `WHEN`, `THEN` y encabezados de delta en inglés. Ningún texto visible al usuario se escribe fijo en el código.
- **Stack.** Servidor .NET (C#), PostgreSQL, interfaz React + TypeScript + Vite. En la interfaz, solo `pnpm`.
- **Pruebas primero** y contra Postgres real para todo lo que toque la base.
- **Licencia.** LGPL versión 3 o posterior (`LGPL-3.0-or-later`); es el identificador para el aviso de licencia en archivos de código.
- **Seguridad.** Sin secretos en el código. SQL siempre con parámetros. Todo lo que entra por la API se valida.

## Git

- Mensajes de commit: `<tipo>: <descripción>` (feat, fix, refactor, docs, test, chore, perf, ci).
- **Sin firmas ni atribuciones de herramientas de IA** en commits ni en pull requests. No agregues líneas `Co-Authored-By` de asistentes.
- No hagas commit ni push si no se te pide.
