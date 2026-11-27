## Context

Tras el change 0-1-1 el servidor carga núcleo y TI interno, crea tablas desde la declaración y ofrece la API uniforme de documentos, pero no sabe quién llama: escucha solo en `127.0.0.1` y `[::1]`, y deja `company_id`, `created_by` y `updated_by` vacíos y sin llave foránea (`documents/arquitectura/base-de-datos.md`, sección 12).

Este change crea `core.company` y `core.user_account`, el inicio de sesión, los roles editables y los permisos, y abre el servidor a la red local. Lo respaldan `documents/decisiones/0014-usuarios-compartidos.md`, `documents/decisiones/0020-roles-editables.md`, `documents/arquitectura/arquitectura.md` 4.1 y `documents/fases/fase-00-base-ti-interno/diseno.md` 2.4.

Restricciones heredadas:

- El núcleo (`src/Core`) es una biblioteca que hoy solo depende de Npgsql; no referencia ASP.NET.
- El motor solo sabe dar de alta, consultar uno y listar. Editar registros llega en `c0-3-1`.
- Los rechazos viajan como código (`required`, `not_found`), nunca como frase; la interfaz los redacta.
- No hay bitácora de cambios todavía (`c0-3-1`).

**Todas las decisiones de abajo las tomó DataDave-Dev el 2026-11-27**, respondiendo preguntas con opciones. Donde eligió algo distinto de lo recomendado se dice. Lo que nadie ha decidido está en "Open Questions", sin valor.

**Verificado ejecutándolo:** que NSec.Cryptography calcula Argon2id en Linux con .NET 10 y da el mismo resultado que otras cuatro librerías (comparativa de la decisión 2). También la tarea 2.1: el núcleo crea sus siete tablas y el rol de administrador, y un segundo arranque no cambia nada. Nada más de este change todavía. Del anterior sigue en pie lo que su `design.md` lista como verificado.

**Supuesto** (cada uno lo cierra una tarea):

- Que Kestrel acepte un certificado generado en el arranque y lo sirva sin más configuración en Windows y Linux.
- Que la publicación autocontenida empaquete sola el binario nativo de libsodium en Windows.
- Que agregar llaves foráneas a tablas ya creadas quepa en "crear lo que falta" sin romper los registros con esas columnas vacías.
- Que las pruebas de integración (servidor en memoria, sin Kestrel) puedan simular una petición que no viene de la propia máquina.

## Goals / Non-Goals

**Goals**

- Ninguna ruta de datos responde sin sesión.
- El control de acceso vive en un solo lugar del núcleo; ningún módulo escribe una comprobación.
- Los roles se crean y editan desde pantalla, eligiendo a qué vistas da acceso cada uno, igual para todos los módulos.
- Una instalación vacía no queda abierta ni con contraseña de fábrica.
- Desactivar una cuenta corta su acceso en la petición siguiente.

**Non-Goals**

- Verificación en dos pasos, varias empresas, sucursales, empleados (entrega 0.2).
- Portal y cuentas de portal, correo de la cuenta, recuperar contraseña por correo (entrega 0.3).
- Permisos por campo o por estado del documento (entrega 0.5).
- Llaves de API para integraciones (entrega 0.7).
- Nombre fijo de la instalación, quitar la advertencia del navegador, redirección desde `http://` y recuperación del administrador que olvida su contraseña (`c0-5-1`).
- Bitácora de quién cambió qué (`c0-3-1`).
- Borrar roles y restaurar roles de fábrica.

## Decisions

### 1. Empresa, cuentas, roles y sesiones son tablas propias del núcleo, no documentos declarados

Tablas escritas a mano en el esquema `core`, siguiendo `base-de-datos.md`, con rutas dedicadas y pantallas propias (alta inicial, entrada, usuarios, roles, ajustes de acceso).

| Tabla | Guarda |
|---|---|
| `core.company` | La empresa. Una sola fila en este change; solo su nombre |
| `core.user_account` | Cuenta: usuario, nombre, hash de contraseña, `is_active`, intentos fallidos, bloqueo, si debe cambiar contraseña |
| `core.role` | Rol: nombre, `is_active`, si es del sistema, y de qué rol de fábrica nació |
| `core.role_permission` | Qué permisos concede cada rol y con qué alcance |
| `core.role_user_account` | Qué roles tiene cada cuenta |
| `core.session` | Sesiones abiertas |
| `core.setting` | Ajustes globales de la instalación: clave, valor y quién lo cambió. Sin empresa |

