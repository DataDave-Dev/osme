## ADDED Requirements

### Requirement: La lista de un documento se dibuja a partir de su descripción

La interfaz SHALL mostrar, para cualquier documento, una lista de sus registros cuyas columnas salen de la descripción que publica el servidor, sin código de pantalla propio de ese documento.

Mientras los datos cargan, la interfaz SHALL indicarlo. Cuando no haya registros, SHALL decirlo y ofrecer crear el primero. Cuando el servidor no responda, SHALL decirlo y ofrecer reintentar.

#### Scenario: Lista con registros
- **WHEN** se abre la lista de un documento que tiene registros
- **THEN** se ven en filas, los más recientes primero, con las columnas de su descripción

#### Scenario: Lista vacía
- **WHEN** se abre la lista de un documento sin registros
- **THEN** la pantalla dice que no hay ninguno y ofrece crear el primero

#### Scenario: El servidor no responde
- **WHEN** se abre la lista y el servidor no contesta
- **THEN** la pantalla lo dice con un mensaje comprensible y ofrece reintentar
- **AND** no queda en blanco ni cargando sin fin

### Requirement: El formulario de alta se dibuja a partir de la descripción

La interfaz SHALL mostrar, para cualquier documento, un formulario de alta con un control por cada campo que se puede capturar, elegido según el tipo del campo, con su etiqueta y con los obligatorios señalados.

Los campos de solo lectura NO SHALL aparecer como capturables en el alta.

Cada control SHALL poder usarse solo con teclado y SHALL tener su etiqueta asociada.

#### Scenario: Formulario de un documento
- **WHEN** se abre el alta de un documento
- **THEN** aparece un control por cada campo capturable, del tipo que corresponde
- **AND** los campos obligatorios están señalados

#### Scenario: Un campo nuevo aparece sin tocar la interfaz
- **WHEN** a la declaración de un documento se le agrega un campo y se recarga la pantalla
- **THEN** el formulario y la lista muestran el campo nuevo
- **AND** no se modificó ningún archivo de la interfaz

### Requirement: Guardar muestra el resultado, sea éxito o rechazo

Al guardar, la interfaz SHALL llevar a la lista con el registro nuevo visible cuando el servidor acepte, y SHALL mostrar junto a cada campo el motivo del rechazo cuando el servidor rechace, conservando lo ya capturado.

La interfaz NO SHALL permitir un segundo envío mientras el primero no haya terminado.

#### Scenario: Alta aceptada
- **WHEN** se llena el formulario correctamente y se guarda
- **THEN** se muestra la lista y el registro nuevo aparece arriba

#### Scenario: Alta rechazada
- **WHEN** se guarda el formulario sin un campo obligatorio
- **THEN** el campo faltante queda señalado con el motivo
- **AND** lo capturado en los demás campos no se pierde

#### Scenario: Doble clic en guardar
- **WHEN** se oprime guardar dos veces seguidas
- **THEN** se crea un solo registro
