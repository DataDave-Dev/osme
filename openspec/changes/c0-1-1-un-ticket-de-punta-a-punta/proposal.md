> **Clave 0-1-1** · fase 0 · sprint 1 · change 1

## Why

Hoy OSME es solo documentación. Las dos apuestas más grandes del diseño no se han probado corriendo: que un documento **declarado una vez** produzca tabla, API y pantalla (`documents/decisiones/0003-documentos-declarados.md`), y que un módulo viva **aparte del núcleo**, con su propia zona de tablas (`documents/decisiones/0002-modulos-sobre-nucleo-chico.md`, `0017-comunicacion-entre-modulos.md`).

Este change es la rebanada más delgada que atraviesa todo: servidor, núcleo, un módulo, base de datos y navegador. Si aquí algo no funciona, conviene saberlo antes de construir encima.

Es el primer change de la entrega 0.1 (`documents/fases/fase-00-base-ti-interno/diseno.md`, sección 6).

## What Changes

- **Nace la estructura del repositorio** de `documents/arquitectura/arquitectura.md` 4.0: `src/Core`, `src/Modules`, `src/Host`, `src/web`.
- **El servidor arranca y carga paquetes.** Al iniciar, el Host carga el núcleo y los paquetes presentes; cada paquete se presenta con un manifiesto (nombre, versión, dependencias).
- **Un paquete declara un documento y el núcleo hace el resto.** A partir de la declaración, el núcleo crea la tabla en el esquema del paquete, valida lo que se guarda y expone lectura y alta por API.
- **El servidor publica la descripción de cada documento** y la interfaz dibuja la lista y el formulario a partir de ella, sin conocer el documento.
- **Primer módulo: TI interno, con un solo documento, el ticket**, en su forma mínima: título, descripción y prioridad. Se levanta desde el navegador y aparece en la lista.
- **La frontera entre paquetes se verifica sola desde el primer día**: una prueba falla si un módulo usa código interno de otro o del núcleo.
- **Sin inicio de sesión todavía, y por eso el servidor solo atiende a la propia máquina.** No hay forma de alcanzarlo desde la red hasta que exista el acceso.

## Fuera de alcance

| Qué | A dónde va |
|---|---|
| Crear empresa, usuarios, inicio de sesión, permisos | change `c0-2-1-entrar-al-sistema` |
| Estados, asignar, comentar, cerrar, editar, bitácora de cambios | change `c0-3-1-el-ticket-se-atiende` |
| Español e inglés, pantalla adaptable a celular | change `c0-4-1-dos-idiomas-y-celular` |
| Instalador de Windows; instalar paquetes desde archivo | change `c0-5-1-se-instala-con-doble-clic` |
| Varias empresas, sucursales, empleados | entrega 0.2 |
| Campos personalizados, flujos, reglas | entrega 0.5 |
| Bitácora de eventos y cola de tareas | entrega 0.3, cuando el correo las necesite |

En este change los paquetes se cargan desde la carpeta de compilación. Instalarlos desde archivo llega con el instalador.

## Preguntas abiertas

- **Nombre en código del módulo TI interno.** Asumido: `ItDesk`, esquema `itdesk`. Cambiarlo después cuesta una migración.
- **Versión de .NET.** Asumida: la LTS vigente al iniciar el sprint. El SDK no está instalado en la máquina de desarrollo; es la primera tarea.

## Capabilities

### New Capabilities

- `carga-de-paquetes`: cómo el servidor descubre y carga el núcleo y los paquetes, qué declara cada uno y qué pasa cuando falta una dependencia.
- `motor-de-documentos`: qué produce el núcleo a partir de la declaración de un documento: tabla en el esquema del paquete, validación, API de lectura y alta, y la descripción que publica.
- `pantallas-genericas`: lista y formulario dibujados a partir de la descripción del documento.
- `tickets-de-ti`: el ticket de sistemas en su forma mínima.

### Modified Capabilities

Ninguna. `openspec/specs/` está vacío.

## Impact

- Primer código del repositorio. No hay nada que migrar.
- Requiere en la máquina de desarrollo: SDK de .NET (no instalado), Node y pnpm (instalados), PostgreSQL (instalado; también disponible por Docker).
- Fija convenciones que los changes siguientes heredan: forma de declarar un documento, forma del manifiesto, rutas de la API y nombre de los esquemas.
