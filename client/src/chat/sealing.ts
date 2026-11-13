// Encrypts chat content in the browser, so the server stores and returns it without being able to read it.
//
// AES-GCM with a 256-bit key made in the browser for each chat. Each value is sealed into its own box with a fresh
// random IV, and a label such as "message:12" is bound in as additional authenticated data. A box only opens with
// its own label, so the server can't move text to another message or alter it without opening failing.

const ALGORITHM = 'AES-GCM'
const IV_BYTES = 12

export const labels = {
  title: 'title',
  people: 'people',
  message: (position: number) => `message:${position}`,
  // A multiplayer chat message, labelled with its sender so it can't be passed off as someone else's.
  chatLine: (playerId: string) => `chat:${playerId}`,
}

// Opening failed: the box was changed, moved or sealed with another key.
export class SealError extends Error {}

export function newChatKey(): Promise<CryptoKey> {
  // Extractable, because the host shares it with other players in the game link.
  return crypto.subtle.generateKey({ name: ALGORITHM, length: 256 }, true, ['encrypt', 'decrypt'])
}

export async function exportKey(key: CryptoKey): Promise<string> {
  return toBase64Url(new Uint8Array(await crypto.subtle.exportKey('raw', key)))
}

export async function importKey(text: string): Promise<CryptoKey> {
  const bytes = fromBase64Url(text)
  if (bytes?.length !== 32) throw new SealError("The chat's key isn't valid.")
  return crypto.subtle.importKey('raw', bytes, ALGORITHM, true, ['encrypt', 'decrypt'])
}

const encoder = new TextEncoder()
const decoder = new TextDecoder()

// Returns base64url of the IV followed by the ciphertext and its 16-byte tag.
export async function seal(key: CryptoKey, label: string, value: unknown): Promise<string> {
  const iv = crypto.getRandomValues(new Uint8Array(IV_BYTES))
  const ciphertext = await crypto.subtle.encrypt(
    { name: ALGORITHM, iv, additionalData: encoder.encode(label) },
    key,
    encoder.encode(JSON.stringify(value)),
  )

  const box = new Uint8Array(IV_BYTES + ciphertext.byteLength)
  box.set(iv)
  box.set(new Uint8Array(ciphertext), IV_BYTES)
  return toBase64Url(box)
}

export async function open<T>(key: CryptoKey, label: string, sealed: string): Promise<T> {
  const box = fromBase64Url(sealed)
  if (!box || box.length <= IV_BYTES) throw new SealError('A message is damaged.')

  try {
    const plaintext = await crypto.subtle.decrypt(
      { name: ALGORITHM, iv: box.subarray(0, IV_BYTES), additionalData: encoder.encode(label) },
      key,
      box.subarray(IV_BYTES),
    )
    return JSON.parse(decoder.decode(plaintext)) as T
  } catch {
    throw new SealError("A message couldn't be decrypted. It may have been changed.")
  }
}

export function toBase64Url(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
}

export function fromBase64Url(text: string): Uint8Array<ArrayBuffer> | null {
  if (!/^[A-Za-z0-9_-]*$/.test(text)) return null
  try {
    const binary = atob(text.replaceAll('-', '+').replaceAll('_', '/'))
    const bytes = new Uint8Array(binary.length)
    for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index)
    return bytes
  } catch {
    return null
  }
}
