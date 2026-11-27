# 0014 — Usuarios compartidos en el núcleo

Fecha: 2026-11-27 · Estado: decidido · Decidió: DataDave-Dev · Relacionado: [arquitectura](../arquitectura/arquitectura.md), sección 4.1

## Decisión

Hay una sola tabla de usuarios, en el núcleo, compartida por todos los módulos. Cada módulo declara sus permisos y roles, y el administrador los asigna a la cuenta.

## Por qué

- Una persona tiene una cuenta y una contraseña aunque use varios módulos.
- Al desactivar una cuenta se pierde acceso a todo.
- Vale también para quien usa un solo módulo junto a otro ERP, porque el núcleo se instala siempre.
- Se distinguen tres conceptos: usuario (cuenta), empleado (persona, puede no tener cuenta) y cuenta de portal (usuario ligero de autoservicio).
- Acceso en la primera versión: usuario y contraseña con verificación en dos pasos.

## Alternativas descartadas

- **Usuarios por módulo**: repite el problema de CONTPAQi: una contraseña por programa y nadie sabe quién tiene acceso a qué.
