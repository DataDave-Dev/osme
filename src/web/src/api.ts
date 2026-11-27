// SPDX-License-Identifier: LGPL-3.0-or-later

export type FieldType = 'text' | 'long_text' | 'option' | 'datetime'

export interface FieldOption {
  value: string
  label: string
}

export interface FieldDescription {
  name: string
  label: string
  type: FieldType
  required: boolean
  readOnly: boolean
  maxLength: number | null
  options: FieldOption[] | null
  default: string | null
  inList: boolean
}

export interface DocumentSummary {
  key: string
  label: string
}

export interface DocumentDescription extends DocumentSummary {
  fields: FieldDescription[]
}

export type DocumentRecord = { id: string } & Record<string, string | null>

export interface FieldError {
  field: string
  code: string
  maxLength: number | null
  options: string[] | null
}

/** Any answer that is not a success: a rejection from the server, or no usable answer at all. */
export class ApiFailure extends Error {
  readonly code: string
  readonly fields: FieldError[]

  constructor(code: string, fields: FieldError[] = []) {
    super(code)
    this.code = code
    this.fields = fields
  }
}

export const UNREACHABLE = 'unreachable'

async function call<T>(url: string, init?: RequestInit): Promise<T> {
  let body: { ok?: unknown; data?: unknown; error?: { code?: unknown; fields?: unknown } | null } | null
  try {
    const response = await fetch(url, init)
    body = await response.json()
  } catch {
    throw new ApiFailure(UNREACHABLE)
  }
  if (typeof body?.ok !== 'boolean') throw new ApiFailure(UNREACHABLE)
  if (!body.ok) {
    const code = typeof body.error?.code === 'string' ? body.error.code : 'internal'
    const fields = Array.isArray(body.error?.fields) ? (body.error.fields as FieldError[]) : []
    throw new ApiFailure(code, fields)
  }
  return body.data as T
}

const encode = encodeURIComponent

export const api = {
  documents: () => call<DocumentSummary[]>('/api/meta'),
  describe: (key: string) => call<DocumentDescription>(`/api/meta/${encode(key)}`),
  list: (key: string) => call<DocumentRecord[]>(`/api/docs/${encode(key)}`),
  get: (key: string, id: string) => call<DocumentRecord>(`/api/docs/${encode(key)}/${encode(id)}`),
  create: (key: string, values: Record<string, string>) =>
    call<DocumentRecord>(`/api/docs/${encode(key)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(values),
    }),
}
