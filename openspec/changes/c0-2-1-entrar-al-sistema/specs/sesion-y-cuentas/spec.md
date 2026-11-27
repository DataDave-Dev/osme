## ADDED Requirements

### Requirement: Se entra con usuario y contraseña

El sistema SHALL iniciar una sesión cuando se presenten el usuario y la contraseña de una cuenta activa, sin distinguir mayúsculas de minúsculas en el usuario.

El sistema SHALL responder de la misma forma ante un usuario que no existe, una contraseña equivocada, una cuenta desactivada y una cuenta bloqueada, sin decir cuál fue el caso.

La respuesta a una entrada correcta SHALL traer el nombre de la cuenta, sus roles y sus permisos, y NO SHALL traer la contraseña ni ningún dato derivado de ella.

#### Scenario: Entrada correcta
- **WHEN** alguien escribe su usuario y su contraseña y entra
- **THEN** ve la aplicación con su nombre
- **AND** las opciones que se le ofrecen corresponden a sus roles

#### Scenario: Contraseña equivocada
- **WHEN** alguien escribe un usuario que existe con una contraseña equivocada
- **THEN** la entrada se rechaza con un mensaje que no dice si el usuario existe

#### Scenario: Usuario que no existe
- **WHEN** alguien escribe un usuario que no existe
- **THEN** recibe exactamente el mismo rechazo que con una contraseña equivocada

#### Scenario: Cuenta desactivada
- **WHEN** alguien escribe el usuario y la contraseña correctos de una cuenta desactivada
- **THEN** recibe el mismo rechazo que con una contraseña equivocada
- **AND** no se inicia ninguna sesión

### Requirement: Los intentos fallidos repetidos frenan la cuenta

El sistema SHALL, tras cinco entradas fallidas seguidas sobre una misma cuenta, rechazar toda entrada a esa cuenta durante quince minutos, aunque la contraseña sea correcta.

El sistema SHALL permitir, a quien tiene la vista Usuarios, desbloquear una cuenta antes de que pasen los quince minutos.

Una entrada correcta SHALL reiniciar la cuenta de intentos fallidos.

#### Scenario: Quinto intento fallido
- **WHEN** se falla la contraseña de una cuenta cinco veces seguidas y enseguida se escribe la correcta
- **THEN** la entrada se rechaza

#### Scenario: Pasado el tiempo de espera
- **WHEN** pasan quince minutos desde el quinto intento fallido y se escribe la contraseña correcta
- **THEN** la entrada se acepta

#### Scenario: Desbloqueo desde la pantalla de usuarios
- **WHEN** una cuenta está bloqueada y alguien con la vista Usuarios la desbloquea
- **THEN** esa cuenta puede entrar de inmediato con su contraseña correcta

#### Scenario: Intentos fallidos sin llegar a cinco
- **WHEN** se falla la contraseña cuatro veces y luego se escribe la correcta
- **THEN** la entrada se acepta
- **AND** un fallo posterior cuenta de nuevo desde uno

### Requirement: Se puede salir, y la sesión caduca sola

El sistema SHALL terminar la sesión cuando su dueño salga, y desde ese momento NO SHALL aceptar esa sesión para ninguna operación.

El sistema SHALL dar por terminada una sesión a las doce horas sin uso y, en cualquier caso, a los siete días de iniciada.

Salir SHALL terminar solo la sesión del equipo desde el que se sale.

#### Scenario: Salir
- **WHEN** alguien con sesión iniciada sale
- **THEN** ve la pantalla de entrada
- **AND** una petición hecha con la sesión anterior se rechaza por falta de sesión

#### Scenario: Sesión sin uso
- **WHEN** una sesión pasa doce horas sin ninguna petición y luego se usa
- **THEN** la petición se rechaza por falta de sesión
- **AND** la interfaz muestra la pantalla de entrada

### Requirement: La instalación decide si una cuenta puede tener varias sesiones a la vez

El sistema SHALL tener un ajuste único de la instalación, que vale para todas las cuentas en todos los módulos, y que dice si una cuenta puede tener sesión abierta en varios equipos a la vez. El cambio SHALL aplicar sin reiniciar el servidor. De fábrica SHALL estar permitido.

El sistema SHALL permitir cambiar ese ajuste solo a quien tiene la vista Ajustes de acceso.

Cuando no esté permitido, entrar en un equipo SHALL terminar las demás sesiones de esa cuenta.

