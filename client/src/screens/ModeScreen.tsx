import type { ActiveChat, QuestionSelection } from '../activeChat'

type Props = {
  chat: ActiveChat
  onChoose: (mode: 'single' | 'multi') => void
  onSelect: (selection: QuestionSelection) => void
  onWipe: () => void
}

const SELECTIONS: { value: QuestionSelection; name: string; note: string }[] = [
  { value: 'reacted', name: 'Most reacted', note: "The chat's best-known moments." },
  { value: 'random', name: 'Random', note: 'Any message, shown with the run of messages that led to it.' },
]

export default function ModeScreen({ chat, onChoose, onSelect, onWipe }: Props) {
  const selection = chat.selection ?? 'reacted'

  return (
    <section>
      <h2>{chat.title}</h2>
      <fieldset className="field">
        <legend className="field-label">Which messages?</legend>
        <div className="choices">
          {SELECTIONS.map((option) => (
            <label key={option.value} className={option.value === selection ? 'choice chosen' : 'choice'}>
              <input
                type="radio"
                name="selection"
                className="visually-hidden"
                checked={option.value === selection}
                onChange={() => onSelect(option.value)}
              />
              {option.name}
            </label>
          ))}
        </div>
        <p className="note">{SELECTIONS.find((option) => option.value === selection)!.note}</p>
      </fieldset>
      <p>How do you want to play?</p>
      <div className="mode-options">
        <button type="button" className="mode-option" onClick={() => onChoose('single')}>
          <span className="mode-name">Single player</span>
          <span className="note">Work through the chat at your own pace.</span>
        </button>
        <button type="button" className="mode-option" onClick={() => onChoose('multi')}>
          <span className="mode-name">Multiplayer</span>
          <span className="note">Up to 8 people, against the clock. Everyone plays on their own device.</span>
        </button>
      </div>
      <button className="link" onClick={onWipe}>Wipe my chat and exit</button>
    </section>
  )
}
