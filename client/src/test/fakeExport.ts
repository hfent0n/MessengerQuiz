// Hand-written copies of the export's markup. Real messages must never be used as fixtures.

export const DEFAULT_TIMESTAMP = 'Dec 12, 2023 9:05:03pm'
export const CHAT_FOLDER = 'your_facebook_activity/messages/inbox/chat_123'

const escape = (text: string) =>
  text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;')

export function page(title: string, ...blocks: string[]): string {
  return `<html><head><meta charset="utf-8" /><title>${escape(title)}</title></head>
<body class="_5vb_ _2yq _a7o5"><div class="_a706" role="main">
<div class="_3-95 _a6-g"><div class="_2ph_ _a6-h">Participants: Ana, Ben and Cal</div></div>
<div class="_3-95 _a6-g"><div class="_2ph_ _a6-h">Group invitation link: not available</div></div>
${blocks.join('')}
</div></body></html>`
}

export function message(sender: string, text: string, timestamp = DEFAULT_TIMESTAMP, ...reactions: string[]): string {
  const encoded = escape(text).replaceAll('\n', '<br />')
  return block(sender, `<div></div><div>${encoded}</div><div></div><div></div>${reactionList(reactions)}`, timestamp)
}

// Any image the export can reference, such as a photo, GIF or sticker.
export function media(sender: string, source: string, timestamp = DEFAULT_TIMESTAMP, ...reactions: string[]): string {
  const image = `<div><div><a target="_blank" href="${source}"><img src="${source}" class="_a6_o _3-96" /></a><div></div></div></div>`
  return block(sender, `<div></div><div></div><div></div><div></div>${image}${reactionList(reactions)}`, timestamp)
}

export const photo = (sender: string, fileName = 'p.jpg', ...reactions: string[]) =>
  media(sender, `${CHAT_FOLDER}/photos/${fileName}`, DEFAULT_TIMESTAMP, ...reactions)

export const gif = (sender: string, fileName = 'g.gif') => media(sender, `${CHAT_FOLDER}/gifs/${fileName}`)

export function video(sender: string, fileName = 'v.mp4'): string {
  const source = `${CHAT_FOLDER}/videos/${fileName}`
  const player = `<div><div><video src="${source}" controls="1" class="_a6_o _3-96"><a href="${source}">Click for video</a></video><div></div></div></div>`
  return block(sender, `<div></div><div></div><div></div><div></div>${player}`, DEFAULT_TIMESTAMP)
}

// The export's group photo entries have an image and no timestamp.
export function groupPhoto(sender: string): string {
  return `<div class="_3-95 _a6-g"><div class="_2ph_ _a6-h _a6-i">${sender}</div>
<div class="_2ph_ _a6-p"><div><div></div><a href="g.jpg"><img src="g.jpg" class="_a6_o _3-96" /></a></div></div></div>`
}

// Senders take turns, as in a real chat, so no message has same-sender context.
export function quizzablePage(title = 'Test chat', messagesPerSender = 6): string {
  const blocks = []
  for (let index = 0; index < messagesPerSender; index++) {
    for (const sender of ['Ana', 'Ben', 'Cal']) {
      blocks.push(message(sender, `${sender} wrote a long enough message, number ${index}.`, DEFAULT_TIMESTAMP, '😆Ana'))
    }
  }
  return page(title, ...blocks)
}

function block(sender: string, content: string, timestamp: string): string {
  return `<div class="_3-95 _a6-g"><div class="_2ph_ _a6-h _a6-i">${escape(sender)}</div>
<div class="_2ph_ _a6-p"><div>${content}</div></div>
<div class="_3-94 _a6-o"><div class="_a72d">${timestamp}</div></div></div>`
}

function reactionList(reactions: string[]): string {
  if (reactions.length === 0) return ''
  return `<div><ul class="_a6-q">${reactions.map((reaction) => `<li>${escape(reaction)}</li>`).join('')}</ul></div>`
}
