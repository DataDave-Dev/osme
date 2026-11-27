> **Clave 0-2-1** · fase 0 · sprint 2 · change 1

## Why

Tras `c0-1-1-un-ticket-de-punta-a-punta` el servidor solo atiende a su propia máquina porque no sabe quién llama. Sin cuentas no hay empresa, no hay permisos y no se puede abrir a la red. `documents/decisiones/0014-usuarios-compartidos.md` fija que los usuarios viven en el núcleo y que cada módulo declara sus permisos; este change lo hace real.

Segundo change de la entrega 0.1 (`documents/fases/fase-00-base-ti-interno/diseno.md`, sección 6).

## What Changes

- **Primer arranque**: si la instalación está vacía, el sistema pide crear la empresa y la cuenta de administrador antes de cualquier otra cosa. Desde otra máquina pide el código de instalación que el servidor escribe en su registro.
- **Inicio y cierre de sesión** con usuario y contraseña.
- **Cada módulo declara sus permisos y sus roles de fábrica**; el núcleo los aplica en la API de documentos, una vez, para todos los documentos.
- **Los roles son editables**: el administrador crea roles nuevos y decide a qué vistas da acceso cada uno, en cualquier módulo (`documents/decisiones/0020-roles-editables.md`).
- **Administración mínima de usuarios**: crear, desactivar, desbloquear y asignar roles. Usuarios, Roles y Ajustes de acceso son vistas que se conceden a un rol.
- **El servidor se abre a la red local** al existir el acceso; se retira la restricción a la propia máquina.
- **TI interno trae sus primeros roles de fábrica**: técnico de sistemas y solicitante.

## Historias

1. Como quien instala OSME, quiero que el primer arranque me pida crear mi empresa y mi cuenta de administrador, para no quedar con una instalación abierta ni con una contraseña de fábrica.
2. Como usuario, quiero entrar con mi usuario y contraseña y salir cuando termine, para que nadie más use mi cuenta.
3. Como administrador, quiero crear cuentas y asignarles roles, para decidir quién puede hacer qué.
4. Como administrador, quiero desactivar una cuenta, para que quien se va pierda el acceso a todo de una vez.
5. Como autor de un módulo, quiero declarar los permisos y roles de mi módulo junto a sus documentos, para no escribir comprobaciones de acceso en cada operación.
6. Como administrador, quiero crear roles y marcar qué vistas ve cada uno, para que el acceso siga la forma de trabajar de mi empresa y no la que trae el módulo.
7. Como técnico de sistemas, quiero ver y levantar tickets según mi rol, y que quien no tiene permiso no vea ni la lista.

## Fuera de alcance

| Qué | A dónde va |
|---|---|
| Verificación en dos pasos | entrega 0.2 |
| Permisos por campo o por estado del documento | entrega 0.5 |
| Varias empresas, sucursales, departamentos, empleados | entrega 0.2 |
| Portal del empleado y cuentas de portal | entrega 0.3 |
| Recuperar contraseña por correo | entrega 0.3, cuando exista el correo |
| Entrar con Google/Microsoft o directorio de Windows | fuera de la fase 0 |

## Preguntas abiertas

- **Conexión cifrada en red local.** `diseno.md` 2.4 la pide de fábrica. Resuelta el 2026-11-27: certificado propio de la instalación (`design.md`, decisión 5). El nombre fijo queda para `c0-5-1`.
- **Recuperar la contraseña sin correo.** Resuelta el 2026-11-27: quien tiene la vista Usuarios pone una contraseña temporal (`design.md`, decisión 10). El administrador único que olvida la suya queda para `c0-5-1`.

Las preguntas que siguen abiertas están en `design.md`, sección "Open Questions", sin valor asumido.

## Capabilities

### New Capabilities

- `primer-arranque`: qué pide una instalación vacía y qué queda prohibido hasta completarlo.
- `sesion-y-cuentas`: inicio y cierre de sesión, alta y baja de cuentas, reglas de contraseña.
- `permisos-por-modulo`: cómo un paquete declara permisos y roles de fábrica, cómo el administrador crea y edita roles, y cómo el núcleo los aplica.

### Modified Capabilities

- `motor-de-documentos`: la API de documentos exige sesión y permiso; se retira el requisito de atender solo a la propia máquina.
- `tickets-de-ti`: quién puede levantar y quién puede ver tickets.

## Impact

- Toca el núcleo (cuentas, sesión, permisos) y la API de documentos. Primer cambio a requisitos ya archivados.
- Seguridad: contraseñas, sesiones y control de acceso. Requiere revisión de seguridad antes de archivar.
