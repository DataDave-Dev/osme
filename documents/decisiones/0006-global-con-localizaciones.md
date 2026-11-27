# 0006 — Alcance global con localizaciones por país

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), secciones 2 y 4.4

## Decisión

OSME es global. El núcleo y los módulos genéricos son fiscalmente neutros; lo fiscal de cada país va en un paquete de localización. México es la primera localización.

## Por qué

- Los módulos genéricos ofrecen puntos de extensión y la localización los usa (por ejemplo, al emitir factura, México agrega CFDI y timbrado).
- Anti-meta explícita: conceptos de un país (RFC, CFDI, SAT) dentro del núcleo o de un módulo genérico.
- Diferenciador declarado por DataDave-Dev: localización profunda de México y Latinoamérica, completa y al día.

## Alternativas descartadas

- **Solo PyMEs mexicanas, con lo fiscal en el núcleo**: más directo, pero cierra el proyecto a un país.
- **Base propia para clientes actuales**: no era el objetivo.
- **Proyecto de aprendizaje**: no era el objetivo.
