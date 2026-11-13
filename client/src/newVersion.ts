// Each deploy replaces the built files, and their names change with their contents. A page loaded before a deploy
// still asks for the old names when it loads a part on demand, such as the zip reader, and those are gone.
// Reloading picks up the new version. It's done at most once a minute, so a real outage can't cause a reload loop.

const RELOADED_AT = 'messenger-quiz-reloaded-for-new-version'
const MIN_GAP_MS = 60_000

export const NEW_VERSION_MESSAGE = 'Messenger Quiz has just been updated. Refresh the page, then try again.'

export function reloadOnNewVersion(): void {
  window.addEventListener('vite:preloadError', () => {
    if (reloadedRecently()) return

    try {
      sessionStorage.setItem(RELOADED_AT, String(Date.now()))
    } catch {
      // Without storage there's no loop guard, so don't reload. The page shows NEW_VERSION_MESSAGE instead.
      return
    }
    location.reload()
  })
}

// A failed on-demand load, such as after a deploy.
export function isStaleVersionError(error: unknown): boolean {
  return error instanceof Error && /dynamically imported module|Importing a module script failed/i.test(error.message)
}

function reloadedRecently(): boolean {
  try {
    return Date.now() - Number(sessionStorage.getItem(RELOADED_AT) ?? 0) < MIN_GAP_MS
  } catch {
    return true
  }
}
