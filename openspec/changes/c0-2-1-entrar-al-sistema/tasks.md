# Tareas · entrar al sistema

Ordenadas por dependencia real: decisiones pendientes → tablas del núcleo, contraseña y sesión → primer arranque → roles y permisos → motor → cuentas → TI interno → red cifrada → interfaz → punta a punta. Pruebas primero; todo lo que toca la base se prueba contra Postgres real. Cada tarea se cierra con un comando o una comprobación visible.

## 1. Decisiones pendientes

- [x] 1.1 Comparativa de librerías de Argon2id: licencia compatible con LGPL, mantenimiento, código nativo o no, sistemas donde corre, y parámetros recomendados por OWASP medidos en esta máquina. Cierra: tabla comparativa presentada y decisión de DataDave-Dev (librería y parámetros) anotada en `design.md`.
  - 2026-11-27: cinco librerías medidas con .NET 10; elegida NSec.Cryptography con 19 MiB, 2 pasadas, 1 hilo. Tabla en `design.md`, decisión 2.
- [x] 1.2 Resolver con DataDave-Dev el resto de las preguntas abiertas de `design.md` (dónde vive el ajuste de sesiones, rutas, clave de permiso, certificado, token, caracteres del código, longitudes máximas). Cierra: la sección "Open Questions" de `design.md` queda vacía y cada respuesta anotada como decisión.
  - 2026-11-27: respondidas todas; "Open Questions" quedó vacía.

## 2. Tablas del núcleo, contraseña y sesión

- [x] 2.1 Crear al arrancar, antes de cargar paquetes, el esquema `core` con `company`, `user_account`, `role`, `role_permission`, `role_user_account`, `session` y `setting`, según `base-de-datos.md`, y el rol de administrador del sistema una sola vez. Cierra: prueba contra Postgres real que arranca dos veces sobre una base vacía, encuentra las siete tablas y exactamente un rol del sistema.
  - 2026-11-27: `CoreSchemaTests` pasa contra PostgreSQL real (dos arranques, siete tablas, un rol del sistema, columnas acordadas, unicidad sin distinguir mayúsculas); `dotnet test` completo: 56 de 56.
- [ ] 2.2 Reglas de contraseña (8 a 128, mayúscula, minúscula, número, símbolo; el rechazo nombra la regla con un código) y hash con Argon2id por NSec.Cryptography (19 MiB, 2 pasadas, 1 hilo), con los parámetros guardados en el hash. Cierra: pruebas unitarias, una por regla, más verifica la correcta, rechaza otra, y dos hashes de la misma contraseña difieren.
- [ ] 2.3 Sesiones: crear, resolver, vencer a las 12 horas sin uso y a los 7 días, cerrar una, cerrar todas las de una cuenta, y el ajuste de la instalación que permite o no varias a la vez (de fábrica, sí). Cierra: pruebas contra Postgres real con reloj inyectado, una por caso, incluida la de segunda entrada que cierra la primera cuando el ajuste no permite varias.
- [ ] 2.4 Entrar: usuario sin distinguir mayúsculas; misma respuesta para usuario inexistente, contraseña equivocada, cuenta desactivada y bloqueada; 5 fallos frenan 15 minutos; acierto reinicia la cuenta. Cierra: pruebas contra Postgres real para cada escenario de "Se entra con usuario y contraseña" y "Los intentos fallidos repetidos frenan la cuenta".

## 3. Primer arranque

- [ ] 3.1 Alta inicial en una transacción con candado: crea empresa y administrador o nada; falla si ya hay empresa. Cierra: pruebas contra Postgres real, incluida la de dos altas simultáneas que deja una sola empresa.
- [ ] 3.2 Rutas de estado y de alta inicial; código de instalación de 8 caracteres generado en cada arranque sin empresa y escrito en consola y registro; sin código desde la propia máquina, con código desde otra, y bloqueo tras 5 equivocados hasta reiniciar; sin empresa, todo lo demás responde que falta el alta inicial. Cierra: pruebas de integración para cada escenario de `specs/primer-arranque`; de paso verifica el supuesto de que el servidor de pruebas puede simular otra máquina.
- [ ] 3.3 Rutas de entrar, salir y cambiar la contraseña propia (pide la actual y cierra las otras sesiones), con galleta `HttpOnly; Secure; SameSite=Strict` y comprobación de `Origin` y cuerpo JSON en toda petición que cambia datos. Cierra: pruebas de integración de entrar, salir, sesión vencida y petición con `Origin` ajeno rechazada.
- [ ] 3.4 Cliente de pruebas en `TestSupport` que hace el alta inicial y entra, y adaptar las pruebas existentes de la API a usarlo. Cierra: `dotnet test` vuelve a pasar completo.

