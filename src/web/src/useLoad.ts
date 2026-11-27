// SPDX-License-Identifier: LGPL-3.0-or-later
import { useEffect, useState } from 'react'

export type Load<T> = { status: 'loading' } | { status: 'error'; error: unknown } | { status: 'ready'; data: T }

/** Runs `load` whenever `key` changes, and again on `retry`. */
export function useLoad<T>(key: string, load: () => Promise<T>): { state: Load<T>; retry: () => void } {
  const [attempt, setAttempt] = useState(0)
  const request = `${attempt}:${key}`
  const [settled, setSettled] = useState<{ request: string; state: Load<T> }>()

  useEffect(() => {
    let current = true
    load().then(
      (data) => current && setSettled({ request, state: { status: 'ready', data } }),
      (error: unknown) => current && setSettled({ request, state: { status: 'error', error } }),
    )
    return () => {
      current = false
    }
    // `load` is a fresh closure on every render; `request` says when it really changed.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [request])

  // An answer to an earlier request is not an answer to this one.
  const state: Load<T> = settled?.request === request ? settled.state : { status: 'loading' }
  return { state, retry: () => setAttempt((n) => n + 1) }
}
