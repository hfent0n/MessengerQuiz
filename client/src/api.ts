import { loadChatKey, saveChatKey } from './chat/keys'
import { openQuestion, personName, type OpenedMessage, type SealedQuestion } from './chat/openQuestion'
import type { Chat } from './chat/parseChat'
import { sealChat } from './chat/sealChat'
import { exportKey, newChatKey } from './chat/sealing'

export type UploadedChat = {
  chatId: string
  eligibleCount: number
}

export type ContextMessage = OpenedMessage

export type Question = OpenedMessage & {
  position: number
  context: ContextMessage[]
  // People's names, decrypted in the browser.
  options: string[]
  answer: string
  reactionCount: number
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

const CHAT_ID_HEADER = 'X-Chat-Id'

// Encrypts the chat with a new key, then uploads it gzipped. The key stays in this tab. Media never leaves the
// device: only how many items each message has is sent.
export async function uploadChat(chat: Chat, availableMedia: ReadonlySet<string>): Promise<UploadedChat> {
  const key = await newChatKey()
  const upload = await sealChat(chat, key, availableMedia)

  const response = await fetch('/api/chats', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: await gzip(new Blob([JSON.stringify(upload)])),
  })
  if (!response.ok) throw await toApiError(response)

  const uploaded: UploadedChat = await response.json()
  saveChatKey(uploaded.chatId, await exportKey(key))
  return uploaded
}

function gzip(blob: Blob): Promise<Blob> {
  return new Response(blob.stream().pipeThrough(new CompressionStream('gzip'))).blob()
}

type SealedWhoSaidThis = SealedQuestion & { options: number[]; answer: number; reactions: number }

// Returns null when the chat has no questions left.
export async function nextQuestion(chatId: string, people: string[]): Promise<Question | null> {
  const key = await loadChatKey(chatId)
  if (!key) throw new ApiError(401, "This chat's key is missing. Choose the chat again.")

  const response = await fetch('/api/questions/who-said-this', { headers: { [CHAT_ID_HEADER]: chatId } })
  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return null

  const sealed: SealedWhoSaidThis = await response.json()
  const opened = await openQuestion(key, sealed)
  return {
    ...opened,
    position: sealed.position,
    options: sealed.options.map((person) => personName(people, person)),
    answer: personName(people, sealed.answer),
    reactionCount: sealed.reactions,
  }
}

export async function deleteChat(chatId: string): Promise<void> {
  saveChatKey(chatId, null)
  await fetch('/api/chats/current', {
    method: 'DELETE',
    headers: { [CHAT_ID_HEADER]: chatId },
  })
}

async function toApiError(response: Response): Promise<ApiError> {
  if (response.status === 429) {
    return new ApiError(429, "You've uploaded a few chats quickly. Wait a minute and try again.")
  }

  const problem = await response.json().catch(() => null)
  return new ApiError(response.status, problem?.detail ?? 'Something went wrong. Try again.')
}
