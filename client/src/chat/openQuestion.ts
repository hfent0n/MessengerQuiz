import type { SealedMessage } from './sealChat'
import { labels, open } from './sealing'

// A question as the server sends it: sealed boxes, and people as numbers.
export type SealedContext = { position: number; sealed: string; mediaCount: number }

export type SealedQuestion = {
  position: number
  sealed: string
  context: SealedContext[]
}

// A message ready to show.
export type OpenedMessage = {
  text: string
  hasMedia: boolean
  // Paths inside the chat folder, shown from the player's own files.
  media: string[]
  reactions: SealedMessage['reactions']
}

export type OpenedQuestion = OpenedMessage & {
  // Earlier messages from the same sender, oldest first.
  context: OpenedMessage[]
}

// Each box only opens at its own position, so the server can't move or change messages.
export async function openQuestion(key: CryptoKey, question: SealedQuestion): Promise<OpenedQuestion> {
  const [message, ...context] = await Promise.all(
    [question, ...question.context].map((item) => open<SealedMessage>(key, labels.message(item.position), item.sealed)),
  )
  return { ...message, context }
}

export function openPeople(key: CryptoKey, sealed: string): Promise<string[]> {
  return open<string[]>(key, labels.people, sealed)
}

export function openTitle(key: CryptoKey, sealed: string): Promise<string> {
  return open<string>(key, labels.title, sealed)
}

// Options arrive as people's numbers.
export function personName(people: string[], person: number): string {
  return people[person] ?? 'Someone'
}