- **Columnas fijas completas solo en los catálogos** (`company`, `user_account`, `role`), sin `company_id` porque son de la instalación. Las tablas técnicas llevan solo lo que usan. **Descartado: las ocho columnas fijas en las siete tablas.**
- **El valor de un ajuste es `jsonb`.** **Descartado: `text`.**
- **Usuario y nombre de rol únicos por índice en la base** (`uq_user_account__username`, `uq_role__name` sobre el nombre en minúsculas). **Descartado: que solo lo compruebe el código.**
- **El rol de administrador se guarda con el nombre interno `core.administrator`; la interfaz lo traduce.** **Descartado: guardar "Administrador" en la base.**
- **El código vive en `src/Core/Access`** (`Osme.Core.Access`). **Descartado: `Identity`** y **dos carpetas, `Accounts` y `Permissions`.**
- **Descartado: declararlas como documentos del motor.** Obligaría a meter en este change edición de registros y campos secretos, planeados para `c0-3-1`.
- La cuenta tiene **usuario y nombre**, nada más. **Descartado: correo opcional desde ahora**; llega con el correo (0.3).
- El usuario tiene **de 3 a 50 caracteres: letras sin acento, números, punto, guion y guion bajo**, guardado en minúsculas. **Descartado: texto libre** y **entrar con correo**.
- **Longitudes máximas: nombre de la persona 100, nombre del rol 100, nombre de la empresa 200.** **Descartado: 200 para todos** y **50 / 50 / 100.**
- El alta inicial pide de la empresa **solo el nombre**. **Descartado: país, zona horaria y moneda desde ahora**; llegan con Organización (0.2).

### 2. Contraseñas con Argon2id, por NSec.Cryptography

DataDave-Dev eligió Argon2id en lugar del PBKDF2 de .NET que se recomendaba, y después la librería y los parámetros con esta comparativa a la vista.

Medido el 2026-11-27 en la máquina de desarrollo (i9-12900H, .NET 10), mejor de cinco, un hilo. Las cinco librerías dieron el mismo hash con los mismos parámetros.

| Librería | Licencia | Código | Último release | 19 MiB, 2 pasadas | 46 MiB, 1 pasada | 64 MiB, 3 pasadas |
|---|---|---|---|---|---|---|
| **NSec.Cryptography 26.4.0** | MIT + libsodium ISC | nativo (libsodium) | 2026-04-30 | 19 ms | 30 ms | 96 ms |
| Geralt 4.5.0 | MIT + libsodium ISC | nativo (libsodium) | 2026-10-31 | 19 ms | 31 ms | 99 ms |
| BouncyCastle.Cryptography 2.7.0 | MIT | C# puro, 5 MB | 2026-07-30 | 51 ms | 80 ms | 271 ms |
| Konscious Argon2 1.3.1 | MIT | C# puro, 36 KB | 2024-06-19 | 69 ms | 105 ms | 362 ms |
| Isopoh Argon2 2.0.0 | CC BY 4.0 | C# puro | 2023-08-17 | 73 ms | 97 ms | 371 ms |

- **Elegida: NSec.Cryptography**, que envuelve libsodium. Trae binario nativo para Windows x64, x86 y arm64, Linux (incluido musl y ARM) y Mac; 452 KB en Windows.
- **Descartado: BouncyCastle**, 2.7 veces más lenta y 5 MB de criptografía que no se usa. **Descartado: Konscious**, sin release desde 2024 y 21 incidencias abiertas. **Descartado: Geralt**, mismo libsodium con comunidad más chica. **Descartado: Isopoh**, cuya licencia no es de software.
- **Descartado: PBKDF2 de la biblioteca estándar.** No pide librería de terceros, pero resiste peor los ataques con tarjetas gráficas.
- **Parámetros: 19 MiB de memoria, 2 pasadas, 1 hilo** (mínimo de OWASP). Se guardan con cada hash, así que se pueden subir después. **Descartado: 46 MiB con 1 pasada** y **64 MiB con 3 pasadas**, por el peso en equipos modestos.
- **La librería se referencia en el núcleo** (`src/Core`), fuera de `Osme.Core.Contracts`: los módulos no la ven. **Descartado: una interfaz en el núcleo con la implementación en el Host.**

### 3. Sesión: galleta con token y tabla de sesiones

