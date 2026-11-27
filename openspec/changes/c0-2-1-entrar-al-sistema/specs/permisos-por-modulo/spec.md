## ADDED Requirements

### Requirement: Las vistas que se pueden conceder salen de los paquetes cargados

Cada documento declarado por un paquete SHALL tener, sin que el paquete lo escriba, un permiso por cada acción que la API de documentos ofrece.

El núcleo SHALL aportar además tres vistas propias, cada una concedible con una sola casilla que da a la vez consultar y modificar: Usuarios, Roles y Ajustes de acceso.

El sistema SHALL ofrecer, a quien tiene la vista Roles, el catálogo de permisos concedibles, agrupado por paquete y documento, con las etiquetas de cada uno, formado solo por las vistas del núcleo y los documentos de los paquetes cargados.

#### Scenario: Catálogo con un módulo cargado
- **WHEN** el servidor carga el paquete de TI interno y alguien con la vista Roles abre la edición de un rol
- **THEN** se ofrece el ticket con las acciones ver y dar de alta
- **AND** se ofrecen Usuarios, Roles y Ajustes de acceso, con una casilla cada una

#### Scenario: Un documento nuevo aparece sin tocar la interfaz
- **WHEN** un paquete declara un documento nuevo y el servidor vuelve a arrancar
- **THEN** ese documento aparece en la edición de roles con sus acciones
- **AND** no se modificó ningún archivo de la interfaz

#### Scenario: Quien no tiene la vista pide el catálogo
- **WHEN** una cuenta sin la vista Roles pide el catálogo de permisos
- **THEN** la petición se rechaza
- **AND** no recibe ningún documento ni permiso

### Requirement: Quien tiene la vista Roles crea y edita roles y decide qué vistas ve cada uno

El sistema SHALL permitir, a quien tiene la vista Roles, crear un rol con un nombre de hasta 100 caracteres, cambiarle el nombre, y marcar para ese rol cualquier combinación de permisos del catálogo, de uno o de varios paquetes.

El sistema SHALL aplicar el cambio de un rol a todas las cuentas que lo tienen desde su siguiente petición, sin reiniciar el servidor ni pedirles entrar de nuevo.

El sistema NO SHALL permitir dos roles con el mismo nombre, estén activos o no y sin distinguir mayúsculas, ni guardar en un rol un permiso que no esté en el catálogo.

El sistema NO SHALL guardar en un rol el permiso de dar de alta un documento sin el de verlo, al menos los propios.

El sistema NO SHALL permitir a quien no tiene la vista Roles crear, consultar ni modificar roles.

#### Scenario: Crear un rol a la medida
- **WHEN** alguien con la vista Roles crea un rol, le marca ver un documento, y ese rol se asigna a una cuenta
- **THEN** esa cuenta ve ese documento
- **AND** no se le ofrece darlo de alta

#### Scenario: Rol que mezcla módulos
- **WHEN** se crea un rol con vistas de dos paquetes distintos y la vista Usuarios
- **THEN** el rol se guarda
- **AND** una cuenta con ese rol ve los documentos de ambos paquetes y la pantalla de usuarios

#### Scenario: Editar un rol en uso
- **WHEN** alguien con la vista Roles le quita a un rol el permiso de ver un documento
- **THEN** en su siguiente petición, las cuentas que solo lo veían por ese rol reciben la misma respuesta que para un documento que no existe

#### Scenario: Nombre repetido
- **WHEN** se crea un rol con el nombre de otro que está desactivado, aunque cambie mayúsculas
- **THEN** el alta se rechaza señalando el nombre

#### Scenario: Dar de alta sin ver
- **WHEN** se guarda un rol por la API con el permiso de dar de alta un documento y sin el de verlo
- **THEN** el cambio se rechaza nombrando ese documento
- **AND** en pantalla, marcar dar de alta marca también ver los propios

#### Scenario: Permiso que no existe
- **WHEN** se guarda un rol por la API con un permiso que no está en el catálogo
- **THEN** el cambio se rechaza nombrando ese permiso
- **AND** el rol queda como estaba

#### Scenario: Quien no tiene la vista intenta editar un rol
- **WHEN** una cuenta sin la vista Roles envía un cambio a un rol
- **THEN** la petición se rechaza
- **AND** el rol queda como estaba
- **AND** no recibe los permisos de ningún rol

### Requirement: Un rol se desactiva, no se borra

El sistema SHALL permitir, a quien tiene la vista Roles, desactivar y reactivar un rol. Un rol desactivado NO SHALL conceder ningún permiso ni ofrecerse para asignar.

El sistema NO SHALL ofrecer borrar un rol.

Las asignaciones de un rol desactivado SHALL conservarse, de modo que al reactivarlo vuelva a valer para las mismas cuentas.

#### Scenario: Desactivar un rol asignado
- **WHEN** alguien con la vista Roles desactiva un rol que tiene una cuenta
- **THEN** en su siguiente petición esa cuenta ya no tiene los permisos de ese rol
- **AND** el rol no aparece al asignar roles a una cuenta

#### Scenario: Reactivar
- **WHEN** se reactiva ese rol
- **THEN** la cuenta recupera sus permisos sin que nadie se lo reasigne

### Requirement: Un paquete trae roles de fábrica, y el cliente los edita

El sistema SHALL permitir que un paquete declare, junto a sus documentos, roles de fábrica: cada uno con un nombre único dentro del paquete, una etiqueta y los permisos que concede sobre los documentos de ese mismo paquete.

El sistema SHALL crear esos roles la primera vez que carga el paquete, y desde entonces SHALL tratarlos como cualquier otro rol: se editan y se desactivan. El sistema NO SHALL ofrecer devolverlos a como venían.

