import { useEffect, useRef, useState, type FormEvent } from 'react'
import { labels, open, seal } from '../chat/sealing'
import { cleanChatMessage, MAX_CHAT_LENGTH, type ChatLine, type PlayerView } from '../rooms'

type Props = {
  lines: ChatLine[]
  players: PlayerView[]
  youId: string
  // The game link's key. Messages are sealed with it, so the server can't read them.
  roomKey: CryptoKey
  onSend: (sealed: string) => Promise<string | null>
}

const UNREADABLE = "This message couldn't be decrypted."

export default function GameChat({ lines, players, youId, roomKey, onSend }: Props) {
  const [texts, setTexts] = useState<ReadonlyMap<number, string>>(new Map())
  const [draft, setDraft] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sending, setSending] = useState(false)
  const list = useRef<HTMLOListElement>(null)

  // Opens messages that haven't been opened yet. Each only opens with its sender's label.
  useEffect(() => {
    const unopened = lines.filter((line) => !texts.has(line.id))
    if (unopened.length === 0) return

    let current = true
    void Promise.all(
      unopened.map(async (line) => {
        try {
          return [line.id, await open<string>(roomKey, labels.chatLine(line.playerId), line.sealed)] as const
        } catch {
          return [line.id, UNREADABLE] as const
        }
      }),
    ).then((opened) => {
      if (current) setTexts((previous) => new Map([...previous, ...opened]))
    })
    return () => {
      current = false
    }
  }, [lines, texts, roomKey])

  // Keeps the newest message in view.
  const lastId = lines.at(-1)?.id
  useEffect(() => {
    list.current?.scrollTo({ top: list.current.scrollHeight })
  }, [lastId, texts.size])

  async function send(event: FormEvent) {
    event.preventDefault()
    const text = cleanChatMessage(draft)
    if (!text) return

    setSending(true)
    const refused = await onSend(await seal(roomKey, labels.chatLine(youId), text))
    setSending(false)
    setError(refused)
    if (!refused) setDraft('')
  }

  const names = new Map(players.map((player) => [player.id, player.name]))

  return (
    <section className="game-chat" aria-label="Chat">
      <h3 className="game-chat-heading">Chat</h3>
      <ol className="game-chat-lines" ref={list} aria-live="polite">
        {lines.length === 0 && <li className="note">No messages yet. Say hello!</li>}
        {lines.map((line) => (
          <li key={line.id} className={line.playerId === youId ? 'game-chat-line you' : 'game-chat-line'}>
            <span className="game-chat-name">{names.get(line.playerId) ?? 'Someone'}</span>{' '}
            <span className={texts.get(line.id) === UNREADABLE ? 'note' : undefined}>{texts.get(line.id) ?? '…'}</span>
          </li>
        ))}
      </ol>
      <form className="game-chat-form" onSubmit={send}>
        <input
          className="text-input"
          value={draft}
          maxLength={MAX_CHAT_LENGTH}
          placeholder="Message everyone"
          aria-label="Chat message"
          onChange={(event) => setDraft(event.target.value)}
        />
        <button type="submit" className="button" disabled={sending || !cleanChatMessage(draft)}>Send</button>
      </form>
      {error && <p role="alert" className="error">{error}</p>}
    </section>
  )
}
