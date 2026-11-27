// SPDX-License-Identifier: LGPL-3.0-or-later
import { useEffect, useSyncExternalStore } from 'react'
import { api } from './api'
import { t } from './messages'
import { listPath } from './paths'
import { FormScreen, ListScreen, RecordScreen } from './screens'
import { useLoad } from './useLoad'

const subscribe = (notify: () => void) => {
  window.addEventListener('hashchange', notify)
  return () => window.removeEventListener('hashchange', notify)
}

/** `#/<document>`, `#/<document>/new` or `#/<document>/<id>`. */
function useRoute() {
  const hash = useSyncExternalStore(subscribe, () => window.location.hash)
  const [documentKey = '', rest = ''] = hash.replace(/^#\/?/, '').split('/').map(decodeURIComponent)
  return { documentKey, rest }
}

export default function App() {
  const { documentKey, rest } = useRoute()
  const documents = useLoad('documents', api.documents)
  const first = documents.state.status === 'ready' ? documents.state.data[0]?.key : undefined

  // The interface has no document of its own: with nothing chosen, it opens the first one installed.
  useEffect(() => {
    if (!documentKey && first) window.location.replace(listPath(first))
  }, [documentKey, first])

  return (
    <>
      <header className="app-header">
        <span className="app-name">{t('appName')}</span>
        <nav aria-label={t('mainNavigation')}>
          {documents.state.status === 'ready' &&
            documents.state.data.map((document) => (
              <a
                key={document.key}
                href={listPath(document.key)}
                aria-current={document.key === documentKey ? 'page' : undefined}
              >
                {document.label}
              </a>
            ))}
        </nav>
      </header>
      <main>
        {documentKey ? (
          rest === '' ? (
            <ListScreen key={documentKey} documentKey={documentKey} />
          ) : rest === 'new' ? (
            <FormScreen key={documentKey} documentKey={documentKey} />
          ) : (
            <RecordScreen documentKey={documentKey} id={rest} />
          )
        ) : documents.state.status === 'loading' ? (
          <p role="status">{t('loading')}</p>
        ) : documents.state.status === 'error' ? (
          <div role="alert" className="notice">
            <p>{t('loadFailed')}</p>
            <button type="button" onClick={documents.retry}>
              {t('retry')}
            </button>
          </div>
        ) : (
          !first && <p>{t('noDocuments')}</p>
        )}
      </main>
    </>
  )
}
