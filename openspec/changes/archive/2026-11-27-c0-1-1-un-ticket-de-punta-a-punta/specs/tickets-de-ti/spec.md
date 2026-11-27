## ADDED Requirements

### Requirement: Se puede levantar un ticket de sistemas

El módulo de TI interno SHALL ofrecer el documento ticket con título, descripción y prioridad.

El título SHALL ser obligatorio y de una longitud máxima definida. La descripción SHALL ser opcional. La prioridad SHALL ser una de baja, normal o alta, y SHALL quedar en normal cuando no se indique.

Todo ticket SHALL registrar la fecha y hora en que se levantó, asignada por el servidor.

#### Scenario: Levantar un ticket desde el navegador
- **WHEN** alguien abre el alta de tickets, escribe un título y una descripción y guarda
- **THEN** el ticket queda guardado con prioridad normal y su fecha de alta
- **AND** aparece en la lista de tickets

#### Scenario: Ticket sin título
- **WHEN** alguien intenta guardar un ticket sin título
- **THEN** el ticket no se guarda
- **AND** la pantalla señala que el título es obligatorio

#### Scenario: Prioridad que no existe
- **WHEN** se da de alta un ticket por la API con una prioridad distinta de baja, normal o alta
- **THEN** el alta se rechaza nombrando las prioridades válidas

### Requirement: Los tickets levantados se pueden consultar

El módulo SHALL mostrar la lista de tickets con su título, prioridad y fecha de alta, los más recientes primero, y SHALL permitir abrir un ticket para ver todos sus datos.

#### Scenario: Consultar la lista
- **WHEN** hay tickets levantados y se abre la lista
- **THEN** se ven con título, prioridad y fecha, los más recientes primero

#### Scenario: Los tickets sobreviven a un reinicio
- **WHEN** se levanta un ticket y el servidor se reinicia
- **THEN** el ticket sigue en la lista con los mismos datos
