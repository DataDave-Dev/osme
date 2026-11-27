# 0018 — Firma del instalador: sin firma por ahora, SignPath a considerar

Fecha: 2026-11-27 · Estado: decidido lo de "por ahora"; SignPath queda **por evaluar** · Decidió: DataDave-Dev · Relacionado: [fase 0](../fases/fase-00-base-ti-interno/diseno.md), sección 2.5

## Decisión

Por el momento no se cuenta con certificado de firma de código. El instalador de Windows se publica **sin firma**.

Cuando el repositorio sea público y exista una versión instalable, se evalúa solicitar la firma gratuita para proyectos open source de **SignPath Foundation**.

## Qué implica publicar sin firma

- Al abrir el instalador, Windows muestra "Windows protegió su PC". El usuario continúa con **Más información → Ejecutar de todas formas**.
- No impide instalar ni usar. Sí puede asustar a un usuario no técnico, que es el público del instalador de doble clic.
- Mitigación: la página de descarga y el README explican ese paso con captura de pantalla.
- Linux no se ve afectado. En Mac se distribuye paquete de servidor, no instalador gráfico, así que tampoco pesa.

## SignPath Foundation

Programa que firma gratis los binarios de proyectos open source. El certificado es de la fundación, no del proyecto.

**Condiciones conocidas** (tomadas de signpath.org y de proyectos que lo usan; revisar las vigentes antes de solicitar):

| Condición | Cómo queda OSME |
|---|---|
| Licencia open source aprobada por OSI | Cumple: LGPL |
| Repositorio público | Pendiente: aún no hay repositorio |
| Sin componentes propietarios en lo que se firma | Cumple para núcleo y módulos oficiales. **Ojo:** las extensiones cerradas o de pago no pueden ir dentro del instalador firmado; se distribuyen aparte, lo cual ya es el diseño |
| Compilación totalmente automatizada, de modo que el binario salga directo del código del repositorio | Pendiente: exige montar la compilación del instalador en GitHub Actions |
| Verificación en dos pasos para todos los miembros, en SignPath y en GitHub | Fácil de cumplir |
| Publicar una política de firma de código en el proyecto, con la leyenda de atribución a SignPath | Un documento corto |
| Al menos una versión publicada antes de solicitar | Mencionado por terceros, no confirmado en la página oficial |

**Consecuencias a tener presentes**

- Como editor del instalador aparecerá **SignPath Foundation**, no OSME.
- La fundación no asume responsabilidad por daños del software firmado.
- La advertencia de Windows no desaparece al instante con cualquier certificado: la reputación se construye con descargas. Confirmar cómo se comporta con el certificado de la fundación.
- La solicitud se revisa y puede ser rechazada; no es automática.

## Otras salidas

| Opción | Costo | Nota |
|---|---|---|
| Certificado comercial | Pago anual | Pide persona o empresa verificada |
| Tienda de Microsoft / winget | Bajo o gratis | Vía de distribución alterna; requisitos por confirmar |

## Cuándo retomarlo

Al terminar la entrega 0.1 de la fase 0, cuando ya hay instalador y el repositorio es público. Pasos entonces: confirmar condiciones vigentes en signpath.org, automatizar la compilación del instalador, escribir la política de firma y enviar la solicitud.

## Fuentes

- https://signpath.org/
- https://signpath.org/terms
- https://signpath.io/product/open-source
- Ejemplo de política publicada por un proyecto: https://pandoc.org/code-signing-policy.html
