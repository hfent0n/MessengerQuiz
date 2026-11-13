// Lines the export writes for events rather than messages, such as "Ana named the group".
// Real messages can match these phrases, but losing the odd one from the quiz is harmless.
const MAX_SYSTEM_LINE_LENGTH = 120

const SYSTEM_LINE =
  /reacted .{1,12} to|joined the (video )?call|started a (video )?call|set the nickname|set (his|her|their|your) own|cleared the nickname|named the group|sent an attachment|sent a live|missed a|called you|changed the|left the group|added .+ to the group|removed .+ from|unsent a message|pinned|voted for|created (a|the) poll|set the quick reaction|see when .+ open this chat/i

const LINK_ONLY = /^https?:\/\/\S+$/i

// Shorter messages are too hard to recognise.
export const MIN_QUESTION_LENGTH = 30

export function isSystemLine(text: string): boolean {
  return text.length <= MAX_SYSTEM_LINE_LENGTH && SYSTEM_LINE.test(text)
}

export function isLinkOnly(text: string): boolean {
  return LINK_ONLY.test(text)
}

// Photos, GIFs and videos count whatever their caption. Text needs to be long enough to be recognisable.
export function canBeQuestion(text: string, media: string[]): boolean {
  return media.length > 0 || (text.length >= MIN_QUESTION_LENGTH && !isSystemLine(text) && !isLinkOnly(text))
}
