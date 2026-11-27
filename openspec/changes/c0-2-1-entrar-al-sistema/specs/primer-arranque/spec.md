## ADDED Requirements

### Requirement: Una instalación vacía pide crear la empresa y su administrador

El sistema SHALL, mientras no exista ninguna empresa, ofrecer como única pantalla el alta inicial, que pide el nombre de la empresa y el nombre, usuario y contraseña de la cuenta de administrador. De la empresa NO SHALL pedir ningún otro dato. El nombre de la empresa SHALL tener como máximo 200 caracteres y el de la persona 100.

Al completarla, el sistema SHALL crear la empresa y la cuenta de administrador juntas o ninguna, y SHALL dejar a esa persona dentro del sistema con su sesión iniciada.

La contraseña del administrador SHALL cumplir las mismas reglas que cualquier otra.

#### Scenario: Primer arranque
- **WHEN** alguien abre el sistema recién instalado
- **THEN** ve la pantalla de alta inicial y ninguna otra

#### Scenario: Alta inicial completa
- **WHEN** llena el nombre de la empresa y los datos del administrador con una contraseña válida y guarda
- **THEN** la empresa y la cuenta quedan creadas
- **AND** queda dentro del sistema como administrador, sin volver a escribir su contraseña

#### Scenario: Contraseña que no cumple las reglas
- **WHEN** intenta completar el alta inicial con una contraseña que no cumple las reglas
- **THEN** el alta se rechaza señalando la contraseña
- **AND** no se crea ni la empresa ni la cuenta

### Requirement: Nada más funciona hasta completar el alta inicial

El sistema NO SHALL atender, mientras no exista empresa, ninguna operación distinta de consultar el estado de la instalación y completar el alta inicial.

El sistema NO SHALL tener ninguna cuenta ni contraseña de fábrica.

#### Scenario: Consultar documentos antes del alta inicial
- **WHEN** se pide la lista de cualquier documento en una instalación sin empresa
- **THEN** la respuesta indica que falta el alta inicial
- **AND** no devuelve ningún dato ni la lista de documentos disponibles

#### Scenario: Intentar entrar antes del alta inicial
- **WHEN** se intenta iniciar sesión con cualquier usuario y contraseña en una instalación sin empresa
- **THEN** la entrada se rechaza

### Requirement: Desde otra máquina, el alta inicial pide el código de instalación

El sistema SHALL aceptar el alta inicial sin código cuando venga de la máquina donde corre el servidor, y SHALL exigir el código de instalación cuando venga de cualquier otra.

El sistema SHALL, mientras no exista empresa, generar en cada arranque un código de instalación nuevo de ocho caracteres, formado por letras mayúsculas y números sin los que se confunden entre sí (cero y O, uno, I y L), mostrado en dos grupos de cuatro y aceptado sin distinguir mayúsculas, y SHALL escribirlo en la consola y en el registro del servidor. El sistema NO SHALL entregarlo por ninguna otra vía.

Tras cinco códigos equivocados, el sistema SHALL dejar de aceptar el alta inicial desde otras máquinas hasta que el servidor se reinicie.

#### Scenario: Alta inicial desde la máquina del servidor
- **WHEN** alguien completa el alta inicial desde la máquina donde corre el servidor
- **THEN** no se le pide ningún código

#### Scenario: Alta inicial desde otra máquina con el código correcto
- **WHEN** alguien abre el sistema recién instalado desde otra máquina, escribe el código que aparece en el registro del servidor y completa el alta inicial
- **THEN** la empresa y la cuenta quedan creadas

#### Scenario: Otra máquina sin código o con código equivocado
- **WHEN** otra máquina envía el alta inicial sin código o con uno equivocado
- **THEN** la petición se rechaza
- **AND** no se crea ni la empresa ni la cuenta
- **AND** la respuesta no trae el código ni pista de cuál es

#### Scenario: Quinto código equivocado
- **WHEN** se envían cinco códigos equivocados y después el correcto desde otra máquina
- **THEN** el alta inicial se rechaza
- **AND** desde la máquina del servidor sigue pudiéndose completar

#### Scenario: El código cambia al reiniciar
- **WHEN** el servidor se reinicia sin que exista empresa
- **THEN** el código anterior deja de servir y el registro muestra uno nuevo

### Requirement: El alta inicial ocurre una sola vez

El sistema SHALL rechazar el alta inicial cuando ya exista una empresa, venga de donde venga, y NO SHALL modificar la empresa ni las cuentas existentes.

#### Scenario: Segundo intento de alta inicial
- **WHEN** se envía el alta inicial a una instalación que ya tiene empresa
- **THEN** la petición se rechaza
- **AND** la empresa y el administrador existentes no cambian

#### Scenario: Dos altas iniciales al mismo tiempo
- **WHEN** llegan dos altas iniciales simultáneas a una instalación sin empresa
- **THEN** una se acepta y la otra se rechaza
- **AND** queda una sola empresa y un solo administrador
