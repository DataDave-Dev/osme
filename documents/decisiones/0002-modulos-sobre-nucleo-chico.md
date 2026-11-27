# 0002 — Núcleo chico + módulos instalables

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4

## Decisión

OSME es un núcleo mínimo compartido más módulos que se instalan por separado. Cada módulo tiene sus propias tablas y se comunica con los demás solo por contratos definidos (servicios públicos y eventos).

## Por qué

- La razón de fondo de DataDave-Dev: una empresa que ya tiene otro ERP (SAP, CONTPAQi) y le falta un módulo debe poder usar solo ese módulo de OSME.
- Un documento de un módulo puede provocar efectos en otro (una factura genera su póliza) sin sincronizar nada entre programas.
- Costo aceptado: todo módulo depende del núcleo, que se instala siempre.

## Alternativas descartadas

- **Programas separados, estilo CONTPAQi**: cada módulo con su propia base y despliegue. Máxima independencia, pero obliga a sincronizar datos entre aplicaciones, de donde vienen los descuadres.
- **Un solo sistema con módulos activables, estilo SAP/Odoo**: lo más simple de construir, pero los módulos quedan acoplados y no se pueden usar sueltos.
