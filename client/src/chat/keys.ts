import { importKey } from './sealing'

// Chat keys are kept in sessionStorage as text: they survive a refresh, stay in this tab, and go when it closes.
// The server never sees them.

const chatKeyName = (chatId: string) => `messenger-quiz-key-${chatId}`
const roomKeyName = (code: string) => `messenger-quiz-room-key-${code}`

export function saveChatKey(chatId: string, key: string | null): void {
  store(chatKeyName(chatId), key)
}

export function loadChatKey(chatId: string): Promise<CryptoKey | null> {
  return load(chatKeyName(chatId))
}

export function chatKeyText(chatId: string): string | null {
  return read(chatKeyName(chatId))
}

// Players' browsers get the key from the game link, after the #.
export function saveRoomKey(code: string, key: string | null): void {
  store(roomKeyName(code), key)
}

export function loadRoomKey(code: string): Promise<CryptoKey | null> {
  return load(roomKeyName(code))
}

export function roomKeyText(code: string): string | null {
  return read(roomKeyName(code))
}

// The part after # is never sent to the server, so it can carry the key.
export function keyFromHash(hash: string): string | null {
  const key = new URLSearchParams(hash.replace(/^#/, '')).get('k')
  return key && /^[A-Za-z0-9_-]{43}$/.test(key) ? key : null
}

export function linkWithKey(url: string, key: string): string {
  return `${url}#k=${key}`
}

async function load(name: string): Promise<CryptoKey | null> {
  const text = read(name)
  if (!text) return null
  try {
    return await importKey(text)
  } catch {
    return null
  }
}

function read(name: string): string | null {
  try {
    return sessionStorage.getItem(name)
  } catch {
    return null
  }
}

function store(name: string, value: string | null): void {
  try {
    if (value) {
      sessionStorage.setItem(name, value)
    } else {
      sessionStorage.removeItem(name)
    }
  } catch {
    // Without storage the chat still works until a refresh.
  }
}
