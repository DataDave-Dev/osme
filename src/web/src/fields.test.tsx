// SPDX-License-Identifier: LGPL-3.0-or-later
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test, vi } from 'vitest'
import { FieldInput, FieldValue } from './fields'
import { request } from './test-helpers'

const [subject, notes, kind, createdAt] = request.fields

test('a text field is a labelled text box that can be filled from the keyboard', async () => {
  const onChange = vi.fn()
  render(<FieldInput field={subject} value="" onChange={onChange} />)

  await userEvent.tab()
  await userEvent.keyboard('a')

  const input = screen.getByLabelText(/Subject/)
  expect(input).toHaveFocus()
  expect(input).toHaveAttribute('type', 'text')
  expect(input).toHaveAttribute('maxlength', '50')
  expect(input).toBeRequired()
  expect(screen.getByText(/obligatorio/)).toBeInTheDocument()
  expect(onChange).toHaveBeenCalledWith('a')
})

test('a long text field is a labelled multi-line box, not marked when optional', async () => {
  const onChange = vi.fn()
  render(<FieldInput field={notes} value="" onChange={onChange} />)

  await userEvent.tab()
  await userEvent.keyboard('b')

  const input = screen.getByLabelText('Notes')
  expect(input.tagName).toBe('TEXTAREA')
  expect(input).toHaveFocus()
  expect(input).not.toBeRequired()
  expect(onChange).toHaveBeenCalledWith('b')
})

test('an option field is a labelled list of its options, chosen from the keyboard', async () => {
  const onChange = vi.fn()
  render(<FieldInput field={kind} value="one" onChange={onChange} />)

  await userEvent.tab()
  await userEvent.selectOptions(screen.getByLabelText(/Kind/), 'Second kind')

  expect(screen.getByLabelText(/Kind/)).toHaveFocus()
  expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['First kind', 'Second kind'])
  expect(onChange).toHaveBeenCalledWith('two')
})

test('an optional option field can be left without choosing', () => {
  render(<FieldInput field={{ ...kind, required: false }} value="" onChange={() => {}} />)

  expect(screen.getAllByRole('option')).toHaveLength(3)
})

test('a rejection reason is tied to its control', () => {
  render(<FieldInput field={subject} value="" error="Este dato es obligatorio." onChange={() => {}} />)

  const input = screen.getByLabelText(/Subject/)
  expect(input).toBeInvalid()
  expect(input).toHaveAccessibleDescription('Este dato es obligatorio.')
})

test('a read-only date and time field has no control and is shown as a date', () => {
  const { container } = render(<FieldInput field={createdAt} value="" onChange={() => {}} />)
  expect(container).toBeEmptyDOMElement()

  render(<FieldValue field={createdAt} value="2026-11-27T15:30:00Z" />)
  expect(screen.getByText(/2026/)).toHaveAttribute('datetime', '2026-11-27T15:30:00Z')
})

test('an option value is shown with its label, and a missing value as a dash', () => {
  render(<FieldValue field={kind} value="two" />)
  render(<FieldValue field={notes} value={null} />)

  expect(screen.getByText('Second kind')).toBeInTheDocument()
  expect(screen.getByText('—')).toBeInTheDocument()
})
