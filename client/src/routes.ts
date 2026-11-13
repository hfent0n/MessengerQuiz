import { useSyncExternalStore } from 'react'

// The app's addresses: the home screen, the privacy page, and a multiplayer game at /play/<code>.
export type Route = { kind: 'home' } | { kind: 'privacy' } | { kind: 'play'; code: string }

const NAVIGATED = 'messenger-quiz-navigated'

export function useRoute(): Route {
  const path = useSyncExternalStore(subscribe, () => location.pathname)
  return parseRoute(path)
}

export function navigate(path: string): void {
  history.pushState(null, '', path)
  window.dispatchEvent(new Event(NAVIGATED))
}

export function playPath(code: string): string {
  return `/play/${code}`
}

export function parseRoute(path: string): Route {
  if (/^\/privacy\/?$/.test(path)) return { kind: 'privacy' }
  const match = /^\/play\/([a-z0-9]+)\/?$/i.exec(path)
  return match ? { kind: 'play', code: match[1].toLowerCase() } : { kind: 'home' }
}

function subscribe(onChange: () => void): () => void {
  window.addEventListener('popstate', onChange)
  window.addEventListener(NAVIGATED, onChange)
  return () => {
    window.removeEventListener('popstate', onChange)
    window.removeEventListener(NAVIGATED, onChange)
  }
}
