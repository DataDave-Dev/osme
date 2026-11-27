# 0009 — Tienda de extensiones

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.3

## Decisión

Cada módulo tiene un menú donde se descargan extensiones, gratuitas o de pago. Publican el proyecto y terceros; toda extensión se revisa antes de aparecer y el proyecto cobra comisión sobre las de pago. La tienda es un servicio central operado por el proyecto y se construye al final de todo (fase 15).

## Por qué

- Es la vía de ingreso del proyecto: los módulos oficiales son libres y completos, sin edición de pago.
- Mientras no exista la tienda, las extensiones se instalan desde archivo; el formato de extensión existe desde la fase 0.
- Propuestas pendientes de confirmar: tras la revisión la extensión se firma y la instalación verifica la firma; las de pago se validan con licencia ligada a la instalación; los servicios públicos de cada módulo son una interfaz versionada.

## Alternativas descartadas

- **Solo el proyecto publica**: sin terceros no hay ecosistema.
- **Terceros sin revisión**: obligaría a aislar el código de las extensiones: mucho más trabajo.
- **Sin tienda central**: cada autor vende por su cuenta; sin comisión para el proyecto.
