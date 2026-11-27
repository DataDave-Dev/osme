# OSME — hoja de ruta (orden de construcción)

Fecha: 2026-11-27 · Estado: orden decidido por DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md)

Todo lo aquí escrito está decidido por DataDave-Dev. La marca **[D]** señala una decisión explícita; ya no quedan propuestas pendientes (autorizadas el 2026-11-27).

## Criterio

Orden **"operación primero" [D]**: sigue el camino de la mercancía. Inventario existe antes que Ventas, porque no tiene sentido vender sin control de inventario. Contabilidad llega después y genera las pólizas de lo anterior leyendo la bitácora de eventos.

Cada fase entrega algo que funciona de punta a punta y se puede instalar y usar, no una capa suelta.

Cada fase tendrá su propio diseño detallado antes de construirse. Se trabaja con Scrum en sprints de 2 semanas; cuántos sprints toma cada fase se estima al diseñarla.

## Fases

| # | Fase | Qué funciona de punta a punta al terminar |
|---|---|---|
| 0 | **Base + TI interno + instalador mínimo** **[D]** | Alguien instala OSME en Windows con doble clic, crea su empresa y usuarios, levanta y atiende tickets de sistemas y lleva inventario de equipos. Incluye núcleo, motor de documentos, pantallas genéricas, permisos, campos personalizados, instalación de paquetes y API pública |
| 1 | **Productos + Inventario** | Catálogo de artículos, almacenes, entradas, salidas, traspasos, existencias y costos. Movimientos también por API o archivo, para quien vende con otro sistema |
| 2 | **Compras** | Proveedor → orden de compra → recepción → entrada a inventario con costo |
| 3 | **Paquete de IA** **[D]** | Asistente que consulta y opera lo ya construido, funciones de IA en esos módulos y conexión de IA externa (API/MCP) |
| 4 | **Impuestos + Ventas/Facturación + localización México** | Cliente → pedido → factura → CFDI timbrado → cobro, descontando inventario |
| 5 | **Contabilidad** | Catálogo de cuentas, pólizas, balanza y estados financieros. Genera las pólizas de las fases anteriores desde la bitácora |
| 6 | **Bancos/Tesorería** | Cuentas bancarias, pagos, cobros y conciliación |
| 7 | **Soporte a clientes** | Tickets de clientes con portal externo |
| 8 | **CRM** | Prospectos, oportunidades y seguimiento, conectado a Ventas |
| 9 | **Nómina/RH** | Empleados → cálculo → recibo timbrado → póliza |
| 10 | **Punto de venta** | Caja de mostrador con pantalla propia |
| 11 | **Activos fijos** | Altas, depreciación y bajas |
| 12 | **Proyectos/Obra** | Presupuestos, avances y costos por proyecto |
| 13 | **Producción** | Listas de materiales, órdenes de producción y consumo |
| 14 | **Mantenimiento** | Equipos, órdenes de trabajo y preventivos |
| 15 | **Tienda de extensiones** **[D]** | Sitio central: catálogo por módulo, revisión, firma, cobro y licencias. Hasta entonces las extensiones se instalan desde archivo |

## Decisiones sobre las piezas que no son módulos de negocio

- **Primer módulo: TI interno [D].** Chico, sin nada fiscal, útil por sí solo; prueba la base y la idea de "módulo suelto".
- **Instalador de Windows: versión mínima desde la fase 0 [D]**, y se va puliendo. Es el diferenciador número 1 y evita descubrir tarde que algo no se deja empaquetar.
- **IA: después de 2–3 módulos [D].** Colocada tras Compras **[D]**, cuando ya hay tres módulos con documentos reales.
- **Tienda: hasta el final de todo [D]**, después de todos los módulos. Mientras tanto las extensiones se instalan desde archivo; el formato de extensión, la firma y la interfaz pública versionada sí existen desde antes, para que la tienda solo sea el sitio que las distribuye y cobra.

## Pendientes

- Relación Ventas–Inventario (fase 4): si Ventas puede instalarse sin Inventario cuando se venden artículos físicos, apartar al pedir o descontar al facturar, vender sin existencia.
- Orden fino de las fases 7 a 14: puede cambiar según lo que pida la comunidad.
- Segunda localización de país y cuándo.