## 4. Roles y permisos

- [ ] 4.1 Catálogo de permisos: ver y dar de alta por cada documento de los paquetes cargados, más las tres vistas del núcleo (Usuarios, Roles, Ajustes de acceso). Cierra: prueba unitaria sobre los paquetes de prueba.
- [ ] 4.2 Declaración de roles de fábrica con atributos en `Osme.Core.Contracts` y su lectura al cargar el paquete; un paquete con un rol sobre un documento ajeno o inexistente no se carga y queda registrado. Cierra: prueba de integración con un paquete de prueba válido y uno que viola la regla.
- [ ] 4.3 Sembrado de roles de fábrica: se copian a `core.role` la primera vez, por origen, y nunca se vuelven a crear ni se sobrescriben. Cierra: prueba contra Postgres real que edita uno, desactiva otro, vuelve a cargar y los encuentra igual.
- [ ] 4.4 Resolución de permisos de una cuenta: suma de sus roles activos, alcance más amplio, administrador todo, permisos de paquetes no cargados ignorados. Cierra: pruebas contra Postgres real, una por escenario de "Lo que ningún rol concede está negado", "Un permiso de ver alcanza...", "El administrador puede todo..." y "Un permiso de un paquete que ya no está cargado...".
- [ ] 4.5 Rutas del catálogo de permisos y de roles (listar, crear, editar) para quien tiene la vista Roles: nombre único entre todos los roles, permisos solo del catálogo, dar de alta exige ver, desactivar y reactivar sin borrar, rol de administrador intocable. Cierra: pruebas de integración para cada escenario de `specs/permisos-por-modulo` sobre crear, editar y desactivar, incluidos los rechazos a quien no tiene la vista.

## 5. El motor aplica el permiso

- [ ] 5.1 Un solo punto en la API de documentos que resuelve cuenta, permiso y alcance, con las respuestas de la tabla de la decisión 8; la lista de documentos y la de paquetes exigen sesión, y la de documentos trae solo los que la cuenta puede ver. Cierra: pruebas de integración, una por fila de la tabla y una por escenario de "La API de documentos exige sesión y permiso".
- [ ] 5.2 El motor llena `company_id`, `created_by` y `updated_by` en cada alta e ignora los enviados; la lista y la consulta por identificador filtran por autor cuando el alcance es propios, con parámetro. Cierra: pruebas contra Postgres real de "Todo registro guarda quién lo creó..." y de registro ajeno que responde no encontrado.
- [ ] 5.3 El generador de estructura agrega las llaves foráneas hacia `core.company` y `core.user_account` cuando falten, sin tocar registros con esas columnas vacías. Cierra: prueba contra Postgres real sobre una tabla creada con el generador anterior y con registros sin autor.

## 6. Cuentas

- [ ] 6.1 Rutas de cuentas para quien tiene la vista Usuarios: crear con usuario (3 a 50, letras, números, punto, guion y guion bajo), nombre, contraseña temporal y roles; cambiar roles de otra cuenta, incluido el de administrador; desactivar, reactivar, desbloquear y poner contraseña temporal. Ninguna respuesta trae la contraseña ni su hash. Cierra: pruebas de integración para cada escenario de "Quien tiene la vista Usuarios crea cuentas...", "Desbloqueo desde la pantalla de usuarios" y "La contraseña no aparece en ninguna respuesta".
- [ ] 6.2 Contraseña temporal: quien entra con ella solo puede cambiarla; restablecer y desactivar borran las sesiones de la cuenta. Cierra: pruebas de integración de "La contraseña que pone otra persona es temporal" y "Desactivar a quien tiene sesión abierta".
- [ ] 6.3 Nadie cambia sus propios roles ni se desactiva a sí mismo, y no se puede desactivar ni quitar el rol al último administrador activo. Cierra: pruebas de integración, una por regla, que lo intentan y encuentran la cuenta igual.
- [ ] 6.4 Ruta del ajuste de varias sesiones para quien tiene la vista Ajustes de acceso. Cierra: pruebas de integración de "La instalación decide si una cuenta puede tener varias sesiones a la vez".
- [ ] 6.5 Eventos de acceso al registro del servidor (entradas, fallos, bloqueos, salidas, cambios de cuentas, roles y ajustes, y cada asignación del rol de administrador), sin contraseña ni token. Cierra: prueba de integración que revisa el registro tras una entrada fallida y comprueba que la contraseña enviada no aparece.

