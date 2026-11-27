## ADDED Requirements

### Requirement: TI interno trae de fábrica los roles técnico de sistemas y solicitante

El módulo de TI interno SHALL traer dos roles de fábrica:

- **Técnico de sistemas**: levanta tickets y ve todos los tickets.
- **Solicitante**: levanta tickets y ve solo los que levantó.

Ambos SHALL quedar disponibles para asignar al cargar el módulo por primera vez, y SHALL poder editarse como cualquier otro rol.

Quien no tenga un rol que conceda ver tickets ni sea administrador NO SHALL ver tickets ni que el documento existe.

#### Scenario: Un técnico ve los tickets de todos
- **WHEN** un solicitante levanta un ticket y un técnico de sistemas abre la lista de tickets
- **THEN** el técnico ve ese ticket

#### Scenario: Un solicitante ve solo los suyos
- **WHEN** dos solicitantes levantan un ticket cada uno y el primero abre la lista de tickets
- **THEN** ve su ticket
- **AND** no ve el del otro solicitante

#### Scenario: Un solicitante abre el ticket de otro por su dirección
- **WHEN** un solicitante abre la dirección del ticket que levantó otra persona
- **THEN** la pantalla dice que no se encontró
- **AND** no muestra ningún dato de ese ticket

#### Scenario: Cuenta sin rol de TI interno
- **WHEN** una cuenta sin ningún rol que conceda ver tickets entra al sistema
- **THEN** no se le ofrece la lista de tickets
- **AND** pedirla por la API responde como si el documento no existiera

#### Scenario: Un rol a la medida da acceso a tickets
- **WHEN** un administrador crea un rol que concede ver todos los tickets pero no levantarlos, y lo asigna a una cuenta
- **THEN** esa cuenta ve la lista de tickets
- **AND** no se le ofrece levantar uno

### Requirement: Cada ticket registra quién lo levantó

Todo ticket SHALL registrar la cuenta que lo levantó, asignada por el servidor a partir de la sesión.

#### Scenario: Levantar un ticket con sesión iniciada
- **WHEN** un solicitante levanta un ticket
- **THEN** el ticket queda con ese solicitante como quien lo levantó
- **AND** aparece en su lista de tickets

#### Scenario: Levantar un ticket a nombre de otro
- **WHEN** se da de alta un ticket por la API enviando a otra cuenta como quien lo levanta
- **THEN** el ticket queda a nombre de la cuenta de la sesión
