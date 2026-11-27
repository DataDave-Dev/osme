# OSME — fase 0: base + TI interno + instalador (diseño)

Fecha: 2026-11-27 · Estado: **aprobado por DataDave-Dev el 2026-11-27** · Relacionado: [arquitectura](../../arquitectura/arquitectura.md), [hoja de ruta](../../hoja-de-ruta/hoja-de-ruta.md)

Todo lo aquí escrito está decidido por DataDave-Dev. La marca **[D]** señala una decisión explícita; ya no quedan propuestas pendientes (autorizadas el 2026-11-27).

## 1. Intención

Primera versión instalable de OSME. Prueba la base (núcleo, motor de documentos, pantallas genéricas) con un módulo real y útil por sí solo: **TI interno**, mesa de ayuda de sistemas e inventario de equipos.

Al terminar la fase: alguien sin conocimientos técnicos instala OSME con doble clic, crea su empresa, y su área de sistemas atiende tickets y controla equipos, adaptando el sistema a su forma de trabajar sin programar.

No hay empresa piloto todavía; se diseña genérico. **[D]**

## 2. Alcance

### 2.1 TI interno — tickets **[D]**

- Básico: levantar, asignar, cambiar estado, comentar, adjuntar, cerrar.
- Categorías y prioridades, con asignación automática a técnico o grupo según categoría.
- Tiempos de atención: plazos de respuesta y solución por prioridad, alertas de vencimiento y medición de cumplimiento.
- Tickets por correo: un correo al buzón de soporte crea el ticket y las respuestas se enlazan solas.
- Base de conocimiento: artículos de soluciones frecuentes.

### 2.2 TI interno — equipos **[D]**

- Registro: tipo, marca, modelo, serie, estado.
- Asignación a personas, con historial y carta responsiva imprimible.
- Licencias de software: cuántas hay, en qué equipos, cuándo vencen.
- Historial por equipo: tickets, reparaciones, mantenimientos y garantías con vencimiento.
- Etiquetas con código QR o de barras, consultables escaneando con el celular.

### 2.3 Portal del empleado **[D]**

El empleado común tiene una cuenta ligera que solo ve el autoservicio; no ve el ERP.

```
┌────────────────────────────────────────────┐
│  OSME · Mesa de ayuda          María López │
├────────────────────────────────────────────┤
│  ¿En qué te ayudamos?  [ buscar solución ] │
│  [ + Levantar ticket ]                     │
│  Mis tickets                               │
│  #142 Impresora no imprime    En proceso   │
│  Mis equipos                               │
│  Laptop Dell 5420 · serie 7XK...           │
└────────────────────────────────────────────┘
```

Levanta tickets (formulario o correo), ve el estado de los suyos, consulta la base de conocimiento y ve sus equipos asignados. El portal es una pieza del núcleo **[D]**, porque se reutiliza en Soporte a clientes (fase 7) y en futuros autoservicios.

### 2.4 Base (núcleo)

- Motor de documentos: declaración → tabla, validación, API pública, formulario, lista, permisos, bitácora de cambios.
- Pantallas genéricas: formularios y listas dibujados a partir de la descripción del documento; adaptables a celular.
- Registro de paquetes: instalar, actualizar y desinstalar módulos y extensiones desde archivo, sin línea de comandos.
- **Usuarios compartidos [D]**: una sola tabla en el núcleo; cada módulo declara sus permisos y roles.
- **Acceso [D]**: usuario y contraseña, con verificación en dos pasos. Sin entrada con Google/Microsoft ni directorio de Windows en esta fase.
- **Organización [D]**: varias empresas, sucursales, departamentos y directorio de empleados.
- **TI interno con varias empresas [D]**: configurable. De fábrica una sola mesa atiende a todas (mismos técnicos, misma base de conocimiento, equipos transferibles entre empresas); se puede separar por empresa.
- **Red [D]**: de fábrica funciona en la red local, con conexión cifrada y un nombre fijo (las etiquetas QR llevan esa dirección impresa). El acceso desde internet es opcional, con guía paso a paso.
- **Firma de paquetes [D]**: la verificación existe desde la 0.1. Los paquetes oficiales van firmados por el proyecto; un administrador puede instalar uno sin firma, con advertencia clara.
- **Idiomas [D]**: español e inglés desde el inicio.
- **Configuración por pantalla [D]**: campos personalizados, flujos de estados con aprobaciones, formatos de impresión/PDF y reglas automáticas ("si pasa X, haz Y").
  - Profundidad en esta fase **[D]**: sólida, sin diseñador visual. Formatos: plantillas editables con logo, textos y campos a insertar. Reglas: se arman eligiendo de listas (cuándo, condiciones, qué hacer). Los diseñadores visuales llegan en fases posteriores.
- **Avisos [D]**: correo, dentro del sistema (campana en tiempo real), Telegram y WhatsApp. WhatsApp por la API oficial de Meta: cada empresa pone su cuenta de negocio verificada, sus plantillas aprobadas y paga sus mensajes. Sin pasarelas no oficiales.
- **Tableros e indicadores [D]**: tickets abiertos, tiempos de atención, equipos por estado. Los trae cada módulo, con filtros; el usuario no arma tableros propios en esta fase.
- Bitácora de eventos y cola de tareas (correo entrante y saliente, alertas de plazos).
- Integración: API pública documentada, webhooks, importar/exportar Excel/CSV.

### 2.5 Instalador **[D]**

