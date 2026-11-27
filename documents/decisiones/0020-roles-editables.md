# 0020 — Roles editables y creables por el cliente

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [0014 usuarios compartidos](0014-usuarios-compartidos.md), [0005 adaptación](0005-adaptacion-configuracion-y-extensiones.md), [arquitectura](../arquitectura/arquitectura.md), sección 4.1

Complementa a la 0014; no la reemplaza.

## Decisión

En todos los módulos, los roles son datos del cliente, no código:

1. El administrador **crea roles nuevos** y **edita los existentes**, sin límite y sin programar.
2. Para cada rol decide **a qué vistas da acceso**. Las vistas concedibles salen de lo que cada paquete instalado declara; ningún módulo tiene una lista de roles cerrada.
3. Los roles que un módulo declara son **roles de fábrica**: llegan listos para usar y desde ese momento se editan como cualquier otro.
4. Vale igual para módulos oficiales, localizaciones y extensiones, porque lo aplica el núcleo.

## Por qué

- Es el segundo principio rector: el sistema se adapta al cliente, no el cliente al sistema. Cada empresa reparte el trabajo a su manera.
- Un rol fijo por módulo obliga a elegir entre dar acceso de más o de menos.
- Como los roles son datos, actualizar un módulo no pisa lo que el cliente configuró.

## Consecuencias

- Los roles y sus permisos viven en tablas del núcleo. Un rol puede mezclar vistas de varios módulos.
- Una actualización de un módulo no vuelve a escribir un rol de fábrica que el cliente ya editó o desactivó.
- El rol de administrador es la excepción: no se edita ni se desactiva, para que la instalación nunca quede sin quien la administre.
- Administrar usuarios y administrar roles son también vistas que se conceden a un rol; no son exclusivas del administrador.
- Un rol no se borra: se desactiva. Su nombre no se repite nunca.
- Primer uso: change `0-2-1`. Ahí "vista" es documento y acción (ver, dar de alta), con alcance a todos los registros o solo a los propios. El detalle crece con las acciones y pantallas que cada change agregue.

## Alternativas descartadas

- **Roles fijos declarados por cada módulo**, con roles a la medida como configuración posterior (entrega 0.5): más simple de construir, pero el módulo decide por la empresa cómo se reparte el trabajo.
- **Permisos asignados directo a cada cuenta, sin roles**: flexible, pero con veinte personas nadie sabe quién puede qué.
