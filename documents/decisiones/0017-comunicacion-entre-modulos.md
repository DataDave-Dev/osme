# 0017 — Comunicación entre módulos: zonas de tablas, eventos y tareas

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.2

## Decisión

Cada paquete tiene su propio esquema de Postgres y ninguno toca tablas ajenas. Los módulos se hablan por servicios públicos (preguntar) y eventos (avisar), dentro del mismo proceso y la misma transacción. Los eventos se guardan en una bitácora. Los servicios externos, como el timbrado, quedan fuera de la transacción y se resuelven con una cola de tareas con reintentos.

## Por qué

- Zonas separadas hacen real la independencia: desinstalar un módulo es borrar su zona.
- Misma transacción: factura y póliza, o factura y salida de almacén, se guardan juntas o ninguna. Sin sincronización, sin descuadres.
- Bitácora de eventos: un módulo instalado después puede procesar la historia (Contabilidad instalada tras un año de facturas genera esas pólizas).
- Cola con reintentos: si el proveedor de timbrado falla, la factura queda pendiente de timbrar y nadie pierde su captura.
- Lo que varios módulos comparten vive en paquetes base fuera del núcleo: Productos e Impuestos.

## Alternativas descartadas

- **Tablas compartidas entre módulos**: más simple al inicio; con el tiempo los módulos se enredan y ya no se pueden separar.
- **Eventos sin guardar**: un módulo nuevo empezaría desde el día que se instala.
- **Timbrar dentro de la transacción**: si el proveedor falla, la factura no se guarda.
