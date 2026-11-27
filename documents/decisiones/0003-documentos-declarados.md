# 0003 — Documentos declarados

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 3

## Decisión

Cada documento (factura, ticket, póliza) se declara una sola vez: campos, estados, permisos y relaciones. De esa declaración salen tabla, validación, API, formulario, lista y bitácora.

## Por qué

- Es el único enfoque donde los dos principios del proyecto (módulos aparte y el sistema se adapta al cliente) salen de la estructura y no de disciplina.
- Un campo personalizado del cliente usa el mismo mecanismo que un campo original, así que la adaptabilidad es pareja en todos los módulos.
- Un módulo nuevo tiene pantallas funcionales desde que declara sus documentos.
- Costo aceptado: semanas de base antes de la primera función de negocio, y un dibujador genérico de pantallas que debe sentirse moderno. Las pantallas con forma propia (punto de venta, conciliación) se siguen escribiendo a mano.

## Alternativas descartadas

- **Módulos a mano**: tablas, pantallas y API escritas por módulo. Arranque rápido, pero lo configurable se rehace en cada módulo y queda desigual.
- **Montarse en ERPNext u Odoo Community**: lo más rápido, pero sería una variante de ellos, en Python y bajo sus reglas, no un sistema propio.
