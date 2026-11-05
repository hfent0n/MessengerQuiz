import type { ActiveChat } from '../activeChat'

type Props = {
  chat: ActiveChat
  onChoose: (mode: 'single' | 'multi') => void
  onWipe: () => void
}

export default function ModeScreen({ chat, onChoose, onWipe }: Props) {
  return (
    <section>
      <h2>{chat.title}</h2>
      <p>How do you want to play?</p>
      <div className="mode-options">
        <button type="button" className="mode-option" onClick={() => onChoose('single')}>
          <span className="mode-name">Single player</span>
          <span className="note">Work through the chat at your own pace.</span>
        </button>
        <button type="button" className="mode-option" onClick={() => onChoose('multi')}>
          <span className="mode-name">Multiplayer</span>
          <span className="note">Up to 8 people, 10 questions, against the clock. Everyone plays on their own device.</span>
        </button>
      </div>
      <button className="link" onClick={onWipe}>Wipe my chat and exit</button>
    </section>
  )
}
