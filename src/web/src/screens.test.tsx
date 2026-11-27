// SPDX-License-Identifier: LGPL-3.0-or-later
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test } from 'vitest'
import App from './App'
import { ok, record, rejected, request, serve } from './test-helpers'

const META = 'GET /api/meta'
const DESCRIPTION = 'GET /api/meta/sample.request'
const LIST = 'GET /api/docs/sample.request'
const CREATE = 'POST /api/docs/sample.request'
const base = { [META]: ok([{ key: request.key, label: request.label }]), [DESCRIPTION]: ok(request) }

function open(hash: string) {
  window.location.hash = hash
  render(<App />)
}

test('the list shows the records in the order received, with the columns of the description', async () => {
  serve({ ...base, [LIST]: ok([record('2', 'Newest', 'two'), record('1', 'Oldest')]) })
  open('#/sample.request')

  expect(screen.getByRole('status')).toHaveTextContent('Cargando')
  const rows = await screen.findAllByRole('row')

  expect(within(rows[0]).getAllByRole('columnheader').map((th) => th.textContent)).toEqual(['Subject', 'Kind', 'Created'])
  expect(within(rows[1]).getAllByRole('cell').map((td) => td.textContent).slice(0, 2)).toEqual(['Newest', 'Second kind'])
  expect(within(rows[2]).getByRole('link', { name: 'Oldest' })).toHaveAttribute('href', '#/sample.request/1')
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
})

test('with nothing installed the first document opens by itself', async () => {
  serve({ ...base, [LIST]: ok([record('1', 'Only')]) })
  open('')

  expect(await screen.findByRole('cell', { name: 'Only' })).toBeInTheDocument()
})

test('an empty list says so and offers to create the first one', async () => {
  serve({ ...base, [LIST]: ok([]) })
  open('#/sample.request')

  expect(await screen.findByText('Todavía no hay ninguno.')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Crear el primero' })).toHaveAttribute('href', '#/sample.request/new')
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
})

test('when the server does not answer the list says so and retries on request', async () => {
  let alive = false
  serve({
    ...base,
    [LIST]: () => {
      if (!alive) throw new TypeError('Failed to fetch')
      return ok([record('1', 'Back again')])
    },
  })
  open('#/sample.request')

  expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo hablar con el servidor')
  expect(screen.queryByRole('status')).not.toBeInTheDocument()

  alive = true
  await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }))

  expect(await screen.findByRole('cell', { name: 'Back again' })).toBeInTheDocument()
})

test('the form has one control per writable field, of the right kind, with the required ones marked', async () => {
  serve(base)
  open('#/sample.request/new')

  expect(await screen.findByRole('textbox', { name: /Subject.*obligatorio/ })).toBeRequired()
  expect(screen.getByRole('textbox', { name: 'Notes' }).tagName).toBe('TEXTAREA')
  expect(screen.getByRole('combobox', { name: /Kind.*obligatorio/ })).toHaveValue('one')
  expect(screen.queryByLabelText(/Created/)).not.toBeInTheDocument()
})

test('an accepted record leads to the list with the new record on top', async () => {
  const records = [record('1', 'Older')]
  const fetchMock = serve({
    ...base,
    [LIST]: () => ok(records),
    [CREATE]: () => {
      records.unshift(record('2', 'Brand new', 'two'))
      return ok(records[0])
    },
  })
  open('#/sample.request/new')

  await userEvent.type(await screen.findByLabelText(/Subject/), 'Brand new')
  await userEvent.type(screen.getByLabelText('Notes'), 'Some detail')
  await userEvent.selectOptions(screen.getByLabelText(/Kind/), 'two')
  await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

  const rows = await screen.findAllByRole('row')
  expect(rows[1]).toHaveTextContent('Brand new')
  expect(rows[2]).toHaveTextContent('Older')
  const sent = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')![1]!
  expect(JSON.parse(sent.body as string)).toEqual({ subject: 'Brand new', notes: 'Some detail', kind: 'two' })
})

test('a rejected record marks the field with the reason and keeps what was typed', async () => {
  serve({
    ...base,
    [CREATE]: rejected('validation', [
      { field: 'subject', code: 'required', maxLength: null, options: null },
      { field: 'kind', code: 'invalid_option', maxLength: null, options: ['one', 'two'] },
    ]),
  })
  open('#/sample.request/new')

  await userEvent.type(await screen.findByLabelText('Notes'), 'Do not lose me')
  await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

  const subject = await screen.findByRole('textbox', { name: /Subject/, description: 'Este dato es obligatorio.' })
  expect(subject).toBeInvalid()
  expect(subject).toHaveFocus()
  expect(screen.getByLabelText(/Kind/)).toHaveAccessibleDescription('Elige una de estas: First kind, Second kind.')
  expect(screen.getByLabelText('Notes')).toHaveValue('Do not lose me')
  expect(screen.getByLabelText('Notes')).not.toBeInvalid()
  expect(screen.getByRole('button', { name: 'Guardar' })).toBeEnabled()
  expect(window.location.hash).toBe('#/sample.request/new')
})

test('saving twice in a row creates a single record', async () => {
  let finish: (answer: unknown) => void = () => {}
  const fetchMock = serve({
    ...base,
    [LIST]: ok([record('1', 'Once')]),
    [CREATE]: () => new Promise((resolve) => (finish = resolve)),
  })
  open('#/sample.request/new')
  await userEvent.type(await screen.findByLabelText(/Subject/), 'Once')

  await userEvent.dblClick(screen.getByRole('button', { name: 'Guardar' }))
  await userEvent.keyboard('{Enter}')

  expect(screen.getByRole('button', { name: 'Guardando…' })).toBeDisabled()
  expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)

  finish(ok(record('1', 'Once')))
  await waitFor(() => expect(window.location.hash).toBe('#/sample.request'))
})

test('when saving fails without a reason the form says so and can be sent again', async () => {
  serve({ ...base, [CREATE]: rejected('internal') })
  open('#/sample.request/new')
  await userEvent.type(await screen.findByLabelText(/Subject/), 'Kept')

  await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo guardar')
  expect(screen.getByLabelText(/Subject/)).toHaveValue('Kept')
  expect(screen.getByRole('button', { name: 'Guardar' })).toBeEnabled()
})

test('a record opens with all its fields, and one that does not exist says so', async () => {
  serve({
    ...base,
    'GET /api/docs/sample.request/1': ok({ ...record('1', 'Whole', 'two'), notes: 'Every detail' }),
    'GET /api/docs/sample.request/9': rejected('not_found'),
  })
  open('#/sample.request/1')

  expect(await screen.findByText('Every detail')).toBeInTheDocument()
  expect(screen.getAllByRole('term').map((dt) => dt.textContent)).toEqual(['Subject', 'Notes', 'Kind', 'Created'])

  window.location.hash = '#/sample.request/9'
  expect(await screen.findByRole('alert')).toHaveTextContent('No se encontró')
})
