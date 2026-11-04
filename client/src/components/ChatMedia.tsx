import { Suspense, use, useState } from 'react'
import { mediaKind, type ExportFile } from '../chatFiles'
import { mediaUrl } from '../mediaUrls'

type Props = {
  path: string
  file: ExportFile | undefined
}

export default function ChatMedia({ path, file }: Props) {
  if (!file) return <MissingMedia />

  return (
    <Suspense fallback={<span className="attachment-loading">Loading {MEDIA_NAMES[mediaKind(path)]}…</span>}>
      <LoadedMedia path={path} file={file} />
    </Suspense>
  )
}

const MEDIA_NAMES = { photo: 'photo', gif: 'GIF', video: 'video' } as const

function LoadedMedia({ path, file }: Props & { file: ExportFile }) {
  const url = use(mediaUrl(file))
  const [failed, setFailed] = useState(false)
  if (!url || failed) return <MissingMedia />

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

function MissingMedia() {
  return <span className="attachment-missing">Not available. Choose the chat again to see photos and videos.</span>
}
