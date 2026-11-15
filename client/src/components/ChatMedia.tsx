import { Suspense, use, useState } from 'react'
import { mediaKind, type ExportFile } from '../chatFiles'
import { mediaUrl } from '../mediaUrls'

type Props = {
  path: string
  file: ExportFile | undefined
  missingNote?: string
}

const DEFAULT_MISSING = 'Not available. Choose the chat again to see photos and videos.'

export default function ChatMedia({ path, file, missingNote = DEFAULT_MISSING }: Props) {
  if (!file) return <MissingMedia note={missingNote} />

  return (
    <Suspense fallback={<span className="attachment-loading">Loading {MEDIA_NAMES[mediaKind(path)]}…</span>}>
      <LoadedMedia path={path} file={file} missingNote={missingNote} />
    </Suspense>
  )
}

const MEDIA_NAMES = { photo: 'photo', gif: 'GIF', video: 'video' } as const

function LoadedMedia({ path, file, missingNote }: Props & { file: ExportFile; missingNote: string }) {
  const url = use(mediaUrl(file))
  const [failed, setFailed] = useState(false)
  if (!url || failed) return <MissingMedia note={missingNote} />

  const kind = mediaKind(path)
  if (kind === 'video') {
    return (
      <video
        className="attachment"
        src={url}
        controls
        playsInline
        preload="metadata"
        aria-label="Video sent in the chat"
        onError={() => setFailed(true)}
      />
    )
  }

  return (
    <img
      className="attachment"
      src={url}
      alt={kind === 'gif' ? 'GIF sent in the chat' : 'Photo sent in the chat'}
      onError={() => setFailed(true)}
    />
  )
}

function MissingMedia({ note }: { note: string }) {
  return <span className="attachment-missing">{note}</span>
}
