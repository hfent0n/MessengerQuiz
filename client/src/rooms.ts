import { ApiError } from './api'
import type { SealedContext } from './chat/openQuestion'

export const TIME_LIMITS = [10, 20, 30, 60] as const
export const DEFAULT_TIME_LIMIT = 20
export const MAX_NAME_LENGTH = 20
export const MAX_CHAT_LENGTH = 200

// The only reactions the server accepts.
export const QUICK_REACTIONS = ['😂', '🔥', '😮', '😭', '👏', '🤔'] as const

export type RoomStage = 'lobby' | 'question' | 'reveal' | 'finished' | 'ended'

export type PlayerView = {
  id: string
  name: string
  isHost: boolean
  connected: boolean
  score: number
  answered: boolean
  ready: boolean
}

// What this player sees. The answer only arrives with the reveal.
// Message text and people's names arrive sealed. The key from the game link opens them in the browser.
export type RoomView = {
  code: string
  version: number
  title: string
  people: string
  stage: RoomStage
  timeLimitSeconds: number
  youId: string
  youAreHost: boolean
  maxPlayers: number
  // Ranked by score, then by total answer time.
  players: PlayerView[]
  questionNumber: number
  questionCount: number
  maxScore: number
  question: {
    position: number
    sealed: string
    context: SealedContext[]
    // People's numbers.
    options: number[]
    remainingMilliseconds: number
    yourAnswer: number | null
  } | null
  reveal: {
    answer: number
    reactions: number
    picks: { playerId: string; option: number | null; points: number }[]
  } | null
  // The latest chat messages, oldest first, sealed with the game link's key.
  chat: ChatLine[]
}

export type ChatLine = { id: number; playerId: string; sealed: string }

export type Reaction = { id: number; playerId: string; emoji: string }

// Trimmed, and no longer than the limit.
export function cleanChatMessage(text: string): string | null {
  const trimmed = text.trim()
  return trimmed.length > 0 && trimmed.length <= MAX_CHAT_LENGTH ? trimmed : null
}

type JoinedRoom = { code: string; playerToken: string }

export async function createRoom(chatId: string, name: string, timeLimitSeconds: number): Promise<JoinedRoom> {
  return postRoom('/api/rooms', { name, timeLimitSeconds }, { 'X-Chat-Id': chatId })
}

export async function joinRoom(code: string, name: string): Promise<JoinedRoom> {
  return postRoom(`/api/rooms/${encodeURIComponent(code)}/players`, { name })
}

async function postRoom(url: string, body: object, headers: Record<string, string> = {}): Promise<JoinedRoom> {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    if (response.status === 429) throw new ApiError(429, 'Too many tries. Wait a minute and try again.')
    const problem = await response.json().catch(() => null)
    throw new ApiError(response.status, problem?.detail ?? 'Something went wrong. Try again.')
  }
  return response.json()
}

// Mirrors the server's rule, so most mistakes are caught before sending.
export function isValidName(name: string): boolean {
  const cleaned = name.trim().normalize('NFC')
  return cleaned.length > 0 && cleaned.length <= MAX_NAME_LENGTH && /^[\p{L}\p{M}\p{N}]+( [\p{L}\p{M}\p{N}]+)*$/u.test(cleaned)
}

// The token lets this tab rejoin its seat after a refresh. sessionStorage is cleared when the tab closes.
const tokenKey = (code: string) => `messenger-quiz-room-${code}`

export function loadRoomToken(code: string): string | null {
  try {
    return sessionStorage.getItem(tokenKey(code))
  } catch {
    return null
  }
}

export function saveRoomToken(code: string, token: string | null): void {
  try {
    if (token) {
      sessionStorage.setItem(tokenKey(code), token)
    } else {
      sessionStorage.removeItem(tokenKey(code))
    }
  } catch {
    // Without storage the game still works, but a refresh loses the seat.
  }
}
