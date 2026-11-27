# 0010 — Licencia LGPL

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 2

## Decisión

El núcleo y los módulos oficiales se publican bajo LGPL versión 3 o posterior.

## Por qué

- Quien modifique el núcleo debe publicar sus cambios, pero las extensiones pueden ser cerradas y de pago, lo que hace viable la tienda.
- Es el modelo de Odoo Community.

## Archivos

- `LICENSE`: texto de la GNU Lesser General Public License, versión 3.
- `LICENSE.GPL`: texto de la GNU General Public License, versión 3. La LGPL 3 se define como permisos adicionales sobre la GPL 3 y pide acompañarse de ella.

## Versión

**LGPL versión 3 o posterior** (`LGPL-3.0-or-later`). Permite adoptar una versión futura de la licencia sin pedir permiso a cada contribuidor. Es el identificador que llevará el aviso de licencia en los archivos de código.

## Alternativas descartadas

- **Apache 2.0 o MIT**: más permisivas: cualquiera podría cerrar su versión, incluido un competidor.
- **GPL/AGPL**: complican vender extensiones cerradas.
