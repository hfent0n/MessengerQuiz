import { Suspense, use } from 'react'
import type { ExportFile } from '../chatFiles'
import { photoUrl } from '../photoUrls'

type Props = {
  file: ExportFile | undefined
}

export default function ChatPhoto({ file }: Props) {
  if (!file) return <MissingPhoto />

  return (
    <Suspense fallback={<span className="photo-loading">Loading photo…</span>}>
      <LoadedPhoto file={file} />
    </Suspense>
  )
}

function LoadedPhoto({ file }: { file: ExportFile }) {
  const url = use(photoUrl(file))
  if (!url) return <MissingPhoto />

  return <img className="photo" src={url} alt="Photo sent in the chat" />
}

function MissingPhoto() {
  return <span className="photo-missing">Photo not available. Choose the chat again to see photos.</span>
}
