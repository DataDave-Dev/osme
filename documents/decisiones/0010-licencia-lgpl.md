# 0010 — Licencia LGPL

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 2

## Decisión

El núcleo y los módulos oficiales se publican bajo LGPL.

## Por qué

- Quien modifique el núcleo debe publicar sus cambios, pero las extensiones pueden ser cerradas y de pago, lo que hace viable la tienda.
- Es el modelo de Odoo Community.

## Archivos

- `LICENSE`: texto de la GNU Lesser General Public License, versión 3.
- `LICENSE.GPL`: texto de la GNU General Public License, versión 3. La LGPL 3 se define como permisos adicionales sobre la GPL 3 y pide acompañarse de ella.

## Pendiente

Decidir si el código se publica como "solo versión 3" o "versión 3 o posterior". Importa al poner el aviso de licencia en los archivos de código; hasta entonces el repositorio dice solo "LGPL versión 3".

## Alternativas descartadas

- **Apache 2.0 o MIT**: más permisivas: cualquiera podría cerrar su versión, incluido un competidor.
- **GPL/AGPL**: complican vender extensiones cerradas.
