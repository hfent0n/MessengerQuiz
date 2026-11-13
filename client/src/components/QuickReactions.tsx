import { QUICK_REACTIONS, type PlayerView, type Reaction } from '../rooms'

type Props = {
  reactions: Reaction[]
  players: PlayerView[]
  onReact: (emoji: string) => void
}

// A row of reaction buttons, and the reactions everyone sends floating up the screen.
export default function QuickReactions({ reactions, players, onReact }: Props) {
  const names = new Map(players.map((player) => [player.id, player.name]))

  return (
    <>
      <div className="quick-reactions" role="group" aria-label="React">
        {QUICK_REACTIONS.map((emoji) => (
          <button key={emoji} type="button" className="quick-reaction" aria-label={`React ${emoji}`} onClick={() => onReact(emoji)}>
            {emoji}
          </button>
        ))}
      </div>
      <div className="floating-reactions" aria-hidden="true">
        {reactions.map((reaction) => (
          <span key={reaction.id} className="floating-reaction" style={{ left: `${floatPosition(reaction.id)}%` }}>
            <span className="floating-emoji">{reaction.emoji}</span>
            <span className="floating-name">{names.get(reaction.playerId) ?? ''}</span>
          </span>
        ))}
      </div>
    </>
  )
}

// Spreads reactions across the screen, so several at once don't stack on top of each other.
function floatPosition(id: number): number {
  return 10 + ((id * 37) % 80)
}