#### Scenario: De fábrica, dos equipos a la vez
- **WHEN** alguien entra en su computadora y luego en su celular, sin haber cambiado el ajuste
- **THEN** las dos sesiones siguen valiendo

#### Scenario: Una sola sesión
- **WHEN** el ajuste no permite varias sesiones y alguien entra en un segundo equipo
- **THEN** la siguiente petición del primer equipo se rechaza por falta de sesión

#### Scenario: Quien no tiene la vista intenta cambiar el ajuste
- **WHEN** una cuenta sin la vista Ajustes de acceso envía un cambio al ajuste
- **THEN** la petición se rechaza
- **AND** el ajuste queda como estaba

### Requirement: Las contraseñas cumplen reglas mínimas y no se pueden leer

El sistema SHALL aceptar como contraseña solo un texto de entre 8 y 128 caracteres que tenga al menos una mayúscula, una minúscula, un número y un símbolo, y SHALL rechazar las demás diciendo qué regla falló.

El sistema NO SHALL guardar la contraseña de forma que pueda recuperarse, y NO SHALL devolverla ni escribirla en sus registros.

#### Scenario: Contraseña sin símbolo
- **WHEN** se propone una contraseña de diez caracteres con mayúscula, minúscula y número pero sin símbolo
- **THEN** se rechaza indicando que falta un símbolo

#### Scenario: Contraseña demasiado corta
- **WHEN** se propone una contraseña de siete caracteres
- **THEN** se rechaza indicando la longitud mínima

#### Scenario: La contraseña no aparece en ninguna respuesta
- **WHEN** alguien con la vista Usuarios consulta la lista de cuentas o una cuenta
- **THEN** la respuesta no trae la contraseña ni ningún dato derivado de ella

### Requirement: Quien tiene la vista Usuarios crea cuentas y les asigna roles

El sistema SHALL permitir, a quien tiene la vista Usuarios, crear una cuenta con nombre, usuario, contraseña temporal y roles, consultar la lista de cuentas y cambiar los roles de otra cuenta. Una cuenta NO SHALL tener más datos que esos.

El nombre de la persona SHALL tener como máximo 100 caracteres.

El usuario SHALL tener entre 3 y 50 caracteres, solo letras sin acento, números, punto, guion y guion bajo, y SHALL guardarse en minúsculas.

Quien tiene la vista Usuarios SHALL poder asignar y quitar cualquier rol activo, incluido el de administrador.

Una cuenta SHALL poder existir sin ningún rol: entra y ve un aviso de que no tiene nada asignado.

El sistema SHALL ofrecer para asignar los roles activos de la instalación, y ningún otro.

El sistema NO SHALL permitir dos cuentas con el mismo usuario.

El sistema NO SHALL permitir a nadie cambiar sus propios roles.

El sistema NO SHALL permitir a quien no tiene la vista Usuarios crear, consultar ni modificar cuentas.

#### Scenario: Crear una cuenta
- **WHEN** alguien con la vista Usuarios crea una cuenta con el rol de técnico de sistemas
- **THEN** la cuenta aparece en la lista de cuentas con ese rol
- **AND** esa persona puede entrar con la contraseña temporal

#### Scenario: Usuario repetido
- **WHEN** se crea una cuenta con un usuario que ya existe, aunque cambie mayúsculas
- **THEN** el alta se rechaza señalando el usuario

#### Scenario: Usuario con caracteres no permitidos
- **WHEN** se crea una cuenta con un usuario que tiene espacios, acentos o menos de tres caracteres
- **THEN** el alta se rechaza señalando el usuario y la regla

#### Scenario: Cambiar roles
- **WHEN** alguien con la vista Usuarios quita un rol a otra cuenta
- **THEN** en la siguiente petición de esa cuenta ya no vale ese rol

#### Scenario: Cambiar los roles propios
- **WHEN** alguien con la vista Usuarios intenta darse o quitarse un rol a sí mismo
- **THEN** el cambio se rechaza
- **AND** sus roles quedan como estaban

#### Scenario: Cuenta sin roles
- **WHEN** entra una cuenta que no tiene ningún rol
- **THEN** ve un aviso de que no tiene nada asignado y de que hable con su administrador
- **AND** no se le ofrece ningún documento

#### Scenario: Quien no tiene la vista pide la lista de cuentas
- **WHEN** una cuenta sin la vista Usuarios pide la lista de cuentas
- **THEN** la petición se rechaza
- **AND** no recibe ningún nombre ni usuario

