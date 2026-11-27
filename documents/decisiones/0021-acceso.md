# 0021 — Acceso: contraseñas, sesiones y conexión cifrada

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [0014 usuarios compartidos](0014-usuarios-compartidos.md), [0020 roles editables](0020-roles-editables.md), change `0-2-1`

## Decisión

Reglas de seguridad del acceso que heredan todos los changes y módulos:

1. **Contraseñas con Argon2id**, por la librería NSec.Cryptography (libsodium), con 19 MiB de memoria, 2 pasadas y 1 hilo. Los parámetros se guardan con cada hash para poder subirlos.
2. **Regla de contraseña:** de 8 a 128 caracteres, con mayúscula, minúscula, número y símbolo.
3. **Intentos fallidos:** tras 5 seguidos la cuenta espera 15 minutos; se desbloquea sola, o antes desde la pantalla de usuarios.
4. **Sesión por galleta y tabla de sesiones**, sin JWT. Token de 32 bytes; en la base solo su SHA-256. Vence a las 12 horas sin uso y, como tope, a los 7 días.
5. **Varias sesiones a la vez:** ajuste único de la instalación, para todos los usuarios y módulos; de fábrica permitido.
6. **Falsificación de peticiones entre sitios:** galleta `SameSite=Strict`, cuerpo JSON en toda escritura y encabezado `Origin` igual al servidor.
7. **Conexión cifrada siempre**, con un certificado propio de la instalación: ECDSA P-256, nombre de la máquina y `localhost`, 2 años, renovación automática.
8. **Sin permiso de ver, la respuesta es "no encontrado"**, igual que para algo que no existe.
9. **Alta inicial:** sin código desde la máquina del servidor; desde otra, con un código de instalación de 8 caracteres que el servidor escribe en su registro.
10. **La librería de contraseñas vive en el núcleo** y no forma parte de lo que el núcleo publica a los módulos.

## Por qué

- Argon2id resiste mejor que PBKDF2 los ataques con tarjetas gráficas; libsodium es su implementación más auditada y fue la más rápida de las cinco medidas.
- La tabla de sesiones permite cortar el acceso de una cuenta en la petición siguiente, que es lo que pide la decisión 0014.
- Un servidor que se abre a la red no debe mandar contraseñas en claro ni revelar qué existe a quien no puede verlo.

## Alternativas descartadas

El detalle de cada alternativa y la comparativa de librerías con sus mediciones están en `openspec/changes/c0-2-1-entrar-al-sistema/design.md` (tras archivarse, en `openspec/changes/archive/`).

- **PBKDF2 de .NET**: sin dependencia nueva, pero más débil frente a tarjetas gráficas.
- **JWT**: no se puede revocar al instante.
- **HTTP en la red local**: las contraseñas viajarían en claro.
- **Responder "sin permiso" para lo que no se puede ver**: revela qué existe.
