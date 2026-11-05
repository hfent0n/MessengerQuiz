import { useEffect, useState } from 'react'
import MessageThread from '../components/MessageThread'
import { questionPrompt, reactionSummary } from '../questionText'
import type { PlayerView, RoomView } from '../rooms'
import { playPath } from '../routes'
import { useRoom } from '../useRoom'

type Props = {
  code: string
  token: string
  // The host can end the game by wiping their chat. Other players can only leave.
  onWipe: () => void
  onLeave: () => void
}

// Multiplayer has no media, so the thread never needs files.
const NO_FILES = new Map()

export default function GameScreen({ code, token, onWipe, onLeave }: Props) {
  const { view, error, reconnecting, send } = useRoom(code, token)
  const [actionError, setActionError] = useState<string | null>(null)

  async function act(method: 'Start' | 'Answer' | 'Ready', ...args: string[]) {
    setActionError(await send(method, ...args))
  }

  if (error || view?.stage === 'ended') {
    return (
      <section>
        <h2>Game over</h2>
        <p role="alert">{view?.stage === 'ended' ? 'The host ended the game.' : error}</p>
        <button className="button primary" onClick={onLeave}>Back to the start</button>
      </section>
    )
  }

  if (!view) return <p className="note">Connecting…</p>

  const playing = view.stage === 'question' || view.stage === 'reveal'

  return (
    <section>
      <header className="quiz-header">
        <h2>Multiplayer</h2>
        {playing && (
          <p className="score">
            {view.questionNumber} / {view.questionCount}
          </p>
        )}
      </header>
      {reconnecting && <p className="note" role="status">Reconnecting…</p>}

      {view.stage === 'lobby' && <Lobby view={view} onStart={() => act('Start')} />}

      {playing && (
        <div className="game">
          <ScoreBars view={view} />
          <Round view={view} onAnswer={(option) => act('Answer', option)} onReady={() => act('Ready')} />
        </div>
      )}

      {view.stage === 'finished' && <Results view={view} onRematch={() => act('Start')} />}

      {actionError && <p role="alert" className="error">{actionError}</p>}

      {view.youAreHost ? (
        <button className="link" onClick={onWipe}>End the game and wipe my chat</button>
      ) : (
        <button className="link" onClick={onLeave}>Leave the game</button>
      )}
    </section>
  )
}

function Lobby({ view, onStart }: { view: RoomView; onStart: () => void }) {
  const link = `${location.origin}${playPath(view.code)}`
  const others = view.players.length - 1

  return (
    <>
      {view.youAreHost ? (
        <>
          <p>Send this link to the people you want to play with:</p>
          <CopyLink link={link} />
          <p className="warning">
            Anyone with this link will see messages from this chat. Only share it with people in the chat.
          </p>
        </>
      ) : (
        <p>You're in. Waiting for the host to start the game.</p>
      )}

      <h3 className="players-heading">
        Players ({view.players.length} of {view.maxPlayers})
      </h3>
      <ul className="lobby-players">
        {view.players.map((player) => (
          <li key={player.id}>
            {player.name}
            {player.isHost && <span className="tag">host</span>}
            {player.id === view.youId && <span className="tag">you</span>}
            {!player.connected && <span className="tag">away</span>}
          </li>
        ))}
      </ul>

      <p className="note">{view.timeLimitSeconds} seconds per question.</p>

      {view.youAreHost && (
        <button className="button primary" disabled={others < 1} onClick={onStart}>
          {others < 1 ? 'Waiting for players…' : 'Start the game'}
        </button>
      )}
    </>
  )
}

function CopyLink({ link }: { link: string }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(link)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // Clipboard access can be refused. The link is still there to select by hand.
    }
  }

  return (
    <div className="copy-link">
      <input className="text-input" readOnly value={link} aria-label="Game link" onFocus={(event) => event.target.select()} />
      <button type="button" className="button" onClick={copy}>{copied ? 'Copied' : 'Copy'}</button>
    </div>
  )
}

function ScoreBars({ view }: { view: RoomView }) {
  const points = new Map(view.reveal?.picks.map((pick) => [pick.playerId, pick.points]))

  return (
    <ol className="score-bars" aria-label="Scores">
      {view.players.map((player) => (
        <li key={player.id} className={player.id === view.youId ? 'score-bar you' : 'score-bar'}>
          <div className="score-bar-label">
            <span className="score-bar-name">{player.name}</span>
            <span className="score-bar-status">{playerStatus(view, player)}</span>
            <span className="score-bar-score">
              {player.score}
              {view.stage === 'reveal' && <span className="points"> +{points.get(player.id) ?? 0}</span>}
            </span>
          </div>
          <div className="bar" aria-hidden="true">
            <div className="bar-fill" style={{ width: `${(player.score / Math.max(view.maxScore, 1)) * 100}%` }} />
          </div>
        </li>
      ))}
    </ol>
  )
}

