## ADDED Requirements

### Requirement: La API de documentos exige sesión y permiso

El sistema NO SHALL atender ninguna operación de la API de documentos, ni entregar descripciones de documentos ni la lista de paquetes, a quien no tenga una sesión iniciada.

El sistema SHALL entregar la lista de documentos disponibles y la descripción de un documento solo para los documentos que la cuenta puede leer.

Ante una cuenta sin permiso de lectura sobre un documento, el sistema SHALL responder a toda operación sobre él igual que si el documento no existiera.

Ante una cuenta que puede leer un documento pero no darlo de alta, el sistema SHALL rechazar el alta indicando falta de permiso y NO SHALL guardar nada.

#### Scenario: Petición sin sesión
- **WHEN** se pide la lista de un documento sin sesión iniciada
- **THEN** la respuesta indica que falta iniciar sesión
- **AND** no devuelve ningún registro ni dice si el documento existe

#### Scenario: Lista de documentos disponibles
- **WHEN** una cuenta con permiso de lectura sobre un solo documento pide la lista de documentos disponibles
- **THEN** la respuesta trae ese documento y ningún otro

#### Scenario: Documento sin permiso de lectura
- **WHEN** una cuenta pide la descripción, la lista o un registro de un documento que no puede leer
- **THEN** la respuesta indica que no se encontró, igual que para un documento que ningún paquete declara
- **AND** no devuelve campos ni registros

#### Scenario: Alta sin permiso de alta
- **WHEN** una cuenta que puede leer un documento pero no darlo de alta envía un registro
- **THEN** la respuesta indica falta de permiso
- **AND** la lista no cambia

### Requirement: Todo registro guarda quién lo creó y de qué empresa es

El sistema SHALL registrar en cada alta la cuenta que la hizo y la empresa de la instalación, asignadas por el servidor.

El sistema NO SHALL aceptar valores enviados por quien llama para esos datos.

#### Scenario: Alta con sesión iniciada
- **WHEN** una cuenta da de alta un registro
- **THEN** el registro queda con esa cuenta como autora y con la empresa de la instalación

#### Scenario: Quien llama intenta fijar al autor
- **WHEN** una cuenta da de alta un registro enviando como autora a otra cuenta
- **THEN** el registro se guarda con la cuenta de la sesión como autora, no con la enviada

## REMOVED Requirements

### Requirement: Mientras no haya inicio de sesión, el servidor solo atiende a su propia máquina

**Reason**: Ya existe inicio de sesión. La restricción era temporal (decisión 5 del change 0-1-1) y este change la retira: el servidor atiende a la red local por conexión cifrada, y la protección pasa a ser la sesión y el permiso.

**Migration**: La protección la dan ahora el requisito "La API de documentos exige sesión y permiso" de esta capacidad, "El servidor atiende a la red local solo por conexión cifrada" de `sesion-y-cuentas`, y "Desde otra máquina, el alta inicial pide el código de instalación" de `primer-arranque`, que protege a la instalación mientras no exista empresa.
