// SPDX-License-Identifier: LGPL-3.0-or-later
import type { FieldDescription, FieldError } from './api'

// ponytail: every sentence the interface says lives here, in one language.
// Change c0-4-1 turns this table into real translations.
const messages = {
  appName: 'OSME',
  mainNavigation: 'Documentos',
  loading: 'Cargando…',
  loadFailed: 'No se pudo hablar con el servidor. Revisa que esté encendido e inténtalo de nuevo.',
  retry: 'Reintentar',
  notFound: 'No se encontró lo que buscas.',
  noDocuments: 'Todavía no hay ningún módulo instalado.',
  empty: 'Todavía no hay ninguno.',
  createFirst: 'Crear el primero',
  create: 'Nuevo',
  newTitle: 'Nuevo: {label}',
  save: 'Guardar',
  saving: 'Guardando…',
  cancel: 'Cancelar',
  backToList: 'Volver a la lista',
  required: 'obligatorio',
  noOption: 'Sin elegir',
  saveFailed: 'No se pudo guardar. Inténtalo de nuevo.',
  saveRejected: 'Hay datos por corregir.',
  'error.required': 'Este dato es obligatorio.',
  'error.too_long': 'Máximo {maxLength} caracteres.',
  'error.invalid_option': 'Elige una de estas: {options}.',
  'error.unknown_field': 'El servidor no reconoce el dato «{field}».',
  'error.invalid_type': 'El valor no tiene la forma esperada.',
  'error.other': 'El valor no es válido.',
} as const

export type MessageKey = keyof typeof messages

export function t(key: MessageKey, values: Record<string, string | number> = {}): string {
  return messages[key].replace(/\{(\w+)\}/g, (_, name: string) => String(values[name] ?? ''))
}

/** Words a rejection code from the server for the person filling the form. */
export function describeError(error: FieldError, field?: FieldDescription): string {
  const key = `error.${error.code}`
  const labels = (error.options ?? []).map(
    (value) => field?.options?.find((option) => option.value === value)?.label ?? value,
  )
  return t(key in messages ? (key as MessageKey) : 'error.other', {
    field: error.field,
    maxLength: error.maxLength ?? '',
    options: labels.join(', '),
  })
}