El servidor entrega un token aleatorio de **32 bytes** en una galleta `HttpOnly; Secure; SameSite=Strict` y guarda en `core.session` **solo su SHA-256**. Cada petición consulta la sesión y la cuenta.

- **Descartado: guardar el token tal cual.** Quien leyera la base o un respaldo podría suplantar sesiones abiertas.

- **Descartado: JWT firmado.** No se puede revocar al instante, y quien fue desactivado seguiría entrando hasta que el token venza.
- **Duración: 12 horas sin uso y tope de 7 días.** **Descartado: 8 horas con tope de un día** y **30 días sin uso**.
- **Varias sesiones a la vez: configurable por instalación, de fábrica permitido.** Es un ajuste único de la instalación, que vale para todos los usuarios en todos los módulos y cambia quien tenga la vista Ajustes de acceso. Se guarda en la tabla `core.setting` y aplica al instante. **Descartado: columna en la empresa** y **archivo de configuración.** Cuando no se permite, entrar en un equipo cierra las demás sesiones de la cuenta. **Descartado: fijo en varias, fijo en una, por rol y por cuenta.**
- Salir cierra solo la sesión de ese equipo.
- **Falsificación de peticiones entre sitios: `SameSite=Strict`, cuerpo JSON obligatorio en toda escritura y encabezado `Origin` igual al servidor.** **Descartado: además un token anti-falsificación por sesión.**

### 4. Alta inicial: sin código en la máquina del servidor, con código desde otra

DataDave-Dev eligió el código de instalación en lugar de aceptar el alta inicial solo desde la propia máquina, que era lo recomendado. Así un servidor sin pantalla se puede dar de alta desde otro equipo.

- Desde la máquina del servidor **no se pide código** (camino del instalador de Windows). **Descartado: pedirlo siempre.**
- Desde otra máquina se pide un código de **8 caracteres fáciles de dictar** (`K7QM-2XPD`), que el servidor genera en cada arranque mientras no haya empresa y escribe en su consola y su registro. Usa **mayúsculas y números sin los que se confunden** (sin 0, O, 1, I ni L), se acepta sin distinguir mayúsculas y se muestra en dos grupos de cuatro. **Descartado: solo números.** **Tras 5 códigos equivocados** deja de aceptarlo hasta reiniciar, que genera otro. **Descartado: código de 32 caracteres sin límite de intentos.**
- **Descartado: aceptar el alta inicial desde cualquier máquina sin código.** El primero en abrir la dirección se quedaría con la instalación.
- El alta crea empresa y administrador en una transacción o nada, y ocurre una sola vez.
- Al terminar, **el administrador queda dentro**, con sesión iniciada. **Descartado: mandarlo a la pantalla de entrada.**

### 5. Conexión cifrada con un certificado propio de la instalación

El servidor escucha en todas las interfaces, solo por HTTPS, con un certificado autofirmado que genera en el primer arranque y conserva.

- **Descartado: abrir a la red por HTTP** y **seguir en la propia máquina hasta el instalador.**
- **Llave ECDSA P-256.** **Descartado: RSA 2048.**
- **Lleva el nombre de la máquina y `localhost`.** **Descartado: agregar las direcciones IP**, que obligarían a regenerarlo cuando la red las cambie.
- **Dura 2 años y se renueva solo** cuando falten menos de 30 días; al renovarse, cada navegador muestra la advertencia otra vez. **Descartado: 10 años**, que algunos dispositivos rechazan.
- **Un solo puerto: quien escriba `http://` no conecta.** La redirección llega con el instalador (`c0-5-1`). **Descartado: segundo puerto que redirige, en este change.**
- Se guarda en una **carpeta de datos configurable** (`Osme:DataPath`; de fábrica `data` junto al servidor). El instalador fija la definitiva. **Descartado: la carpeta estándar de cada sistema operativo desde ahora.**

### 6. Los roles son datos editables; el paquete solo trae roles de fábrica

Respaldo: `documents/decisiones/0020-roles-editables.md`.

**Qué se puede conceder (las vistas).** Cada documento declarado tiene, sin escribir nada, un permiso por acción: ver y dar de alta (las dos que existen hoy). El núcleo aporta tres vistas propias: **Usuarios, Roles y Ajustes de acceso**.

**Clave de un permiso: `paquete.documento.acción`** (`itdesk.ticket.read`, `itdesk.ticket.create`); las vistas del núcleo son `core.users`, `core.roles` y `core.access_settings`. El alcance va en columna aparte. **Descartado: paquete, documento y acción en tres columnas.**

