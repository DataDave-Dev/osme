# 0007 — El proyecto no opera nube

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.5

## Decisión

El proyecto no hospeda a nadie. Quien quiera usar OSME lo instala donde decida: su oficina o una nube que contrate por su cuenta. Una instalación es una organización con una base de datos. La única excepción es la tienda de extensiones, que opera el proyecto.

## Por qué

- Decisión directa de DataDave-Dev: «nosotros no manejaremos nada».
- Simplifica el diseño: no hay varios clientes dentro de una instalación.
- El instalador de Windows debe ser de doble clic para un usuario no técnico, como CONTPAQi; es el diferenciador número 1. Para Linux y Mac, paquete de servidor sencillo.
- De fábrica funciona en la red local con conexión cifrada; el acceso desde internet es opcional.

## Alternativas descartadas

- **Nube operada por el proyecto para muchos clientes**: exigiría aislar clientes y operar servicio.
- **Solo nube**: choca con open source instalable por cualquiera.
