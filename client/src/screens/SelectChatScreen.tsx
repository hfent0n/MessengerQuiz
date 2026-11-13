import { useState, type ChangeEvent, type InputHTMLAttributes } from 'react'
import type { ActiveChat } from '../activeChat'
import { uploadChat } from '../api'
import { demoChat, demoMedia } from '../chat/demoChat'
import { ChatParseError, parseChat, type Chat } from '../chat/parseChat'
import { chatFromFolder, chatLabel, groupChats, mediaKind, type ChatSource, type ExportFile } from '../chatFiles'
import { isStaleVersionError, NEW_VERSION_MESSAGE } from '../newVersion'
import { navigate } from '../routes'

type Props = {
  onUploaded: (chat: ActiveChat, media: ReadonlyMap<string, ExportFile>) => void
}

type Step =
  | { kind: 'pick' }
  | { kind: 'reading' }
  | { kind: 'choose'; chats: ChatSource[] }
  | { kind: 'uploading' }
  | { kind: 'demo' }

const folderPickerProps = { webkitdirectory: '' } as InputHTMLAttributes<HTMLInputElement>

export default function SelectChatScreen({ onUploaded }: Props) {
  const [step, setStep] = useState<Step>({ kind: 'pick' })
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState('')
  // The player confirms they're in the chat before using anyone else's messages.
  const [agreed, setAgreed] = useState(false)

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
      if (isStaleVersionError(caught)) {
        setError(NEW_VERSION_MESSAGE)
      } else {
        setError(caught instanceof Error ? caught.message : "Those zips couldn't be read.")
      }
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
      // Read and encrypted here. The server only gets sealed text and names.
      const pages = await Promise.all(chat.pages.map(async (page) => (await page.load()).text()))
      const parsed = parseChat(pages)
      await send(parsed, chat.media, false)
    } catch (caught) {
      setError(caught instanceof ChatParseError || caught instanceof Error ? caught.message : 'Something went wrong. Try again.')
      setStep(previous)
    }
  }

  // The demo goes through the same encryption and upload as a real chat.
  async function playDemo() {
    setError(null)
    setStep({ kind: 'demo' })

    try {
      await send(demoChat(), demoMedia(), true)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Something went wrong. Try again.')
      setStep({ kind: 'pick' })
    }
  }

  async function send(chat: Chat, media: ReadonlyMap<string, ExportFile>, demo: boolean) {
    const { chatId } = await uploadChat(chat, new Set(media.keys()))
    onUploaded({ chatId, title: chat.title, people: chat.people, ...(demo && { demo }) }, media)
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
  const locked = busy || !agreed

  return (
    <section>
      <p>Find out who really knows your group chat.</p>
      <div className="actions">
        <button type="button" className={step.kind === 'demo' ? 'button primary busy' : 'button primary'} disabled={busy} onClick={playDemo}>
          {step.kind === 'demo' ? 'Starting demo…' : 'Try a demo chat'}
        </button>
      </div>
      <p className="note">The demo is a made-up group chat between four flatmates, playable alone or with friends.</p>

      <h2 className="section-heading">Play your own chat</h2>
      <p className="note">
        Download your Facebook information in HTML format, then choose every zip it came in at once. A chat's photos
        and videos can be spread across them.
      </p>
      <p className="note">
        Your chat is read and <strong>encrypted on this device</strong> before it's uploaded. The server can't read
        messages or names, and deletes the chat after 2 idle hours. Photos and videos never leave your device.{' '}
        <a href="/privacy" onClick={(event) => { event.preventDefault(); navigate('/privacy') }}>What the server sees</a>
      </p>
      <label className="consent">
        <input type="checkbox" checked={agreed} disabled={busy} onChange={(event) => setAgreed(event.target.checked)} />
        <span>
          I'm in this chat, and I'll only share game links with other people in it. Their messages are theirs too.
        </span>
      </label>
      <div className="actions">
        <label className={['button', busy && step.kind !== 'demo' ? 'busy' : '', locked ? 'disabled' : ''].filter(Boolean).join(' ')} aria-disabled={locked}>
          {zipButtonLabel(step)}
          <input
            type="file"
            accept=".zip,application/zip"
            multiple
            className="visually-hidden"
            disabled={locked}
            onChange={handleZips}
          />
        </label>
      </div>
      {error && <p role="alert" className="error">{error}</p>}
      <label className={locked ? 'link disabled' : 'link'} aria-disabled={locked}>
        Or choose an unzipped chat folder
        <input
          type="file"
          className="visually-hidden"
          disabled={locked}
          onChange={handleFolder}
          {...folderPickerProps}
        />
      </label>
    </section>
  )
}

function zipButtonLabel(step: Step): string {
  if (step.kind === 'reading') return 'Reading zips…'
  if (step.kind === 'uploading') return 'Encrypting and uploading…'
  return 'Choose your export zips'
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
