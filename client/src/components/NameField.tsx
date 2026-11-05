import { MAX_NAME_LENGTH } from '../rooms'

type Props = {
  value: string
  onChange: (value: string) => void
}

export default function NameField({ value, onChange }: Props) {
  return (
    <label className="field">
      <span className="field-label">Your name</span>
      <input
        type="text"
        className="text-input"
        value={value}
        maxLength={MAX_NAME_LENGTH}
        autoComplete="nickname"
        autoFocus
        onChange={(event) => onChange(event.target.value)}
      />
      <span className="note">Up to 20 letters and numbers. Spaces are fine between words.</span>
    </label>
  )
}
