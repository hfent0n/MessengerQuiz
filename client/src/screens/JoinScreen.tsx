import { useState, type FormEvent } from 'react'
import NameField from '../components/NameField'
import { isValidName, joinRoom, saveRoomToken } from '../rooms'
import { navigate } from '../routes'

type Props = {
  code: string
  onJoined: (token: string) => void
}

export default function JoinScreen({ code, onJoined }: Props) {
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [joining, setJoining] = useState(false)

  async function join(event: FormEvent) {
    event.preventDefault()
    if (!isValidName(name)) {
      setError('Names can be up to 20 letters and numbers, with single spaces between words.')
      return
    }

    setError(null)
    setJoining(true)
    try {
      const { playerToken } = await joinRoom(code, name)
      saveRoomToken(code, playerToken)
      onJoined(playerToken)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Something went wrong. Try again.')
      setJoining(false)
    }
  }

  return (
    <section>
      <h2>Join the game</h2>
      <p>Someone has invited you to guess who said what in their group chat.</p>
      <form className="form" onSubmit={join}>
        <NameField value={name} onChange={setName} />
        {error && <p role="alert" className="error">{error}</p>}
        <div className="actions">
          <button type="submit" className={joining ? 'button primary busy' : 'button primary'} disabled={joining}>
            {joining ? 'Joining…' : 'Join'}
          </button>
          <button type="button" className="button" onClick={() => navigate('/')} disabled={joining}>
            Cancel
          </button>
        </div>
      </form>
    </section>
  )
}