El sistema NO SHALL volver a crear ni sobrescribir un rol de fábrica que ya creó una vez, aunque el cliente lo haya editado o desactivado, ni al reiniciar ni al actualizar el paquete.

El sistema NO SHALL cargar un paquete que declare un rol de fábrica sobre un documento que no es suyo o que no existe, y SHALL dejar registrado el motivo.

#### Scenario: Primera carga de un módulo
- **WHEN** el servidor carga por primera vez el paquete de TI interno
- **THEN** sus roles de fábrica aparecen en la lista de roles, listos para asignar

#### Scenario: Un rol de fábrica editado sobrevive al reinicio
- **WHEN** a un rol de fábrica se le quita un permiso y el servidor se reinicia
- **THEN** el rol sigue sin ese permiso
- **AND** no aparece un segundo rol con el nombre original

#### Scenario: Un rol de fábrica desactivado no regresa
- **WHEN** se desactiva un rol de fábrica y el servidor se reinicia
- **THEN** el rol sigue desactivado

#### Scenario: Rol de fábrica sobre un documento ajeno
- **WHEN** el servidor arranca con un paquete que declara un rol de fábrica con permiso sobre un documento de otro paquete
- **THEN** ese paquete no se carga
- **AND** el registro del servidor nombra el paquete, el rol y el documento

### Requirement: Lo que ningún rol concede está negado

El sistema SHALL permitir una acción sobre un documento solo a las cuentas que tengan algún rol activo que conceda ese permiso.

Un documento cuyo permiso no concede ningún rol SHALL quedar disponible solo para el administrador.

Los permisos de una cuenta SHALL ser la suma de los de todos sus roles activos.

#### Scenario: Cuenta sin ningún rol
- **WHEN** una cuenta sin roles entra al sistema
- **THEN** no se le ofrece ningún documento
- **AND** cualquier consulta a un documento responde como si no existiera

#### Scenario: Cuenta con dos roles
- **WHEN** una cuenta tiene un rol que concede ver un documento y otro que concede darlo de alta
- **THEN** puede verlo y darlo de alta

#### Scenario: Documento que ningún rol concede
- **WHEN** un paquete declara un documento y ningún rol tiene permiso sobre él
- **THEN** el administrador puede usarlo
- **AND** a ninguna otra cuenta se le ofrece

### Requirement: El núcleo aplica los permisos, no cada módulo

El sistema SHALL comprobar el permiso de toda operación sobre un documento en el núcleo, de la misma forma para todos los documentos de todos los paquetes, sin que el paquete escriba comprobación alguna.

#### Scenario: Un documento nuevo queda protegido sin escribir nada
- **WHEN** un paquete declara un documento nuevo, sin código de acceso, y una cuenta sin permiso lo consulta
- **THEN** la consulta responde como si el documento no existiera
- **AND** no devuelve ningún registro

### Requirement: Un permiso de ver alcanza todos los registros o solo los propios

El sistema SHALL permitir que un rol conceda ver un documento con alcance a todos los registros o solo a los que creó la propia cuenta, a elección de quien edita el rol.

Con alcance a los propios, el sistema NO SHALL devolver registros creados por otra cuenta, ni en la lista ni al pedirlos por su identificador.

Cuando una cuenta tenga roles con alcances distintos sobre el mismo documento, SHALL valer el más amplio.

#### Scenario: Lista con alcance a los propios
- **WHEN** dos cuentas con alcance a los propios crean registros y una de ellas pide la lista
- **THEN** la lista trae solo los que ella creó

#### Scenario: Pedir por identificador un registro ajeno
- **WHEN** una cuenta con alcance a los propios pide por su identificador un registro que creó otra cuenta
- **THEN** la respuesta indica que no se encontró, igual que para un identificador que no existe
- **AND** no devuelve ningún dato del registro

#### Scenario: Dos roles con alcances distintos
- **WHEN** una cuenta tiene un rol con alcance a los propios y otro con alcance a todos sobre el mismo documento
- **THEN** la lista trae los registros de todas las cuentas

### Requirement: El administrador puede todo y su rol no se toca

El núcleo SHALL traer el rol de administrador, que concede todas las acciones sobre todos los documentos de todos los paquetes cargados, y las vistas Usuarios, Roles y Ajustes de acceso.

El sistema NO SHALL permitir editar ni desactivar el rol de administrador.

#### Scenario: Administrador ante un módulo recién cargado
- **WHEN** el servidor carga un paquete nuevo y el administrador entra
- **THEN** se le ofrecen todos los documentos de ese paquete sin tocar ningún rol

#### Scenario: Intentar editar el rol de administrador
- **WHEN** alguien con la vista Roles intenta quitarle un permiso al rol de administrador, renombrarlo o desactivarlo
- **THEN** el cambio se rechaza
- **AND** el rol sigue concediendo todo

### Requirement: Un permiso de un paquete que ya no está cargado no concede nada

El sistema NO SHALL conceder permisos sobre documentos de un paquete que no esté cargado, ni mostrarlos al editar un rol.

Lo guardado en el rol SHALL conservarse, de modo que vuelva a valer si el paquete se carga otra vez.

#### Scenario: El paquete deja de estar presente
- **WHEN** una cuenta tiene un rol con permisos sobre un paquete y el servidor arranca sin ese paquete
- **THEN** la cuenta puede entrar
- **AND** no se le ofrece ningún documento de ese paquete

#### Scenario: El paquete vuelve
- **WHEN** el servidor arranca otra vez con ese paquete
- **THEN** la cuenta recupera esos permisos sin que nadie edite el rol
