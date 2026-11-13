import type { Chat, Reaction } from './parseChat'
import { labels, seal } from './sealing'
import { canBeQuestion, isSystemLine } from './systemMessages'

// What the server receives. Everything readable is sealed. The rest is what choosing questions needs.
export type ChatUpload = {
  title: string
  people: string
  personCount: number
  messages: UploadedMessage[]
}

export type UploadedMessage = {
  // The sender's place in the sealed people list.
  sender: number
  sentAt: number | null
  reactions: number
  media: number
  // Long enough, and not a system line or a bare link, or has a photo, GIF or video.
  quizzable: boolean
  // An event line, such as "Ana named the group". It ends a run of context.
  system: boolean
  sealed: string
}

// The contents of a message's sealed box.
export type SealedMessage = {
  text: string
  reactions: Reaction[]
  // Anything shown as media, including stickers and voice messages.
  hasMedia: boolean
  media: string[]
}

// Media the player doesn't have is left out, since it can't be shown. Exports often leave out older media.
export async function sealChat(chat: Chat, key: CryptoKey, availableMedia: ReadonlySet<string>): Promise<ChatUpload> {
  const senders = new Map(chat.people.map((person, index) => [person, index]))

  const messages = await Promise.all(
    chat.messages.map(async (message, position): Promise<UploadedMessage> => {
      const media = message.media.filter((path) => availableMedia.has(path))
      const contents: SealedMessage = { text: message.text, reactions: message.reactions, hasMedia: message.hasMedia, media }

      return {
        sender: senders.get(message.sender)!,
        sentAt: message.sentAt,
        reactions: message.reactions.length,
        media: media.length,
        quizzable: canBeQuestion(message.text, media),
        system: isSystemLine(message.text),
        sealed: await seal(key, labels.message(position), contents),
      }
    }),
  )

  return {
    title: await seal(key, labels.title, chat.title),
    people: await seal(key, labels.people, chat.people),
    personCount: chat.people.length,
    messages,
  }
}
