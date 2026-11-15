import type { ContextMessage } from '../api'
import type { ExportFile } from '../chatFiles'
import ChatMedia from './ChatMedia'

type Props = {
  text: string
  media: string[]
  context: ContextMessage[]
  files: ReadonlyMap<string, ExportFile>
  // Shown in place of media that can't be loaded.
  missingNote?: string
}

// The question's message, with the sender's earlier messages above it.
export default function MessageThread({ text, media, context, files, missingNote }: Props) {
  return (
    <div className="thread">
      {context.map((message, index) => (
        <ContextBubble key={index} message={message} files={files} missingNote={missingNote} />
      ))}
      <blockquote className="message">
        {media.map((path) => (
          <ChatMedia key={path} path={path} file={files.get(path)} missingNote={missingNote} />
        ))}
        {text && <span className="message-text">{text}</span>}
      </blockquote>
    </div>
  )
}

function ContextBubble({ message, files, missingNote }: { message: ContextMessage; files: ReadonlyMap<string, ExportFile>; missingNote?: string }) {
  if (message.media.length === 0 && !message.text) {
    // A sticker, voice message, or a photo or video missing from the export or left out of multiplayer.
    return <p className="message context media">Sent something that can't be shown</p>
  }

  return (
    <div className="message context">
      {message.media.map((path) => (
        <ChatMedia key={path} path={path} file={files.get(path)} missingNote={missingNote} />
      ))}
      {message.text && <span className="message-text">{message.text}</span>}
    </div>
  )
}
