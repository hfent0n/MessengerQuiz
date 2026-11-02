export type ActiveChat = {
  chatId: string
  title: string
}

const STORAGE_KEY = 'messenger-quiz-chat'

// sessionStorage survives a refresh but is cleared when the tab closes.
export function loadActiveChat(): ActiveChat | null {
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY)
    return stored ? JSON.parse(stored) : null
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
