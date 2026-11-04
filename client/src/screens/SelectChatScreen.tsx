import { useState, type ChangeEvent, type InputHTMLAttributes } from 'react'
import type { ActiveChat } from '../activeChat'
import { uploadChat } from '../api'
import { findChatPages, findChatPhotos } from '../chatFiles'

type Props = {
  onUploaded: (chat: ActiveChat, photos: ReadonlyMap<string, File>) => void
}

const folderPickerProps = { webkitdirectory: '' } as InputHTMLAttributes<HTMLInputElement>

export default function SelectChatScreen({ onUploaded }: Props) {
  const [error, setError] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)

  async function handleFolder(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])]
    event.target.value = ''
    if (files.length === 0) return

    const pages = findChatPages(files)
    if (pages.length === 0) {
      setError("That isn't a chat folder. Open messages/inbox in your export and choose one chat's folder.")
      return
    }

    setError(null)
    setUploading(true)

    const photos = findChatPhotos(files)

    try {
      const { chatId, title } = await uploadChat(pages, [...photos.keys()])
      onUploaded({ chatId, title }, photos)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Something went wrong. Try again.')
      setUploading(false)
    }
  }

  return (
    <section>
      <p>Find out who really knows your group chat.</p>
      <label className={uploading ? 'button primary busy' : 'button primary'}>
        {uploading ? 'Uploading…' : 'Choose a chat'}
        <input
          type="file"
          className="visually-hidden"
          disabled={uploading}
          onChange={handleFolder}
          {...folderPickerProps}
        />
      </label>
      {error && <p role="alert" className="error">{error}</p>}
      <p className="note">
        In your Facebook HTML export, open <code>messages/inbox</code> and choose the folder for one chat.
      </p>
      <p className="note">
        Only that chat's messages are uploaded. They're held in memory, never saved to disk, and deleted after 2 idle
        hours. Photos are shown from your own files and never leave your device.
      </p>
    </section>
  )
}
