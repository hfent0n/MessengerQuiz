import type { ContextMessage } from './api'
import { mediaKind } from './chatFiles'

export function questionPrompt({ text, media, context }: { text: string; media: string[]; context: ContextMessage[] }): string {
  if (context.length > 0) return 'Who sent these messages?'
  if (media.length === 0 || text) return 'Who said this?'
  return `Who sent this ${PROMPT_NAMES[mediaKind(media[0])]}?`
}

const PROMPT_NAMES = { photo: 'photo', gif: 'GIF', video: 'video' } as const

export function reactionSummary(reactions: number): string {
  if (reactions === 0) return ''
  return reactions === 1 ? 'It got 1 reaction.' : `It got ${reactions} reactions.`
}
