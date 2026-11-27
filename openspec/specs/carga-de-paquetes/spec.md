# carga-de-paquetes Specification

## Purpose
TBD - created by archiving change c0-1-1-un-ticket-de-punta-a-punta. Update Purpose after archive.
## Requirements
### Requirement: El servidor carga el núcleo y los paquetes presentes al arrancar

El servidor SHALL descubrir al arrancar los paquetes presentes en su carpeta de paquetes, leer el manifiesto de cada uno y cargarlos después del núcleo, en un orden que respete sus dependencias.

Cada paquete SHALL declarar en su manifiesto un nombre único, una versión y la lista de paquetes de los que depende.

#### Scenario: Arranque con un módulo presente
- **WHEN** el servidor arranca con el paquete de TI interno en su carpeta de paquetes
- **THEN** el servidor queda en marcha
- **AND** la lista de paquetes cargados incluye el núcleo y TI interno, cada uno con su versión

#### Scenario: Arranque sin módulos
- **WHEN** el servidor arranca con la carpeta de paquetes vacía
- **THEN** el servidor queda en marcha solo con el núcleo
- **AND** no se ofrece ningún documento de ningún módulo

### Requirement: Un paquete con una dependencia faltante no se carga

El servidor NO SHALL cargar un paquete cuyo manifiesto declare una dependencia obligatoria que no esté presente, y SHALL dejar registrado qué paquete se omitió y qué dependencia faltó.

Los demás paquetes SHALL seguir cargándose.

#### Scenario: Falta una dependencia obligatoria
- **WHEN** el servidor arranca con un paquete que depende de otro que no está presente
- **THEN** ese paquete no aparece en la lista de paquetes cargados
- **AND** el registro del servidor nombra el paquete omitido y la dependencia que falta
- **AND** el resto de los paquetes queda disponible

### Requirement: Cada paquete guarda sus datos en su propio esquema

El servidor SHALL crear las tablas de cada paquete en un esquema de base de datos propio de ese paquete, y NO SHALL crear tablas de un paquete en el esquema de otro.

#### Scenario: Las tablas de un módulo viven en su esquema
- **WHEN** el servidor termina de cargar el paquete de TI interno
- **THEN** la tabla de tickets existe en el esquema de TI interno
- **AND** no existe ninguna tabla de TI interno en el esquema del núcleo

### Requirement: Un módulo no usa el interior de otro paquete

El proyecto SHALL contar con una verificación automática que falle cuando un módulo use código de otro paquete que no forme parte de lo que ese paquete publica.

#### Scenario: Un módulo usa un tipo interno del núcleo
- **WHEN** se ejecuta la verificación sobre un módulo que referencia un tipo no publicado del núcleo
- **THEN** la verificación falla
- **AND** el mensaje nombra el módulo y el tipo usado

#### Scenario: Un módulo que solo usa lo publicado
- **WHEN** se ejecuta la verificación sobre el paquete de TI interno
- **THEN** la verificación pasa

