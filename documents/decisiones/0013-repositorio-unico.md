# 0013 — Un solo repositorio

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.0

## Decisión

Núcleo, módulos oficiales, localizaciones oficiales e interfaz viven en un solo repositorio. Cada módulo es su propia carpeta y se publica como paquete independiente con su versión.

## Por qué

- «Módulos aparte» se refiere a cómo se instalan y entregan, no a dónde vive el código.
- Los cambios que cruzan módulos van juntos y cada cambio corre las pruebas de todos.
- Un lugar para clonar, reportar y llevar el tablero de Scrum.
- Una verificación automática falla si un módulo usa código interno de otro.
- En repositorios aparte: extensiones de terceros y la tienda. Si un módulo llega a tener equipo propio, se saca a su repositorio.

## Alternativas descartadas

- **Un repositorio por módulo**: coordinar cambios entre 15 o más repositorios es trabajo que no produce nada para un equipo de una persona.
