// SPDX-License-Identifier: LGPL-3.0-or-later
import type { FieldDescription } from './api'
import { t } from './messages'

interface InputProps {
  field: FieldDescription
  value: string
  error?: string
  onChange: (value: string) => void
}

const inputId = (field: FieldDescription) => `field-${field.name}`
const errorId = (field: FieldDescription) => `field-${field.name}-error`

/** What every control shares: the label tied to it, the required mark and the rejection reason. */
function controlProps({ field, value, error, onChange }: InputProps) {
  return {
    id: inputId(field),
    name: field.name,
    value,
    'aria-required': field.required,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error ? errorId(field) : undefined,
    onChange: (event: { target: { value: string } }) => onChange(event.target.value),
  }
}

function TextInput(props: InputProps) {
  return <input type="text" maxLength={props.field.maxLength ?? undefined} {...controlProps(props)} />
}

function LongTextInput(props: InputProps) {
  return <textarea rows={5} maxLength={props.field.maxLength ?? undefined} {...controlProps(props)} />
}

function OptionInput(props: InputProps) {
  return (
    <select {...controlProps(props)}>
      {!props.field.required && <option value="">{t('noOption')}</option>}
      {props.field.options?.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  )
}

// One control per field type. A read-only type has no control.
const inputs = { text: TextInput, long_text: LongTextInput, option: OptionInput, datetime: null }

export function FieldInput(props: InputProps) {
  const { field, error } = props
  const Input = inputs[field.type]
  if (field.readOnly || !Input) return null

  return (
    <div className="field">
      <label htmlFor={inputId(field)}>
        {field.label}
        {field.required && <span className="field-required"> ({t('required')})</span>}
      </label>
      <Input {...props} />
      {error && (
        <p className="field-error" id={errorId(field)}>
          {error}
        </p>
      )}
    </div>
  )
}

const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

/** A stored value, shown the way its type asks. */
export function FieldValue({ field, value }: { field: FieldDescription; value: string | null }) {
  if (value === null || value === '') return <span className="empty-value">—</span>
  if (field.type === 'datetime') return <time dateTime={value}>{dateTime.format(new Date(value))}</time>
  if (field.type === 'option') return <>{field.options?.find((option) => option.value === value)?.label ?? value}</>
  return <>{value}</>
}
