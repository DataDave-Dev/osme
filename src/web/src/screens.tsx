// SPDX-License-Identifier: LGPL-3.0-or-later
import { type FormEvent, useRef, useState } from 'react'
import { api, ApiFailure, type DocumentDescription, type DocumentRecord, type FieldError } from './api'
import { FieldInput, FieldValue } from './fields'
import { describeError, t } from './messages'
import { listPath, newPath, recordPath } from './paths'
import { type Load, useLoad } from './useLoad'

/** Loading and failure look the same on every screen; only the ready state differs. */
function Loaded<T>({ load, children }: { load: { state: Load<T>; retry: () => void }; children: (data: T) => React.ReactNode }) {
  const { state, retry } = load
  if (state.status === 'loading') return <p role="status">{t('loading')}</p>
  if (state.status === 'ready') return children(state.data)
  if (state.error instanceof ApiFailure && state.error.code === 'not_found') return <p role="alert">{t('notFound')}</p>
  return (
    <div role="alert" className="notice">
      <p>{t('loadFailed')}</p>
      <button type="button" onClick={retry}>
        {t('retry')}
      </button>
    </div>
  )
}

export function ListScreen({ documentKey }: { documentKey: string }) {
  const load = useLoad(documentKey, () => Promise.all([api.describe(documentKey), api.list(documentKey)]))

  return (
    <Loaded load={load}>
      {([document, records]) => {
        const columns = document.fields.filter((field) => field.inList)
        return (
          <>
            <header className="screen-header">
              <h1>{document.label}</h1>
              {records.length > 0 && (
                <a className="button primary" href={newPath(documentKey)}>
                  {t('create')}
                </a>
              )}
            </header>
            {records.length === 0 ? (
              <div className="notice">
                <p>{t('empty')}</p>
                <a className="button primary" href={newPath(documentKey)}>
                  {t('createFirst')}
                </a>
              </div>
            ) : (
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      {columns.map((field) => (
                        <th key={field.name} scope="col">
                          {field.label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {records.map((record) => (
                      <tr key={record.id}>
                        {columns.map((field, index) => (
                          <td key={field.name}>
                            {index === 0 ? (
                              <a href={recordPath(documentKey, record.id)}>
                                <FieldValue field={field} value={record[field.name]} />
                              </a>
                            ) : (
                              <FieldValue field={field} value={record[field.name]} />
                            )}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )
      }}
    </Loaded>
  )
}

export function FormScreen({ documentKey }: { documentKey: string }) {
  const load = useLoad(documentKey, () => api.describe(documentKey))
  return <Loaded load={load}>{(document) => <CreateForm document={document} />}</Loaded>
}

function CreateForm({ document }: { document: DocumentDescription }) {
  const fields = document.fields.filter((field) => !field.readOnly)
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(fields.map((field) => [field.name, field.default ?? ''])),
  )
  const [errors, setErrors] = useState<FieldError[]>([])
  const [problem, setProblem] = useState('')
  const [saving, setSaving] = useState(false)
  // State updates wait for a render; the ref closes the door within the same tick.
  const sending = useRef(false)
  const form = useRef<HTMLFormElement>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (sending.current) return
    sending.current = true
    setSaving(true)
    setErrors([])
    setProblem('')
    try {
      await api.create(document.key, values)
      window.location.hash = listPath(document.key)
    } catch (error) {
      const rejected = error instanceof ApiFailure && error.code === 'validation' ? error.fields : []
      setErrors(rejected)
      setProblem(t(rejected.length > 0 ? 'saveRejected' : 'saveFailed'))
      sending.current = false
      setSaving(false)
      const first = rejected.find((e) => fields.some((field) => field.name === e.field))
      if (first) form.current?.querySelector<HTMLElement>(`[name="${CSS.escape(first.field)}"]`)?.focus()
    }
  }

  const orphans = errors.filter((error) => fields.every((field) => field.name !== error.field))

  return (
    <form ref={form} onSubmit={submit} noValidate className="form">
      <h1>{t('newTitle', { label: document.label })}</h1>
      {problem && (
        <div role="alert" className="form-problem">
          <p>{problem}</p>
          {orphans.map((error) => (
            <p key={error.field}>{describeError(error)}</p>
          ))}
        </div>
      )}
      {fields.map((field) => {
        const error = errors.find((e) => e.field === field.name)
        return (
          <FieldInput
            key={field.name}
            field={field}
            value={values[field.name] ?? ''}
            error={error && describeError(error, field)}
            onChange={(value) => setValues((current) => ({ ...current, [field.name]: value }))}
          />
        )
      })}
      <div className="form-actions">
        <button type="submit" className="primary" disabled={saving}>
          {t(saving ? 'saving' : 'save')}
        </button>
        <a className="button" href={listPath(document.key)}>
          {t('cancel')}
        </a>
      </div>
    </form>
  )
}

export function RecordScreen({ documentKey, id }: { documentKey: string; id: string }) {
  const load = useLoad(`${documentKey}/${id}`, () => Promise.all([api.describe(documentKey), api.get(documentKey, id)]))

  return (
    <Loaded load={load}>
      {([document, record]: [DocumentDescription, DocumentRecord]) => (
        <>
          <header className="screen-header">
            <h1>{document.label}</h1>
            <a className="button" href={listPath(documentKey)}>
              {t('backToList')}
            </a>
          </header>
          <dl className="record">
            {document.fields.map((field) => (
              <div key={field.name}>
                <dt>{field.label}</dt>
                <dd className={field.type === 'long_text' ? 'long-text' : undefined}>
                  <FieldValue field={field} value={record[field.name]} />
                </dd>
              </div>
            ))}
          </dl>
        </>
      )}
    </Loaded>
  )
}
