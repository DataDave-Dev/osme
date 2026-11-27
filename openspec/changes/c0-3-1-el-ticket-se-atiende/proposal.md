> **Clave 0-3-1** · fase 0 · sprint 3 · change 1

## Why

Un ticket que solo se levanta no sirve a un área de sistemas: hay que tomarlo, moverlo, contestarlo y cerrarlo. Además es la primera vez que el motor de documentos tiene que **modificar** un registro y guardar quién cambió qué, piezas que todo módulo posterior necesita.

Tercer change de la entrega 0.1 (`documents/fases/fase-00-base-ti-interno/diseno.md`, secciones 2.1 y 6).

## What Changes

- **El motor de documentos permite editar** un registro, con control de ediciones simultáneas.
- **Bitácora de cambios**: cada alta y cada edición queda registrada con quién, cuándo y qué cambió.
- **Estados del ticket**: abierto, en proceso, resuelto, cerrado, con las transiciones permitidas.
- **Asignar** un ticket a un técnico.
- **Comentarios** en el ticket, en orden cronológico.
- **Adjuntos** en el ticket y en los comentarios.
- **Pantalla de detalle** del ticket: sus datos, su estado, su conversación y su historial.

## Historias

1. Como técnico, quiero tomar un ticket o asignarlo a un compañero, para que se sepa quién lo atiende.
2. Como técnico, quiero cambiar el estado del ticket conforme avanza, para que el solicitante y el equipo sepan en qué va.
3. Como técnico o solicitante, quiero comentar en el ticket, para llevar la conversación en un solo lugar.
4. Como solicitante, quiero adjuntar una foto o un archivo, para mostrar el problema.
5. Como técnico, quiero cerrar el ticket indicando la solución, para que quede registro.
6. Como responsable del área, quiero ver el historial de un ticket, para saber quién hizo qué y cuándo.
7. Como dos técnicos que abren el mismo ticket, queremos que el sistema nos avise si el otro lo cambió, para no pisarnos.

## Fuera de alcance

| Qué | A dónde va |
|---|---|
| Categorías, prioridades con asignación automática | entrega 0.2 |
| Avisos al asignar o comentar | entrega 0.3 |
| Tickets por correo | entrega 0.3 |
| Estados y transiciones configurables por pantalla | entrega 0.5 |
| Tiempos de atención | entrega 0.5 |

Los estados de este change son fijos, declarados por el módulo. Volverlos configurables es trabajo de la entrega 0.5 y no debe exigir rehacer lo de aquí.

## Preguntas abiertas

- **Reabrir un ticket cerrado.** Asumido: se puede reabrir un ticket resuelto; uno cerrado no, se levanta otro.
- **Dónde se guardan los adjuntos.** Asumido: en disco, en la carpeta de datos de la instalación, no en la base. Se decide en `design.md`.
- **Tamaño y tipos de archivo permitidos.** Asumido: tope por archivo y lista de tipos permitidos, ambos fijos en este change.

## Capabilities

### New Capabilities

- `bitacora-de-cambios`: qué se registra en cada alta y edición, y quién puede consultarlo.
- `estados-de-documento`: cómo un documento declara sus estados y transiciones y cómo el núcleo las hace cumplir.
- `adjuntos`: subir, descargar y limitar archivos ligados a un documento.

### Modified Capabilities

- `motor-de-documentos`: edición de registros y control de ediciones simultáneas.
- `pantallas-genericas`: pantalla de detalle y edición.
- `tickets-de-ti`: estados, asignación, comentarios, adjuntos y cierre.

## Impact

- Amplía el motor de documentos y la interfaz genérica; todo módulo futuro hereda edición, bitácora, estados y adjuntos.
- Manejo de archivos subidos por usuarios: requiere revisión de seguridad antes de archivar.