## 7. TI interno

- [ ] 7.1 Declarar en `ItDesk` los roles de fábrica técnico de sistemas y solicitante. Cierra: tras arrancar, la lista de roles pedida como administrador los muestra con sus permisos.
- [ ] 7.2 Escenarios de `specs/tickets-de-ti` por la API: técnico ve todos, solicitante solo los suyos, ticket ajeno no encontrado, cuenta sin rol no ve el documento, rol a la medida que solo ve. Cierra: pruebas de integración, una por escenario.

## 8. Red local cifrada

- [ ] 8.1 Certificado propio de 2 años: generarlo en el primer arranque, guardarlo en la carpeta de datos configurable (`Osme:DataPath`) con permisos restringidos, reutilizarlo y regenerarlo cuando falten menos de 30 días. Cierra: prueba que arranca dos veces y obtiene la misma huella; verifica el supuesto de Kestrel.
- [ ] 8.2 El servidor escucha en todas las interfaces solo por HTTPS, en un solo puerto; Vite reenvía `/api` sin validar el certificado. Cierra: `curl -k` por `https://` a la IP de red responde el estado, y por `http://` no entrega datos.

## 9. Interfaz

- [ ] 9.1 Arranque por estado: la interfaz pide el estado y muestra alta inicial (con campo de código cuando el servidor lo exige), entrada, cambio de contraseña, aviso de cuenta sin roles o la aplicación; una respuesta de falta de sesión en cualquier pantalla lleva a la entrada. Cierra: pruebas de componente, una por estado.
- [ ] 9.2 Pantallas de alta inicial, entrada y cambio de contraseña, con las reglas de contraseña visibles, errores por campo, etiquetas asociadas y uso por teclado; textos en `messages.ts`. Cierra: pruebas de componente de aceptado y rechazado para cada una.
- [ ] 9.3 Barra con el nombre de la cuenta y salir; "Usuarios", "Roles" y "Ajustes de acceso" solo para quien tiene cada vista; la lista de documentos sale del servidor y "nuevo" solo se ofrece con permiso de alta. Cierra: pruebas de componente con una cuenta administradora, una con solo ver y una sin roles.
- [ ] 9.4 Pantalla de usuarios: lista, crear, roles, desactivar, reactivar, desbloquear, contraseña temporal; sin acciones sobre la cuenta propia. Cierra: pruebas de componente por acción.
- [ ] 9.5 Pantalla de roles: lista, crear, renombrar, desactivar y reactivar; por módulo, cada documento con ver (todos o propios) y dar de alta, y las tres vistas del núcleo con una casilla, todo dibujado desde el catálogo del servidor; marcar dar de alta marca ver propios; aviso junto a Usuarios de que permite nombrar administradores; el rol de administrador se muestra sin edición. Cierra: pruebas de componente por acción y una que agrega un documento al catálogo simulado y lo ve aparecer.
- [ ] 9.6 Pantalla de ajustes de acceso con el ajuste de varias sesiones. Cierra: prueba de componente que lo cambia y lo ve guardado.

## 10. De punta a punta

- [ ] 10.1 Prueba de navegador sobre una base vacía: alta inicial desde la propia máquina → crear un rol a la medida → crear una cuenta con rol solicitante → salir → entrar con la temporal → cambiarla → levantar un ticket → no ver el del administrador. Cierra: la prueba pasa contra el servidor y Postgres reales, por HTTPS.
- [ ] 10.2 Sobre una base vacía, hacer el alta inicial desde otro equipo o celular de la red local con el código del registro, y entrar. Cierra: comprobación visible, anotada en este archivo con fecha.
- [ ] 10.3 Actualizar `documents/arquitectura/base-de-datos.md` sección 12 (las llaves foráneas ya existen) y el README (dirección HTTPS, advertencia del certificado con captura, alta inicial y dónde ver el código de instalación). Cierra: una persona sigue el README en limpio y llega a la pantalla de alta inicial.
- [ ] 10.4 Revisión de seguridad del change (contraseñas, sesiones, permisos, alta inicial, certificado), corrigiendo lo crítico y lo alto. Cierra: el informe queda sin hallazgos críticos ni altos, anotado en este archivo con fecha.
- [ ] 10.5 Anotar en `design.md` qué supuestos quedaron verificados y cuáles cambiaron. Cierra: la sección de contexto ya no dice que nada está verificado.
