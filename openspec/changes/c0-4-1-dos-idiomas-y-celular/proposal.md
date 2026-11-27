> **Clave 0-4-1** · fase 0 · sprint 4 · change 1

## Why

`documents/decisiones/0011-comunidad-e-idiomas.md` pide español e inglés desde la primera versión, y `documents/decisiones/0004-stack-dotnet-react.md` que la misma interfaz se adapte a celular. Hacerlo ahora, con pocas pantallas, cuesta poco; hacerlo después de diez módulos significa cazar textos fijos por todo el código.

Cuarto change de la entrega 0.1 (`documents/fases/fase-00-base-ti-interno/diseno.md`, sección 6).

## What Changes

- **Todo texto visible pasa por traducción**: etiquetas de documentos y campos, opciones, mensajes de error y textos de la interfaz.
- **Cada paquete trae sus propias traducciones**, junto a sus documentos.
- **Español e inglés completos** para el núcleo y TI interno.
- **Cada usuario elige su idioma**; fechas y números se muestran según ese idioma.
- **Una verificación automática falla** si aparece un texto visible sin traducción.
- **La interfaz se adapta a pantalla de celular**: navegación, lista, detalle y formulario.

## Historias

1. Como usuario, quiero elegir español o inglés y ver todo el sistema en ese idioma, incluidos los mensajes de error.
2. Como usuario, quiero ver fechas y números con el formato de mi idioma.
3. Como autor de un módulo, quiero entregar las traducciones de mi módulo dentro de mi paquete, para no depender del núcleo.
4. Como mantenedor, quiero que la compilación falle si alguien agrega un texto sin traducir, para que no se acumulen.
5. Como técnico que anda por la oficina, quiero atender un ticket desde el celular, para no regresar a mi escritorio.

## Fuera de alcance

| Qué | A dónde va |
|---|---|
| Más idiomas que español e inglés | cuando la comunidad los aporte |
| Traducir desde pantalla (el cliente cambia etiquetas) | entrega 0.5, con los campos personalizados |
| Traducir el contenido capturado por usuarios | fuera de la fase 0 |
| Zonas horarias por sucursal | entrega 0.2, con sucursales |
| App nativa | fuera de alcance del proyecto por ahora |

## Preguntas abiertas

- **Idioma de fábrica de una instalación nueva.** Asumido: el del sistema operativo o navegador de quien hace el primer arranque, con español como respaldo.
- **Zona horaria.** Asumido: una por instalación en este change.

## Capabilities

### New Capabilities

- `traducciones`: cómo un paquete aporta sus textos, cómo se elige el idioma y qué pasa cuando falta una traducción.
- `interfaz-adaptable`: qué debe poder hacerse desde una pantalla de celular y con qué tamaños mínimos.

### Modified Capabilities

- `motor-de-documentos`: la descripción de un documento se entrega en el idioma de quien la pide.
- `pantallas-genericas`: textos traducidos y disposición adaptable.

## Impact

- Toca todos los textos escritos hasta ahora en núcleo, TI interno e interfaz.
- Agrega una verificación al flujo de integración continua.
- Pruebas visuales en anchos de celular, tableta y escritorio.