#### Scenario: Sin sesión
- **WHEN** se pide la lista de cuentas sin sesión iniciada
- **THEN** la petición se rechaza por falta de sesión
- **AND** no recibe ningún dato

### Requirement: La contraseña que pone otra persona es temporal

El sistema SHALL exigir, a quien entra con una contraseña puesta desde la pantalla de usuarios, que la cambie por una propia antes de hacer cualquier otra cosa.

El sistema SHALL permitir, a quien tiene la vista Usuarios, poner una contraseña temporal nueva a una cuenta, y al hacerlo SHALL terminar todas las sesiones de esa cuenta.

El sistema SHALL permitir a cualquier cuenta cambiar su propia contraseña presentando la actual, y al hacerlo SHALL terminar sus sesiones en otros equipos.

#### Scenario: Primera entrada con contraseña temporal
- **WHEN** alguien entra por primera vez con la contraseña temporal
- **THEN** la única pantalla que ve es la de cambiar contraseña
- **AND** cualquier otra operación se rechaza hasta que la cambie

#### Scenario: Cambio hecho
- **WHEN** escribe la contraseña temporal y una nueva que cumple las reglas
- **THEN** entra a la aplicación
- **AND** la contraseña temporal deja de servir

#### Scenario: Restablecer una contraseña
- **WHEN** alguien con la vista Usuarios pone una contraseña temporal a una cuenta que tiene una sesión abierta
- **THEN** esa sesión deja de valer
- **AND** la contraseña anterior deja de servir

#### Scenario: Cambiar la contraseña propia sin saber la actual
- **WHEN** alguien con sesión iniciada pide cambiar su contraseña con una contraseña actual equivocada
- **THEN** el cambio se rechaza y la contraseña sigue siendo la misma

### Requirement: Desactivar una cuenta le quita el acceso a todo de inmediato

El sistema SHALL permitir, a quien tiene la vista Usuarios, desactivar y reactivar otra cuenta. Al desactivarla, el sistema SHALL terminar todas sus sesiones y NO SHALL aceptar entradas a esa cuenta.

La cuenta desactivada SHALL conservarse, con sus roles y con lo que haya creado.

El sistema NO SHALL permitir a nadie desactivar su propia cuenta.

El sistema NO SHALL permitir desactivar ni quitar el rol de administrador a la última cuenta de administrador activa.

#### Scenario: Desactivar a quien tiene sesión abierta
- **WHEN** alguien con la vista Usuarios desactiva una cuenta que tiene una sesión abierta
- **THEN** la siguiente petición de esa sesión se rechaza por falta de sesión
- **AND** no recibe ningún dato

#### Scenario: Lo creado por la cuenta se conserva
- **WHEN** se desactiva una cuenta que había levantado tickets
- **THEN** esos tickets siguen en la lista de quien puede verlos

#### Scenario: Reactivar
- **WHEN** alguien con la vista Usuarios reactiva una cuenta
- **THEN** esa persona puede entrar otra vez con sus mismos roles

#### Scenario: Desactivarse a sí mismo
- **WHEN** alguien con la vista Usuarios intenta desactivar su propia cuenta, aunque haya otros administradores
- **THEN** el cambio se rechaza
- **AND** su cuenta sigue activa

#### Scenario: Último administrador
- **WHEN** alguien con la vista Usuarios intenta desactivar o quitarle el rol de administrador a la única cuenta de administrador activa
- **THEN** el cambio se rechaza
- **AND** esa cuenta sigue activa y como administrador

### Requirement: El servidor atiende a la red local solo por conexión cifrada

El sistema SHALL atender a las máquinas de la red local, y SHALL hacerlo únicamente por conexión cifrada.

El sistema SHALL generar por sí mismo lo necesario para cifrar la conexión, sin pedir al usuario instalar ni configurar nada, SHALL conservarlo entre reinicios y SHALL renovarlo solo antes de que venza, cada dos años.

El sistema NO SHALL entregar la sesión por una conexión sin cifrar.

#### Scenario: Entrar desde otra máquina
- **WHEN** alguien abre la dirección del servidor desde otra máquina de la red, por conexión cifrada
- **THEN** ve la pantalla de entrada y puede iniciar sesión

#### Scenario: Conexión sin cifrar
- **WHEN** otra máquina intenta cualquier ruta del servidor por conexión sin cifrar
- **THEN** no recibe ningún dato

#### Scenario: Reinicio del servidor
- **WHEN** el servidor se reinicia
- **THEN** un navegador que ya había aceptado la conexión cifrada no vuelve a ver la advertencia
