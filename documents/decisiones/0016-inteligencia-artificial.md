# 0016 — Inteligencia artificial como paquete opcional

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.7

## Decisión

OSME integra asistentes de IA externos por API mediante un paquete opcional. Cubre tres usos: asistente dentro del ERP, funciones de IA en módulos e IA externa que opera el ERP. Cada instalación pone su propia llave del proveedor. Qué puede hacer la IA sin confirmación humana lo configura cada empresa por tipo de documento.

## Por qué

- Opcional: el ERP funciona completo sin IA y sin internet, y el núcleo se mantiene ligero.
- Llave propia: coherente con que el proyecto no opera nada.
- Además: neutral al proveedor con un adaptador por cada uno; la IA usa descripciones de documentos y servicios públicos, no tablas; actúa con los permisos del usuario; de fábrica todo cambio de datos pide confirmación; la bitácora marca lo hecho por IA; MCP para la IA externa; aviso de que con proveedor externo los datos consultados salen de la instalación.

## Alternativas descartadas

- **IA dentro del núcleo**: engorda lo que todos cargan.
- **El proyecto revende créditos de IA**: obliga a operar un servicio central y manejar datos de clientes.
- **La IA ejecuta siempre según permisos, sin confirmación**: un error de la IA quedaría registrado en contabilidad.
