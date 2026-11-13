export type ActiveChat = {
  chatId: string
  title: string
  // People's names, most messages first. Questions refer to people by their place in this list, and the server
  // only has an encrypted copy.
  people: string[]
  // The demo's photos ship with the app, so unlike a player's own media they can be shown again after a refresh.
  demo?: boolean
  // Unset until the player picks single player or multiplayer.
  mode?: 'single' | 'multi'
}

const STORAGE_KEY = 'messenger-quiz-chat'

// sessionStorage survives a refresh but is cleared when the tab closes.
export function loadActiveChat(): ActiveChat | null {
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY)
    const chat = stored ? JSON.parse(stored) : null
    // Chats saved by an older version have no names, and were uploaded unencrypted, so they're dropped.
    return Array.isArray(chat?.people) ? chat : null
  } catch {
    return null
  }
}

export function saveActiveChat(chat: ActiveChat | null): void {
  try {
    if (chat) {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(chat))
    } else {
      sessionStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    // Without storage the quiz still works, but a refresh loses the chat.
  }
}
