// Reads a chat's pages from a Messenger HTML export. Runs in the browser, so the server never sees the pages.

export type Reaction = { emoji: string; reactor: string }

export type ChatMessage = {
  sender: string
  text: string
  // Seconds since 1970, reading the export's local times as UTC. Only the gaps between messages matter.
  sentAt: number | null
  reactions: Reaction[]
  // Anything shown as media, including stickers and voice messages.
  hasMedia: boolean
  // Photos, GIFs and videos, as paths inside the chat folder, such as "photos/123.jpg".
  media: string[]
}

export type Chat = {
  title: string
  // Most messages first.
  people: string[]
  // Oldest first.
  messages: ChatMessage[]
}

// The message is shown to the player, so it must never contain chat content.
export class ChatParseError extends Error {}

// Facebook's auto-generated class names, kept together because a future export could rename them.
const MESSAGE_BLOCK = '._a6-g'
const SENDER = '._a6-i'
const CONTENT = '._a6-p'
const TIMESTAMP = '._a72d'
const REACTIONS = 'ul._a6-q > li'

// Inside the content wrapper, the second div holds the text. Later divs hold media and reactions.
const TEXT_DIV_INDEX = 1

// Images in these folders are photos and GIFs. Video sources are in "videos/". Images there are thumbnails.
const IMAGE_FOLDERS = ['photos/', 'gifs/']
const VIDEO_FOLDERS = ['videos/']

const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec']
// Older exports write "9:05:03pm" and newer ones "9:05:03 pm".
const TIMESTAMP_PATTERN = /^([A-Za-z]{3}) (\d{1,2}), (\d{4}) (\d{1,2}):(\d{2}):(\d{2}) ?(am|pm)$/i

export function parseChat(pages: string[]): Chat {
  if (pages.length === 0) throw new ChatParseError('The chat has no message pages.')

  const titles = new Set<string>()
  const messages: ChatMessage[] = []

  for (const html of pages) {
    const page = new DOMParser().parseFromString(html, 'text/html')
    if (!page.querySelector(MESSAGE_BLOCK)) {
      throw new ChatParseError("A file isn't a recognisable Messenger chat page.")
    }

    titles.add(page.title.trim())
    // Pages list messages newest first. Reversing keeps same-second messages in sending order after sorting.
    const blocks = [...page.querySelectorAll(MESSAGE_BLOCK)].map(parseBlock).filter((message) => message !== null)
    messages.push(...blocks.reverse())
  }

  if (titles.size > 1) throw new ChatParseError('The pages come from different chats.')

  // Sorting is stable, so messages with the same time stay in order.
  messages.sort((a, b) => (a.sentAt ?? -Infinity) - (b.sentAt ?? -Infinity))

  return { title: [...titles][0], people: byMessageCount(messages), messages }
}

// The same rule as the server used: most messages first, ties in order of first message.
export function byMessageCount(messages: { sender: string }[]): string[] {
  const counts = new Map<string, number>()
  for (const { sender } of messages) counts.set(sender, (counts.get(sender) ?? 0) + 1)
  return [...counts.entries()].sort((a, b) => b[1] - a[1]).map(([sender]) => sender)
}

function parseBlock(block: Element): ChatMessage | null {
  const sender = block.querySelector(SENDER)
  const timestamp = block.querySelector(TIMESTAMP)

  // Header blocks have no sender, and group photo blocks have no timestamp. Neither is a message.
  if (!sender || !timestamp) return null

  const textDiv = block.querySelector(CONTENT)?.firstElementChild?.children[TEXT_DIV_INDEX]
  const images = [...block.querySelectorAll('img')].map((image) => mediaPath(image.getAttribute('src'), IMAGE_FOLDERS))
  const videos = [...block.querySelectorAll('video')].map((video) => mediaPath(video.getAttribute('src'), VIDEO_FOLDERS))

  return {
    sender: (sender.textContent ?? '').trim(),
    text: textDiv ? extractText(textDiv).trim() : '',
    sentAt: parseTimestamp(timestamp.textContent ?? ''),
    reactions: [...block.querySelectorAll(REACTIONS)].map(parseReaction).filter((reaction) => reaction !== null),
    hasMedia: block.querySelector('img, video, audio') !== null,
    media: [...new Set([...images, ...videos].filter((path) => path !== null))],
  }
}

// Sources look like "your_facebook_activity/messages/inbox/chat_123/photos/456.jpg".
// Keeping the part from the media folder matches the paths inside the chat folder.
export function mediaPath(source: string | null, folders: string[]): string | null {
  if (!source) return null

  const normalised = source.replaceAll('\\', '/')
  for (const folder of folders) {
    if (normalised.startsWith(folder)) return normalised

    const index = normalised.lastIndexOf('/' + folder)
    if (index >= 0) return normalised.slice(index + 1)
  }
  return null
}

function extractText(node: Node): string {
  let text = ''
  for (const child of node.childNodes) {
    if (child.nodeType === Node.TEXT_NODE) {
      text += (child as Text).data
    } else if (child.nodeName === 'BR') {
      text += '\n'
    } else {
      text += extractText(child)
    }
  }
  return text
}

const graphemes = new Intl.Segmenter('en', { granularity: 'grapheme' })

function parseReaction(item: Element): Reaction | null {
  const text = (item.textContent ?? '').trim()
  if (!text) return null

  // One emoji can be several code points, so split on the first whole character.
  const emoji = graphemes.segment(text)[Symbol.iterator]().next().value!.segment
  return { emoji, reactor: text.slice(emoji.length).trim() }
}

export function parseTimestamp(value: string): number | null {
  const match = TIMESTAMP_PATTERN.exec(value.trim())
  if (!match) return null

  const [, monthName, day, year, hour, minute, second, half] = match
  const month = MONTHS.indexOf(monthName.toLowerCase())
  const hours = Number(hour)
  if (month < 0 || hours < 1 || hours > 12) return null

  const hours24 = (hours % 12) + (half.toLowerCase() === 'pm' ? 12 : 0)
  const date = new Date(Date.UTC(Number(year), month, Number(day), hours24, Number(minute), Number(second)))
  // Rejects dates that roll over, such as Feb 30.
  if (date.getUTCDate() !== Number(day) || Number(minute) > 59 || Number(second) > 59) return null

  return date.getTime() / 1000
}
