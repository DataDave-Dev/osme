## ADDED Requirements

### Requirement: Declarar un documento basta para poder guardarlo

El sistema SHALL, a partir de la declaración de un documento hecha por un paquete, dejar lista su tabla al cargar el paquete, sin que el paquete escriba instrucciones de base de datos.

Cuando la declaración gane un campo nuevo, el sistema SHALL agregarlo a la tabla existente en el siguiente arranque, conservando los registros ya guardados.

El sistema NO SHALL borrar ni alterar columnas existentes como resultado de un cambio en la declaración.

#### Scenario: Primer arranque con un documento declarado
- **WHEN** el servidor arranca por primera vez con un paquete que declara un documento
- **THEN** se puede guardar y leer un registro de ese documento

#### Scenario: La declaración gana un campo
- **WHEN** a un documento con registros guardados se le agrega un campo en su declaración y el servidor vuelve a arrancar
- **THEN** los registros anteriores siguen leyéndose, con el campo nuevo vacío
- **AND** los registros nuevos pueden guardar ese campo

### Requirement: El servidor publica la descripción de cada documento

El sistema SHALL ofrecer, para cada documento de cada paquete cargado, una descripción con su nombre, su etiqueta y la lista de sus campos; y para cada campo, su nombre, etiqueta, tipo, si es obligatorio, si es de solo lectura y, cuando aplique, sus opciones.

#### Scenario: Pedir la descripción de un documento existente
- **WHEN** se pide la descripción del ticket de TI interno
- **THEN** la respuesta lista sus campos con tipo, etiqueta y si son obligatorios

#### Scenario: Pedir la descripción de un documento que no existe
- **WHEN** se pide la descripción de un documento que ningún paquete cargado declara
- **THEN** la respuesta indica que no se encontró
- **AND** no revela qué documentos sí existen

### Requirement: Todo documento declarado se puede dar de alta y consultar por la misma API

El sistema SHALL ofrecer para cualquier documento declarado, con las mismas rutas y la misma forma de respuesta: alta de un registro, consulta de un registro por su identificador y consulta de la lista.

El identificador y la fecha de creación SHALL asignarlos el servidor. El sistema NO SHALL aceptar valores enviados por quien llama para esos dos datos, ni para ningún campo de solo lectura.

La lista SHALL devolver primero los registros más recientes.

#### Scenario: Alta válida
- **WHEN** se da de alta un registro con todos sus campos obligatorios
- **THEN** la respuesta trae el registro con su identificador y su fecha de creación
- **AND** el registro aparece al consultar la lista

#### Scenario: Quien llama intenta fijar el identificador
- **WHEN** se da de alta un registro enviando un identificador o una fecha de creación
- **THEN** el registro se guarda con los valores que asigna el servidor, no con los enviados

#### Scenario: Consulta de un identificador que no existe
- **WHEN** se consulta un registro con un identificador que no corresponde a ninguno
- **THEN** la respuesta indica que no se encontró

### Requirement: Lo que no cumple la declaración no se guarda

El sistema SHALL rechazar el alta de un registro al que le falte un campo obligatorio, que traiga un valor fuera de las opciones de un campo de opción, que exceda la longitud máxima de un campo o que traiga un campo que la declaración no tiene.

El rechazo SHALL decir qué campo falló y por qué, y NO SHALL guardar nada.

#### Scenario: Falta un campo obligatorio
- **WHEN** se da de alta un registro sin un campo obligatorio
- **THEN** la respuesta es un rechazo que nombra ese campo
- **AND** la lista no cambia

#### Scenario: Valor fuera de las opciones
- **WHEN** se da de alta un registro con un valor que no está entre las opciones de un campo de opción
- **THEN** la respuesta es un rechazo que nombra ese campo y sus opciones válidas

#### Scenario: Campo desconocido
- **WHEN** se da de alta un registro con un campo que la declaración no tiene
- **THEN** la respuesta es un rechazo que nombra ese campo
- **AND** no se guarda nada

### Requirement: Mientras no haya inicio de sesión, el servidor solo atiende a su propia máquina

El sistema NO SHALL aceptar conexiones desde otra máquina mientras no exista inicio de sesión.

#### Scenario: Petición desde la misma máquina
- **WHEN** se consulta la lista de un documento desde la máquina donde corre el servidor
- **THEN** la respuesta trae la lista

#### Scenario: Petición desde otra máquina de la red
- **WHEN** otra máquina de la misma red intenta consultar cualquier ruta del servidor
- **THEN** la conexión no se establece
- **AND** no recibe ningún dato
