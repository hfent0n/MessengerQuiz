import type { ExportFile } from '../chatFiles'
import demoFile from './demo-chat.json'
import { byMessageCount, type Chat, type ChatMessage } from './parseChat'

// A made-up chat, so people without a Facebook export can try the game. It goes through the same encryption and
// upload as a real chat, so the server treats it no differently.

type DemoMessage = {
  // Starts a new conversation at this time. Otherwise the message comes this many minutes after the one before.
  at?: string
  after?: number
  sender: string
  text?: string
  // Written as "😂 Sam".
  reactions?: string[]
  media?: string[]
}

export function demoChat(): Chat {
  const messages: ChatMessage[] = []
  let sentAt: number | null = null

  for (const entry of demoFile.messages as DemoMessage[]) {
    if (entry.at) {
      sentAt = Date.parse(`${entry.at}Z`) / 1000
    } else if (sentAt === null) {
      throw new Error('The first demo message needs a time.')
    } else {
      sentAt += (entry.after ?? 0) * 60
    }

    const media = entry.media ?? []
    messages.push({
      sender: entry.sender,
      text: entry.text ?? '',
      sentAt,
      reactions: (entry.reactions ?? []).map((reaction) => {
        const space = reaction.indexOf(' ')
        return { emoji: reaction.slice(0, space), reactor: reaction.slice(space + 1) }
      }),
      hasMedia: media.length > 0,
      media,
    })
  }

  return { title: demoFile.title, people: byMessageCount(messages), messages }
}

// The demo's photos ship with the client, under public/demo.
const DEMO_PHOTOS = ['photos/lasagne.svg', 'photos/quiz-trophy.svg', 'photos/pickle.svg']

export function demoMedia(): Map<string, ExportFile> {
  return new Map(
    DEMO_PHOTOS.map((path) => [
      path,
      {
        path,
        load: async () => {
          const response = await fetch(`${import.meta.env.BASE_URL}demo/${path}`)
          if (!response.ok) throw new Error(`The demo photo ${path} is missing.`)
          return response.blob()
        },
      },
    ]),
  )
}
