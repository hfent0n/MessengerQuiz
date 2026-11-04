import { useState, type ChangeEvent, type InputHTMLAttributes } from 'react'
import type { ActiveChat } from '../activeChat'
import { uploadChat } from '../api'
import { chatFromFolder, chatLabel, groupChats, mediaKind, type ChatSource, type ExportFile } from '../chatFiles'

type Props = {
  onUploaded: (chat: ActiveChat, media: ReadonlyMap<string, ExportFile>) => void
}

type Step = { kind: 'pick' } | { kind: 'reading' } | { kind: 'choose'; chats: ChatSource[] } | { kind: 'uploading' }

const folderPickerProps = { webkitdirectory: '' } as InputHTMLAttributes<HTMLInputElement>

export default function SelectChatScreen({ onUploaded }: Props) {
  const [step, setStep] = useState<Step>({ kind: 'pick' })
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState('')

  async function handleZips(event: ChangeEvent<HTMLInputElement>) {
    const zips = [...(event.target.files ?? [])]
    event.target.value = ''
    if (zips.length === 0) return

    setError(null)
    setStep({ kind: 'reading' })

    try {
      // The zip library is large, so it loads only when it's needed.
      const { readZips } = await import('../zipExport')
      const chats = groupChats(await readZips(zips))
      if (chats.length === 0) {
        setError('No Messenger chats were found. Choose the zips from an HTML export of your Facebook information.')
        setStep({ kind: 'pick' })
        return
      }
      setFilter('')
      setStep({ kind: 'choose', chats })
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Those zips couldn't be read.")
      setStep({ kind: 'pick' })
    }
  }

  function handleFolder(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])]
    event.target.value = ''
    if (files.length === 0) return

    const chat = chatFromFolder(files)
    if (!chat) {
      setError("That isn't a chat folder. Open messages/inbox in your export and choose one chat's folder.")
      return
    }
    void upload(chat)
  }

  async function upload(chat: ChatSource) {
    const previous = step
    setError(null)
    setStep({ kind: 'uploading' })

    try {
      const { chatId, title } = await uploadChat(chat)
      onUploaded({ chatId, title }, chat.media)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Something went wrong. Try again.')
      setStep(previous)
    }
  }

  if (step.kind === 'choose') {
    const query = filter.trim().toLowerCase()
    const shown = step.chats.filter((chat) => chatLabel(chat).toLowerCase().includes(query))

    return (
      <section>
        <p>Which chat do you want to play?</p>
        <input
          type="search"
          className="chat-filter"
          placeholder="Search chats"
          aria-label="Search chats"
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
        />
        <ul className="chat-list">
          {shown.map((chat) => (
            <li key={chat.folder}>
              <button type="button" className="chat-option" onClick={() => void upload(chat)}>
                <span className="chat-name">{chatLabel(chat)}</span>
                <span className="note">{chatSummary(chat)}</span>
              </button>
            </li>
          ))}
        </ul>
        {shown.length === 0 && <p className="note">No chats match.</p>}
        {error && <p role="alert" className="error">{error}</p>}
        <button type="button" className="link" onClick={() => setStep({ kind: 'pick' })}>
          Choose different zips
        </button>
      </section>
    )
  }

  const busy = step.kind !== 'pick'

  return (
    <section>
      <p>Find out who really knows your group chat.</p>
      <label className={busy ? 'button primary busy' : 'button primary'}>
        {step.kind === 'reading' ? 'Reading zips…' : step.kind === 'uploading' ? 'Uploading…' : 'Choose your export zips'}
        <input
          type="file"
          accept=".zip,application/zip"
          multiple
          className="visually-hidden"
          disabled={busy}
          onChange={handleZips}
        />
      </label>
      {error && <p role="alert" className="error">{error}</p>}
      <p className="note">
        Download your Facebook information in HTML format, then choose every zip it came in at once. A chat's photos
        and videos can be spread across them.
      </p>
      <p className="note">
        Only the chosen chat's messages are uploaded. They're held in memory, never saved to disk, and deleted after 2
        idle hours. Photos and videos are shown from your own files and never leave your device.
      </p>
      <label className={busy ? 'link busy' : 'link'}>
        Or choose an unzipped chat folder
        <input
          type="file"
          className="visually-hidden"
          disabled={busy}
          onChange={handleFolder}
          {...folderPickerProps}
        />
      </label>
    </section>
  )
}

function chatSummary(chat: ChatSource): string {
  const pages = `${chat.pages.length} ${chat.pages.length === 1 ? 'page' : 'pages'}`
  const counts = { photo: 0, gif: 0, video: 0 }
  chat.media.forEach((_, path) => counts[mediaKind(path)]++)

  const parts = [
    pages,
    counted(counts.photo, 'photo', 'photos'),
    counted(counts.gif, 'GIF', 'GIFs'),
    counted(counts.video, 'video', 'videos'),
  ]
  return parts.filter(Boolean).join(', ')
}

function counted(count: number, one: string, many: string): string {
  return count === 0 ? '' : `${count} ${count === 1 ? one : many}`
}