function playerStatus(view: RoomView, player: PlayerView): string {
  if (!player.connected) return 'left'
  if (view.stage === 'question') return player.answered ? 'answered' : ''
  if (view.stage === 'reveal') return player.ready ? 'ready' : ''
  return ''
}

function Round({ view, onAnswer, onReady }: { view: RoomView; onAnswer: (option: string) => void; onReady: () => void }) {
  const question = view.question!
  const reveal = view.reveal
  const you = view.players.find((player) => player.id === view.youId)
  const pickers = (option: string) =>
    view.players.filter((player) => reveal?.picks.some((pick) => pick.playerId === player.id && pick.option === option))
  const waitingFor = view.players.filter((player) => player.connected && !(reveal ? player.ready : player.answered))

  return (
    <div className="round">
      <p className="prompt">{questionPrompt({ text: question.text, media: [], context: question.context })}</p>
      {view.stage === 'question' && (
        <Countdown key={view.questionNumber} remaining={question.remainingMilliseconds} total={view.timeLimitSeconds * 1000} />
      )}
      <MessageThread text={question.text} media={[]} context={question.context} files={NO_FILES} />

      <div className="options">
        {question.options.map((option) => (
          <button
            key={option}
            className={optionClass(view, option)}
            disabled={view.stage !== 'question' || question.yourAnswer !== null}
            onClick={() => onAnswer(option)}
          >
            {option}
            {reveal && pickers(option).length > 0 && (
              <span className="pickers">{pickers(option).map((player) => player.name).join(', ')}</span>
            )}
          </button>
        ))}
      </div>

      {view.stage === 'question' && question.yourAnswer !== null && (
        <p className="note" role="status">Waiting for {names(waitingFor)}…</p>
      )}

      {reveal && (
        <div className="result">
          <p role="status">
            {revealSummary(view)} {reactionSummary(reveal.reactions)}
          </p>
          {you?.ready ? (
            <p className="note">Waiting for {names(waitingFor)}…</p>
          ) : (
            <button className="button primary" autoFocus onClick={onReady}>
              {view.questionNumber === view.questionCount ? 'See the results' : 'Ready for the next one'}
            </button>
          )}
        </div>
      )}
    </div>
  )
}

function revealSummary(view: RoomView): string {
  const reveal = view.reveal!
  const yours = reveal.picks.find((pick) => pick.playerId === view.youId)
  if (!yours?.option) return `Out of time. It was ${reveal.answer}.`
  return yours.option === reveal.answer ? `Correct! +${yours.points} points.` : `It was ${reveal.answer}.`
}

function optionClass(view: RoomView, option: string): string {
  const question = view.question!
  if (view.reveal) {
    if (option === view.reveal.answer) return 'button option correct'
    return option === question.yourAnswer ? 'button option wrong' : 'button option'
  }
  return option === question.yourAnswer ? 'button option chosen' : 'button option'
}

function names(players: PlayerView[]): string {
  if (players.length === 0) return 'everyone'
  if (players.length === 1) return players[0].name
  return `${players.slice(0, -1).map((player) => player.name).join(', ')} and ${players[players.length - 1].name}`
}

function Countdown({ remaining, total }: { remaining: number; total: number }) {
  // The server sends the time left rather than a deadline, because players' clocks differ.
  const [deadline] = useState(() => performance.now() + remaining)
  const [now, setNow] = useState(() => performance.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(performance.now()), 200)
    return () => clearInterval(timer)
  }, [])

  const left = Math.max(0, deadline - now)
  return (
    <div className="countdown" role="timer" aria-label={`${Math.ceil(left / 1000)} seconds left`}>
      <div className="countdown-fill" style={{ width: `${(left / total) * 100}%` }} />
      <span className="countdown-text">{Math.ceil(left / 1000)}s</span>
    </div>
  )
}

function Results({ view, onRematch }: { view: RoomView; onRematch: () => void }) {
  const [first, second] = view.players
  const headline = second && second.score === first.score ? `${first.name} wins on speed!` : `${first.name} wins!`

  return (
    <>
      <p className="headline">{headline}</p>
      <ol className="results">
        {view.players.map((player, index) => (
          <li key={player.id} className={player.id === view.youId ? 'you' : undefined}>
            <span className="place">{index + 1}</span>
            <span className="results-name">{player.name}</span>
            <span className="results-score">{player.score} points</span>
          </li>
        ))}
      </ol>
      {view.youAreHost ? (
        <button className="button primary" onClick={onRematch}>Rematch with new questions</button>
      ) : (
        <p className="note">Waiting for the host to start a rematch.</p>
      )}
    </>
  )
}
