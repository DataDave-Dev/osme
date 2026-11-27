# 0008 — Varias empresas por instalación, catálogos compartidos configurables

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.5

## Decisión

Una instalación maneja varias empresas. Clientes, proveedores y productos se comparten entre empresas por defecto, y cada catálogo puede marcarse como exclusivo de una. Contabilidad, folios e inventario son siempre por empresa.

## Por qué

- Los grupos de empresas no duplican catálogos.
- Deja abierta la puerta a operaciones entre empresas y consolidación.
- La separación por empresa la aplica el motor de documentos, no cada módulo.

## Alternativas descartadas

- **Todo separado por empresa, estilo CONTPAQi**: más simple, pero los grupos duplican clientes y productos y consolidar es difícil.