**Quién lo concede (los roles).** Un rol es una fila con sus permisos. Quien tiene la vista Roles crea roles, les cambia el nombre, marca qué vistas ve cada uno y los desactiva.

- **Un rol puede mezclar vistas de varios módulos.** **Descartado: un rol por módulo.**
- **Usuarios y Roles son vistas concedibles** a cualquier rol. Se recomendaba dejarlas solo al administrador; DataDave-Dev eligió que se puedan conceder.
- Cada una se marca con **una sola casilla**, que da consultar y modificar. **Descartado: ver y modificar por separado.**
- **Dar de alta exige ver**, al menos los propios; la pantalla lo marca sola y el servidor rechaza lo contrario. **Descartado: alta sin ver.**
- **El nombre de un rol no se repite nunca**, ni contra roles desactivados, sin distinguir mayúsculas. **Descartado: único solo entre activos** y **sin restricción.**
- **Un rol se desactiva, no se borra.** **Descartado: borrar cuando nadie lo tiene** y **borrar siempre.**

**Roles de fábrica.** Un módulo los declara con **atributos de C#** junto a `[assembly: Package]`. **Descartado: un archivo JSON en el paquete.** La primera vez que el paquete se carga, el núcleo los copia a la tabla de roles; desde ahí se editan como cualquiera, y una actualización del módulo no los vuelve a escribir.

```
Técnico de sistemas   ve todos los tickets, levanta tickets
Solicitante           ve solo sus tickets, levanta tickets
```

- **No hay "restaurar valores de fábrica"** en este change. **Descartado: botón de restaurar.**
- **El rol de administrador es intocable:** concede todo, incluidos los módulos que se carguen después; no se edita, no se renombra ni se desactiva. **Descartado: editable con candado** y **editable como cualquiera.**
- Negar es el valor de fábrica: una vista que ningún rol concede solo la ve el administrador.
- Un permiso sobre un documento cuyo paquete ya no está cargado no concede nada y no se muestra al editar; se conserva.

### 7. Alcance: todos o propios, y vale el más amplio

Ver un documento se concede con alcance a todos los registros o solo a los que creó la propia cuenta.

- **El solicitante ve solo sus tickets.** **Descartado: que no vea ninguno hasta el portal** y **que vea todos.**
- **Con dos roles de alcance distinto vale el más amplio:** un rol nunca quita lo que otro da. **Descartado: el más restringido.**
- El filtro lo escribe el motor en la consulta, con parámetro; nunca el módulo.

### 8. Sin permiso de ver, la respuesta es "no encontrado"

| Caso | Respuesta |
|---|---|
| Sin sesión | falta iniciar sesión |
| Instalación sin empresa | falta el alta inicial |
| Debe cambiar contraseña | debe cambiar contraseña |
| Sin permiso de ver el documento | no encontrado, igual que un documento que no existe |
| Registro ajeno con alcance propios | no encontrado |
| Puede ver pero no dar de alta | sin permiso |

- **Descartado: responder "sin permiso" también para lo que no se puede ver.** Más claro para el usuario, pero revela qué documentos y registros existen.
- En cada alta el motor llena autor y empresa; quien llama no puede fijarlos.
- **Los tickets del change anterior, sin autor, se conservan así:** los ve quien puede ver todos, no un solicitante. **Descartado: asignarlos al administrador al completar el alta inicial.**

### 9. Contraseñas e intentos fallidos

- **Regla clásica:** de 8 a 128 caracteres con mayúscula, minúscula, número y símbolo. Se recomendaba longitud mínima sin composición (pauta de NIST); DataDave-Dev eligió la clásica.
- **Tras 5 fallos seguidos, la cuenta espera 15 minutos; se desbloquea sola, o antes quien tenga la vista Usuarios.** **Descartado: solo sola** y **solo por un administrador**, que dejaría fuera a cualquiera con cinco intentos ajenos.
- Usuario inexistente, contraseña equivocada, cuenta desactivada y cuenta bloqueada responden igual.
- **Cambiar la contraseña propia pide la actual** y cierra las sesiones de esa cuenta en otros equipos. **Descartado: no pedirla.**

### 10. Cuentas: quién puede qué

