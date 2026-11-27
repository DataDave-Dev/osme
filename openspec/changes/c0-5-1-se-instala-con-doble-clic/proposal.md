> **Clave 0-5-1** · fase 0 · sprint 5 · change 1

## Why

La instalación sencilla es el primer diferenciador de OSME (`documents/arquitectura/arquitectura.md`, sección 2) y `documents/hoja-de-ruta/hoja-de-ruta.md` pide una versión mínima del instalador desde la fase 0, para no descubrir tarde que algo no se deja empaquetar. Hasta este change, OSME solo corre en la máquina de quien lo desarrolla.

Quinto y último change de la entrega 0.1 (`documents/fases/fase-00-base-ti-interno/diseno.md`, secciones 2.5 y 6). Al archivarlo, la entrega 0.1 está completa: instalar, crear empresa, entrar y atender un ticket.

## What Changes

- **Instalador de Windows de doble clic** que deja funcionando el servidor, su base de datos y el servicio que lo arranca con la máquina.
- **Al terminar abre el navegador** en el primer arranque, listo para crear la empresa.
- **Los paquetes se instalan desde archivo**, desde la pantalla de administración, sin línea de comandos. TI interno viene incluido.
- **Verificación de firma de paquetes**: los oficiales van firmados por el proyecto; uno sin firma se puede instalar con advertencia clara.
- **Desinstalar** quita el programa y pregunta qué hacer con los datos.
- **La compilación del instalador es automática** desde el repositorio.
- **Sin firma de código por ahora** (`documents/decisiones/0018-firma-del-instalador.md`): la página de descarga y el README explican el aviso de Windows con captura.

## Historias

1. Como dueño de una empresa sin área de sistemas, quiero instalar OSME con doble clic y que al terminar se abra listo para usar, sin instalar nada más por separado.
2. Como usuario de la oficina, quiero que OSME siga disponible después de reiniciar la máquina donde se instaló.
3. Como administrador, quiero instalar un módulo o una extensión desde un archivo en la pantalla de administración.
4. Como administrador, quiero que el sistema me advierta si un paquete no viene firmado por el proyecto, para decidir si confío en él.
5. Como administrador, quiero desinstalar OSME sin perder mis datos por accidente.
6. Como quien descarga el instalador, quiero entender el aviso de Windows y saber cómo continuar.

## Fuera de alcance

| Qué | A dónde va |
|---|---|
| Actualizador con un clic | entrega 0.7 |
| Respaldos automáticos y restauración | entrega 0.7 |
| Paquetes de servidor para Linux y Mac | entrega 0.7 |
| Tienda de extensiones | fase 15 |
| Firma de código del instalador | se retoma al terminar la 0.1 (`decisiones/0018`) |

Aunque el actualizador llega después, este change instala de forma que actualizar no pierda datos: programa y datos en carpetas separadas.

## Preguntas abiertas

- **Si la máquina ya tiene un PostgreSQL instalado.** Asumido: OSME instala el suyo, aparte, en otro puerto, y no toca el existente.
- **Puerto ocupado.** Asumido: el instalador prueba el puerto y propone otro si está en uso.
- **Acceso desde otras máquinas de la red.** Asumido: el instalador abre el puerto en el cortafuegos de Windows preguntando antes, y muestra al final la dirección para entrar desde otra máquina.
- **Tecnología del instalador.** Queda al constructor; se decide en `design.md`.

## Capabilities

### New Capabilities

- `instalacion-en-windows`: qué deja funcionando el instalador, qué pregunta, y qué pasa al desinstalar.
- `instalar-paquetes-desde-archivo`: cómo un administrador instala un paquete y qué se comprueba antes de aceptarlo.

### Modified Capabilities

- `carga-de-paquetes`: los paquetes ya no solo se descubren al arrancar; se instalan en ejecución y se verifica su firma.

## Impact

- Nuevo proyecto de instalador y nuevo flujo de compilación.
- Instalar paquetes en ejecución ejecuta código de terceros: requiere revisión de seguridad antes de archivar.
- Pruebas en máquinas Windows limpias, que hoy no existen en el entorno de desarrollo (Linux).
