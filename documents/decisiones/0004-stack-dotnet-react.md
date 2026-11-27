# 0004 — Stack: .NET en servidor, React en navegador

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.0

## Decisión

Servidor en .NET (C#) como ejecutable autocontenido por sistema operativo. Cliente en navegador, sin instalar nada en la máquina del usuario. Interfaz en React + TypeScript + Vite, servida por el mismo servidor y adaptable a celular. Base de datos PostgreSQL (ver decisión 0001).

## Por qué

- Requisitos de DataDave-Dev: correr en Windows, Linux y Mac sin problema, ser rápido, interfaz moderna y bajo consumo de recursos.
- .NET es muy rápido, corre en los tres sistemas y soporta cargar extensiones como ensamblados.
- Para no declarar cada documento dos veces (C# y TypeScript), la declaración vive solo en el servidor, que la publica como metadatos; la interfaz dibuja formularios y listas a partir de ellos.
- Celular: la misma interfaz web adaptable, sin app nativa (decisión de DataDave-Dev).
- Costo aceptado: C# es lenguaje nuevo frente a los proyectos anteriores de DataDave-Dev, todos en TypeScript.

## Alternativas descartadas

- **TypeScript ligero en servidor**: era la recomendación inicial: extensiones en caliente y declaraciones compartidas salen naturales. DataDave-Dev eligió .NET.
- **Go**: el más ligero, pero un binario no carga código nuevo; las extensiones requerirían un motor aparte.
- **Rust**: el más rápido, pero demasiado lento de escribir para este alcance.
- **Next.js**: pensado para sitios públicos; compila todo de antemano y choca con extensiones instaladas en caliente.
- **Electron**: cada ventana es un navegador completo; contradice el bajo consumo.