- Quien tiene la vista Usuarios crea cuentas, asigna roles, desactiva, reactiva, desbloquea y pone contraseñas temporales.
- **La contraseña que pone otra persona es temporal:** al entrar con ella solo se puede cambiar. **Descartado: generada por el sistema** y **definitiva.**
- **Quien tiene la vista Usuarios puede dar y quitar cualquier rol, incluido el de administrador.** Se recomendaba que solo un administrador lo hiciera; DataDave-Dev eligió que baste la vista. Consecuencia aceptada: conceder la vista Usuarios equivale a conceder el poder de nombrar administradores.
- **Nadie cambia sus propios roles ni se desactiva a sí mismo;** lo hace otra cuenta. **Descartado: permitirlo salvo quitarse el de administrador**, y **permitir la autobaja si queda otro administrador.**
- La instalación conserva siempre al menos una cuenta activa con el rol de administrador.
- **Una cuenta sin roles entra y ve un aviso** de que no tiene nada asignado. **Descartado: exigir al menos un rol.**
- **Administrador único que olvida su contraseña: nada en este change.** Llega con el instalador (`c0-5-1`). **Descartado: un comando en el servidor desde ahora.**

### 11. Los eventos de acceso van al registro del servidor

Entradas, fallos, bloqueos, salidas y cambios de cuentas, roles y ajustes se escriben en el registro del servidor, sin contraseña ni token. Se mudan a la bitácora cuando nazca (`c0-3-1`).

- **Descartado: tabla de auditoría propia desde este change.**

### 12. Rutas planas por recurso

```
GET    /api/session              estado: falta el alta inicial | sin sesión | cuenta, roles y permisos
POST   /api/session              entrar
DELETE /api/session              salir
POST   /api/session/password     cambiar la contraseña propia
POST   /api/setup                alta inicial
GET    /api/users                cuentas
POST   /api/users                crear cuenta
PATCH  /api/users/{id}           roles, activar, desactivar, desbloquear, contraseña temporal
GET    /api/roles                roles con sus permisos
POST   /api/roles                crear rol
PATCH  /api/roles/{id}           nombre, permisos, activar, desactivar
GET    /api/permissions          vistas concedibles
GET    /api/settings/access      ajustes de acceso
PATCH  /api/settings/access      cambiar ajustes de acceso
```

- **Descartado: agruparlas bajo `/api/core`**, y **entrar, salir y alta inicial bajo `/api/auth`.**

## Risks / Trade-offs

- **La vista Usuarios permite nombrar administradores** (decisión 10). → Decisión de DataDave-Dev. La pantalla de roles lo dice junto a esa casilla, y cada asignación del rol de administrador va al registro.
- **La vista Roles permite ampliar un rol que uno mismo tiene.** → Misma naturaleza que el anterior; queda en el registro.
- **El código de instalación viaja a quien lo lee en el registro.** → Solo sirve mientras no hay empresa, cambia en cada arranque y se agota a los 5 fallos.
- **La advertencia del certificado enseña a "continuar de todas formas".** → README con captura; se quita en `c0-5-1`.
- **Argon2id trae una dependencia nueva al núcleo, con binario nativo** por sistema operativo. → La publicación autocontenida lo empaqueta; lo comprueba el change del instalador (`c0-5-1`) en Windows.
- **Las pruebas existentes de la API dejan de pasar** porque toda ruta exige sesión. → `TestSupport` gana un cliente que hace el alta inicial y entra.
- **El permiso se resuelve en un solo punto:** si falla, falla para todo. → Una prueba de integración por cada fila de la tabla de la decisión 8, y revisión de seguridad antes de archivar.
- **El change pasa de un sprint.** → Decidido llevarlo completo en `0-2-1`; conserva su clave. **Descartado: partir la edición de roles a un `0-2-2`.**

## Migration Plan

1. Al arrancar, el núcleo crea sus tablas en `core` si no existen, antes de cargar paquetes.
2. El generador de estructura agrega las llaves foráneas que falten en las tablas de los paquetes.
3. Una base del change anterior arranca sin empresa: pide el alta inicial y sus tickets quedan sin autor.
4. Actualizar `base-de-datos.md` sección 12 para quitar la nota de "quedan sin llave foránea hasta el change 0-2-1".

Vuelta atrás: el código anterior ignora el esquema `core` y las llaves foráneas nuevas no le estorban.

## Open Questions

Ninguna. Las ocho que había se resolvieron el 2026-11-27 (decisiones 1, 2, 3, 4, 5, 6 y 12).