- Windows de doble clic: instala programa, base de datos y servicio; al terminar abre el navegador listo para crear la empresa.
- Actualizador: avisa de versión nueva y la instala con un clic, respaldando antes.
- Respaldos automáticos programados y restauración sencilla.
- Linux y Mac: paquete de servidor sencillo (un comando o Docker) que deja todo funcionando, con actualización y respaldos. El instalador gráfico de doble clic es solo para Windows en esta fase.
- Certificado de firma: por ahora no se cuenta con uno **[D]**. El instalador de Windows se publica sin firma; Windows mostrará "Windows protegió su PC" y el usuario continúa con Más información → Ejecutar de todas formas. La página de descarga y el README explican ese paso con captura de pantalla. No bloquea construir ni publicar. Cuando el repositorio sea público y haya algo instalable, solicitar firma gratuita para proyectos open source (SignPath Foundation), confirmando entonces sus requisitos (por evaluar, ver decisión 0018).

## 2.6 Capacidad y criterio de terminado **[D]**

- DataDave-Dev trabaja OSME a tiempo parcial, solo con IA, sin fecha objetivo. Alcance fijo; la fase termina cuando termine.
- La fase 0 se da por terminada con las 7 entregas completas, sin recortar. Inventario no empieza antes.

## 3. No entra en la fase 0

- Diseñador visual de formatos, constructor libre de reglas y tableros armados por el usuario.
- Instalador gráfico de doble clic para Linux y Mac.

- IA (fase 3) y tienda de extensiones (fase 15).
- Cualquier cosa fiscal, contable o de productos.
- Tickets de clientes externos (fase 7, Soporte a clientes).
- Entrada con Google/Microsoft o directorio de Windows.
- App nativa de celular.
- Asistentes para migrar datos desde otros sistemas de tickets; solo importación por Excel/CSV.

## 4. Anti-metas

- Un formulario que se nota "generado": la pantalla genérica debe sentirse moderna y rápida.
- Que adaptar tickets o equipos a una empresa requiera tocar código.
- Que TI interno use algo del núcleo por un atajo que otro módulo no podría usar: lo que necesite, lo ofrece el núcleo para todos.
- Instalación que pida abrir una terminal o instalar algo por separado.

## 5. Enfoques considerados

| | Enfoque | A favor | En contra |
|---|---|---|---|
| A | Construir toda la base primero y el módulo al final | Base "completa" antes de usarla | Meses sin nada que probar; la base se diseña a ciegas |
| **B** | **Entregas verticales**: cada entrega atraviesa instalador, núcleo, pantalla y módulo, y deja algo usable | Cada sprint se puede probar de punta a punta; la base crece solo con lo que un uso real pide | Algunas piezas de la base se retocan al llegar la siguiente entrega |
| C | Módulo primero a mano y extraer la base después | Tickets funcionando muy rápido | Contradice el enfoque de documentos declarados ya aprobado |

**Elegido: B [D]**, coherente con que cada fase funcione de punta a punta.

## 6. Entregas **[D]**

El alcance es de DataDave-Dev; el corte en entregas y su orden también están aprobados. Cada entrega se puede instalar y usar. Cuántos sprints toma cada una se estima al armar el backlog.

| Entrega | Qué funciona al terminar |
|---|---|
| **0.1 Esqueleto que camina** | Instalador mínimo de Windows → crear empresa y administrador → iniciar sesión → ticket básico (levantar, asignar, estados, comentar, cerrar) en pantalla genérica, en español e inglés. Nacen: motor de documentos, registro de paquetes, usuarios y permisos, bitácoras, API pública |
| **0.2 Organización** | Varias empresas, sucursales, departamentos y empleados; verificación en dos pasos; categorías, prioridades y asignación automática |
| **0.3 Portal y avisos** | Portal del empleado; avisos dentro del sistema y por correo; tickets por correo; cola de tareas |
| **0.4 Equipos** | Registro, asignación con historial, licencias, garantías, historial por equipo, etiquetas QR y carta responsiva (primer formato de impresión) |
| **0.5 Configuración** | Campos personalizados, flujos de estados, reglas automáticas, editor de formatos de impresión y tiempos de atención |
| **0.6 Conocimiento y tableros** | Base de conocimiento y tableros de indicadores |
| **0.7 Instalación completa e integración** | Actualizador, respaldos, paquetes de servidor para Linux y Mac; webhooks e importar/exportar; avisos por Telegram y WhatsApp (API oficial) |

Dentro de las entregas **[D]**:

- **WhatsApp y Telegram se construyen como extensiones**, no dentro del núcleo. Dependen de servicios externos con sus propias cuentas y reglas, y así sirven de primera prueba real del mecanismo de extensiones instaladas desde archivo. El núcleo ofrece el punto de extensión "canal de aviso"; correo y campana son canales incluidos.
- El actualizador y los respaldos van en la 0.7, pero desde la 0.1 la instalación se hace de forma que actualizar no pierda datos.
- Tiempos de atención se apoyan en las reglas automáticas y la cola de tareas, por eso llegan en la 0.5 y no antes.

## 7. Queda al constructor

- Librerías, forma exacta de las declaraciones, diseño visual de componentes.
- Tecnología del instalador en cada sistema operativo.
- Protocolo de correo entrante y manejo de adjuntos.
- Detalle de cada pantalla más allá de lo descrito.

## 8. Pendiente de DataDave-Dev

- Corte en entregas (sección 6): aceptar o reordenar.
- WhatsApp y Telegram como extensiones y no dentro del núcleo (sección 6): aceptar o cambiar.
- Certificado de firma del instalador: no disponible por ahora; se publica sin firma. Revisar firma gratuita para open source cuando el repositorio sea público.
