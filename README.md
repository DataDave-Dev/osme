# OSME

**Open Source Modular Enterprise** — ERP de código abierto, completo y modular.

- **Módulos aparte.** Cada módulo se instala por separado y funciona solo, incluso junto a otro ERP que la empresa ya tenga.
- **El sistema se adapta a la empresa**, no al revés: configuración desde pantalla para lo común y extensiones instalables para lo demás.
- **Sin edición de pago.** Todos los módulos oficiales son libres y completos.

## Estado

En construcción. Corre la primera rebanada de punta a punta (change `0-1-1`): el servidor carga el módulo de TI interno y se puede levantar un ticket desde el navegador. Todavía no hay inicio de sesión, así que el servidor solo atiende a la propia máquina.

## Levantar el entorno

Hace falta: SDK de .NET 10 (la versión exacta está en `global.json`), Node 24 con `pnpm`, y Docker.

```sh
cp .env.example .env                 # valores de desarrollo
docker compose up -d --wait          # PostgreSQL en localhost:5433
pnpm --dir src/web install
pnpm --dir src/web build             # deja la interfaz donde el servidor la sirve
dotnet build                         # compila todo y deja los paquetes en artifacts/packages
set -a; . ./.env; set +a             # exporta OSME_DB
dotnet run --project src/Host        # http://localhost:5080
```

Para trabajar en la interfaz con recarga: `pnpm --dir src/web dev` (redirige `/api` al servidor).

## Pruebas

```sh
dotnet test                          # servidor, contra PostgreSQL real; incluye la frontera entre paquetes
pnpm --dir src/web test              # componentes de la interfaz
pnpm --dir src/web e2e               # navegador contra el servidor real
```

La primera vez, `pnpm --dir src/web exec playwright install chromium` descarga el navegador de las pruebas; para usar uno ya instalado, `PLAYWRIGHT_CHROMIUM=/usr/bin/chromium`.

## Documentación

- [Índice de la documentación](documents/README.md)
- [Arquitectura](documents/arquitectura/arquitectura.md)
- [Hoja de ruta](documents/hoja-de-ruta/hoja-de-ruta.md)
- [Decisiones](documents/decisiones/README.md)
- [Backlog de la entrega 0.1](documents/fases/fase-00-base-ti-interno/backlog-0.1.md)

El backlog vive en [`openspec/changes/`](openspec/changes/) y en el [tablero del proyecto](https://github.com/users/DataDave-Dev/projects/4).

## Stack

Servidor .NET (C#) · PostgreSQL · interfaz React + TypeScript.

## Licencia

GNU Lesser General Public License, versión 3 o posterior (`LGPL-3.0-or-later`). Ver [LICENSE](LICENSE) y [LICENSE.GPL](LICENSE.GPL).

Las extensiones de terceros pueden publicarse bajo su propia licencia.
