import { fileName, type ChatSource } from './chatFiles'

export type UploadedChat = {
  chatId: string
  title: string
  participants: string[]
  messageCount: number
  eligibleCount: number
}

export type ContextMessage = {
  text: string
  hasMedia: boolean
  media: string[]
}

export type Question = {
  messageId: string
  text: string
  // Paths inside the chat folder, shown from the player's own files.
  media: string[]
  // Earlier messages from the same sender, oldest first.
  context: ContextMessage[]
  options: string[]
  answer: string
  reactions: number
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

const CHAT_ID_HEADER = 'X-Chat-Id'

// Pages are gzipped first, which makes them about 9 times smaller. Media never leaves the device, only file names.
export async function uploadChat(chat: ChatSource): Promise<UploadedChat> {
  const form = new FormData()
  for (const page of chat.pages) {
    form.append('files', await gzip(await page.load()), `${chat.folder}/${fileName(page)}`)
  }
  form.append('media', JSON.stringify([...chat.media.keys()]))

  const response = await fetch('/api/chats', { method: 'POST', body: form })
  if (!response.ok) throw await toApiError(response)

  return response.json()
}

function gzip(blob: Blob): Promise<Blob> {
  return new Response(blob.stream().pipeThrough(new CompressionStream('gzip'))).blob()
}

// Returns null when the chat has no questions left.
export async function nextQuestion(chatId: string): Promise<Question | null> {
  const response = await fetch('/api/questions/who-said-this', {
    headers: { [CHAT_ID_HEADER]: chatId },
  })
  if (!response.ok) throw await toApiError(response)

  return response.status === 204 ? null : response.json()
}

export async function deleteChat(chatId: string): Promise<void> {
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
