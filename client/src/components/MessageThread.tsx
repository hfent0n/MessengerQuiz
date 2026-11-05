import type { ContextMessage } from '../api'
import type { ExportFile } from '../chatFiles'
import ChatMedia from './ChatMedia'

type Props = {
  text: string
  media: string[]
  context: ContextMessage[]
  files: ReadonlyMap<string, ExportFile>
}

// The question's message, with the sender's earlier messages above it.
export default function MessageThread({ text, media, context, files }: Props) {
  return (
    <div className="thread">
      {context.map((message, index) => (
        <ContextBubble key={index} message={message} files={files} />
      ))}
      <blockquote className="message">
        {media.map((path) => (
          <ChatMedia key={path} path={path} file={files.get(path)} />
        ))}
        {text && <span className="message-text">{text}</span>}
      </blockquote>
    </div>
  )
}

function ContextBubble({ message, files }: { message: ContextMessage; files: ReadonlyMap<string, ExportFile> }) {
  if (message.media.length === 0 && !message.text) {
    // A sticker, voice message, or a photo or video missing from the export or left out of multiplayer.
    return <p className="message context media">Sent something that can't be shown</p>
  }

  return (
    <div className="message context">
      {message.media.map((path) => (
        <ChatMedia key={path} path={path} file={files.get(path)} />
      ))}
      {message.text && <span className="message-text">{message.text}</span>}
    </div>
  )
}
