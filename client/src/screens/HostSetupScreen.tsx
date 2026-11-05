import { useState, type FormEvent } from 'react'
import type { ActiveChat } from '../activeChat'
import NameField from '../components/NameField'
import { createRoom, DEFAULT_TIME_LIMIT, isValidName, saveRoomToken, TIME_LIMITS } from '../rooms'
import { navigate, playPath } from '../routes'

type Props = {
  chat: ActiveChat
  onBack: () => void
}

export default function HostSetupScreen({ chat, onBack }: Props) {
  const [name, setName] = useState('')
  const [timeLimit, setTimeLimit] = useState<number>(DEFAULT_TIME_LIMIT)
  const [error, setError] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)

  async function create(event: FormEvent) {
    event.preventDefault()
    if (!isValidName(name)) {
      setError('Names can be up to 20 letters and numbers, with single spaces between words.')
      return
    }

    setError(null)
    setCreating(true)
    try {
      const room = await createRoom(chat.chatId, name, timeLimit)
      saveRoomToken(room.code, room.playerToken)
      navigate(playPath(room.code))
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Something went wrong. Try again.')
      setCreating(false)
    }
  }

  return (
    <section>
      <h2>Multiplayer</h2>
      <form className="form" onSubmit={create}>
        <NameField value={name} onChange={setName} />

        <fieldset className="field">
          <legend className="field-label">Time per question</legend>
          <div className="choices">
            {TIME_LIMITS.map((seconds) => (
              <label key={seconds} className={seconds === timeLimit ? 'choice chosen' : 'choice'}>
                <input
                  type="radio"
                  name="time-limit"
                  className="visually-hidden"
                  checked={seconds === timeLimit}
                  onChange={() => setTimeLimit(seconds)}
                />
                {seconds} seconds
              </label>
            ))}
          </div>
        </fieldset>

        <p className="note">
          Multiplayer only uses text messages. Photos and videos stay on your device, so the other players can't see
          them.
        </p>

        {error && <p role="alert" className="error">{error}</p>}
        <div className="actions">
          <button type="submit" className={creating ? 'button primary busy' : 'button primary'} disabled={creating}>
            {creating ? 'Creating…' : 'Create game'}
          </button>
          <button type="button" className="button" onClick={onBack} disabled={creating}>Back</button>
        </div>
      </form>
    </section>
  )
}
