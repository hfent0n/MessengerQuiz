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
}

export type Question = {
  messageId: string
  text: string
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

export async function uploadChat(pages: File[]): Promise<UploadedChat> {
  const form = new FormData()
  for (const page of pages) {
    form.append('files', page, page.webkitRelativePath)
  }

  const response = await fetch('/api/chats', { method: 'POST', body: form })
  if (!response.ok) throw await toApiError(response)

  return response.json()
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
