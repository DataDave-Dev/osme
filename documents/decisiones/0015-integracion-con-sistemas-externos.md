# 0015 — Integración con sistemas externos

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 2

## Decisión

Cada módulo trae API pública documentada, avisos de eventos (webhooks) e importar/exportar Excel/CSV. Los conectores para sistemas concretos (CONTPAQi, SAP y otros) van como extensiones.

## Por qué

- Es consecuencia de la razón de ser de los módulos sueltos: convivir con el ERP que la empresa ya tiene.
- Para un módulo, un movimiento es el mismo venga de otro módulo de OSME, de la API o de un archivo.

## Alternativas descartadas

- **Solo API y archivos, sin conectores**: deja todo el trabajo de conexión a quien implementa.
- **Conectores oficiales dentro de los módulos**: mucho mantenimiento: cada sistema externo cambia por su cuenta.
