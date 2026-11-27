# Tareas · un ticket de punta a punta

Ordenadas por dependencia real: primero las herramientas, luego la frontera, después núcleo → módulo → interfaz. Cada tarea se cierra con un comando o una comprobación visible.

## 1. Herramientas y esqueleto

- [x] 1.1 Instalar el SDK de .NET en su versión LTS vigente y anotar la versión en `global.json`. Cierra: `dotnet --version` imprime esa versión.
- [x] 1.2 Crear la solución con los proyectos `src/Core`, `src/Host`, `src/Modules/ItDesk` y `tests/`. El Host no referencia a ItDesk. Cierra: `dotnet build` termina sin errores ni advertencias.
- [x] 1.3 Postgres de desarrollo con `docker compose` y cadena de conexión por variable de entorno, con archivo de ejemplo. Cierra: `docker compose up -d` y `psql` conecta con la cadena del ejemplo.
- [x] 1.4 Crear `src/web` con Vite + React + TypeScript y pnpm. Cierra: `pnpm --dir src/web build` termina sin errores.
- [x] 1.5 Licencia LGPL y README mínimo en la raíz con cómo levantar el entorno. Cierra: una persona sigue el README en limpio y llega al servidor corriendo.

## 2. Frontera entre paquetes

- [x] 2.1 Prueba de arquitectura que falla si un módulo referencia tipos no publicados de otro paquete. Cierra: `dotnet test` pasa; al agregar a propósito una referencia prohibida, falla nombrando módulo y tipo; se retira la referencia.
  - La referencia prohibida quedó fija en un paquete de prueba (`tests/TestPackages/Violator`) en vez de agregarse y retirarse, para que el caso que falla se siga probando.

## 3. Carga de paquetes

- [x] 3.1 Contrato de paquete en lo publicado por el núcleo: manifiesto con nombre, versión y dependencias. Cierra: prueba unitaria que lee el manifiesto de ItDesk.
- [x] 3.2 El Host descubre ensamblados en la carpeta de paquetes y los carga en orden de dependencias, cada uno en su propio contexto. Cierra: prueba de integración con dos paquetes de prueba donde uno depende del otro.
- [x] 3.3 Paquete con dependencia faltante se omite y se registra. Cierra: prueba de integración que revisa la lista de cargados y el registro.
- [x] 3.4 Ruta que lista los paquetes cargados con su versión. Cierra: `curl localhost:<puerto>/api/packages` muestra núcleo e ItDesk.

## 4. Motor de documentos

- [x] 4.1 Atributos de declaración y traducción de una clase al modelo interno de descripción (texto, texto largo, opción, fecha y hora). Cierra: prueba unitaria sobre una clase de ejemplo.
- [x] 4.2 Generador de estructura: crea el esquema del paquete y la tabla si no existen, y agrega columnas faltantes sin tocar las existentes. Nombres, tipos y columnas fijas según `documents/arquitectura/base-de-datos.md`. Cierra: prueba contra Postgres real que arranca dos veces, la segunda con un campo más, y conserva los registros.
- [x] 4.3 Validación contra el modelo: obligatorio, longitud, opciones, campo desconocido, campos de solo lectura ignorados. Cierra: pruebas unitarias, una por regla.
- [x] 4.4 Alta y consultas con SQL parametrizado; identificador UUID versión 7 y fechas asignados por el servidor. Cierra: prueba contra Postgres real que da de alta, consulta por identificador y lista en orden.
- [x] 4.5 Rutas `GET /api/meta/...`, `GET /api/docs/...`, `GET /api/docs/.../{id}`, `POST /api/docs/...` con una sola forma de respuesta para éxito y error. Cierra: pruebas de integración que cubren cada escenario de `specs/motor-de-documentos`.
- [x] 4.6 El servidor escucha solo en `localhost`. Cierra: `curl` desde la máquina responde; `curl` a la IP de red de la misma máquina no conecta.

## 5. Módulo TI interno

- [x] 5.1 Declarar el documento ticket en `ItDesk` con título, descripción y prioridad. Cierra: al arrancar existe la tabla en el esquema `itdesk` y `GET /api/meta/itdesk.ticket` devuelve sus campos.
- [x] 5.2 Escenarios del ticket por la API. Cierra: pruebas de integración de `specs/tickets-de-ti`, incluida la de reinicio.

## 6. Pantallas genéricas

- [x] 6.1 Cliente de la API y un componente por tipo de campo, con etiqueta asociada y uso por teclado. Cierra: pruebas de componente por tipo.
- [x] 6.2 Pantalla de lista genérica con estados de carga, vacío y error. Cierra: pruebas de componente para los tres estados.
- [x] 6.3 Pantalla de alta genérica: controles según descripción, errores por campo, sin doble envío. Cierra: pruebas de componente para alta aceptada, rechazada y doble clic.
- [x] 6.4 El Host sirve la interfaz compilada; Vite redirige la API en desarrollo. Cierra: abrir `http://localhost:<puerto>/` muestra la lista de tickets.

## 7. De punta a punta

- [x] 7.1 Prueba de navegador: levantar un ticket y verlo en la lista. Cierra: la prueba pasa contra el servidor y Postgres reales.
- [x] 7.2 Comprobar que un campo nuevo aparece sin tocar la interfaz: agregar un campo temporal a la declaración, reiniciar, verlo en formulario y lista, y retirarlo. Cierra: comprobación visible, anotada en este archivo con fecha.
  - 2026-11-27: se agregó `Location` ("Ubicación") a `Ticket.cs`, se reinició el servidor y una prueba de navegador temporal lo llenó en el formulario y lo vio como columna de la lista, con `src/web` sin cambios. Campo y prueba retirados.
- [x] 7.3 Flujo de integración continua que compila, corre la verificación de frontera y todas las pruebas. Cierra: el flujo pasa en verde en la primera subida a GitHub.
  - 2026-11-27: el flujo pasó en verde en la primera subida a GitHub (commit `d26cfa3`, ejecución 46065210429).
- [x] 7.4 Anotar en `design.md` qué supuestos quedaron verificados y cuáles cambiaron. Cierra: la sección de contexto ya no dice que nada está verificado.
