import { photoUrl } from '../photoUrls'

type Props = {
  file: File | undefined
}

export default function ChatPhoto({ file }: Props) {
  if (!file) {
    return <span className="photo-missing">Photo not available. Choose the chat again to see photos.</span>
  }

  return <img className="photo" src={photoUrl(file)} alt="Photo sent in the chat" />
}
