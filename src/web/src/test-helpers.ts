// SPDX-License-Identifier: LGPL-3.0-or-later
import { vi } from 'vitest'
import type { DocumentDescription, DocumentRecord, FieldDescription } from './api'

const field = (name: string, label: string, type: FieldDescription['type'], rest: Partial<FieldDescription> = {}): FieldDescription => ({
  name, label, type, required: false, readOnly: false, maxLength: null, options: null, default: null, inList: true, ...rest,
})

/** A document the interface has no code for. */
export const request: DocumentDescription = {
  key: 'sample.request',
  label: 'Request',
  fields: [
    field('subject', 'Subject', 'text', { required: true, maxLength: 50 }),
    field('notes', 'Notes', 'long_text', { inList: false }),
    field('kind', 'Kind', 'option', {
      required: true,
      default: 'one',
      options: [{ value: 'one', label: 'First kind' }, { value: 'two', label: 'Second kind' }],
    }),
    field('created_at', 'Created', 'datetime', { readOnly: true }),
  ],
}

export const record = (id: string, subject: string, kind = 'one'): DocumentRecord => ({
  id, subject, notes: null, kind, created_at: '2026-11-27T15:30:00Z',
})

export const ok = (data: unknown) => ({ ok: true, data, error: null })
export const rejected = (code: string, fields: unknown[] = []) => ({ ok: false, data: null, error: { code, fields } })

type Answer = unknown | (() => unknown)

/**
 * Replaces the server: answers by "METHOD /url". A function is called on every request,
 * so it can return a pending promise, throw like a dead network, or change its answer.
 */
export function serve(routes: Record<string, Answer>) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const answer = routes[`${init?.method ?? 'GET'} ${url}`]
    if (answer === undefined) throw new TypeError(`No route for ${url}`)
    const body = await (typeof answer === 'function' ? answer() : answer)
    return { json: async () => body } as Response
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}
