// A file from the player's export, either inside a zip or from an unzipped folder.
// Loading is deferred so nothing is read until it's needed.
export type ExportFile = {
  path: string
  load: () => Promise<Blob>
}

export type ChatSource = {
  // The chat's folder name, such as "friends_123". Pages are uploaded under it.
  folder: string
  pages: ExportFile[]
  // Photos, GIFs and videos, keyed by the path inside the chat folder, such as "photos/123.jpg", which is how the
  // pages refer to them. The files never leave the browser.
  media: Map<string, ExportFile>
}

const PAGE_FILE_NAME = /^message_\d+\.html$/
const MEDIA_PATH = /^(photos\/[^/]+\.(jpe?g|png|webp)|gifs\/[^/]+\.gif|videos\/[^/]+\.mp4)$/i
// Chats sit in messages/<inbox, archived_threads, ...>/<chat folder>/.
const CHAT_FILE_PATH = /(?:^|\/)messages\/([^/]+\/([^/]+))\/(.+)$/

// One export is split across several zips, and a chat's pages and media can be in different ones.
// Files with the same path are kept once.
export function groupChats(files: ExportFile[]): ChatSource[] {
  const chats = new Map<string, ChatSource & { pageNames: Set<string> }>()

  for (const file of files) {
    const match = CHAT_FILE_PATH.exec(file.path)
    if (!match) continue
    const [, key, folder, rest] = match

    let chat = chats.get(key)
    if (!chat) {
      chat = { folder, pages: [], media: new Map(), pageNames: new Set() }
      chats.set(key, chat)
    }

    if (PAGE_FILE_NAME.test(rest) && !chat.pageNames.has(rest)) {
      chat.pageNames.add(rest)
      chat.pages.push(file)
    } else if (MEDIA_PATH.test(rest) && !chat.media.has(rest)) {
      chat.media.set(rest, file)
    }
  }

  return [...chats.values()]
    .filter((chat) => chat.pages.length > 0)
    .map(({ folder, pages, media }) => ({ folder, pages, media }))
    .sort((a, b) => b.pages.length - a.pages.length || a.folder.localeCompare(b.folder))
}

// An unzipped chat folder. Pages sit directly inside it. Deeper pages mean it holds several chats, such as inbox.
export function chatFromFolder(files: File[]): ChatSource | null {
  const chat: ChatSource = { folder: '', pages: [], media: new Map() }

  for (const file of files) {
    const [folder, ...rest] = file.webkitRelativePath.split('/')
    const path = rest.join('/')
    const exportFile = { path: file.webkitRelativePath, load: () => Promise.resolve(file) }

    if (rest.length === 1 && PAGE_FILE_NAME.test(path)) {
      chat.folder = folder
      chat.pages.push(exportFile)
    } else if (MEDIA_PATH.test(path)) {
      chat.media.set(path, exportFile)
    }
  }

  return chat.pages.length > 0 ? chat : null
}

export type MediaKind = 'photo' | 'gif' | 'video'

// The folder says what a media file is, as it does on the server.
export function mediaKind(path: string): MediaKind {
  if (path.startsWith('videos/')) return 'video'
  if (path.startsWith('gifs/')) return 'gif'
  return 'photo'
}

const MIME_TYPES: Record<string, string> = {
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  png: 'image/png',
  webp: 'image/webp',
  gif: 'image/gif',
  mp4: 'video/mp4',
}

export function mimeType(path: string): string {
  return MIME_TYPES[path.slice(path.lastIndexOf('.') + 1).toLowerCase()] ?? ''
}

// Folder names end in a numeric ID, such as "friends_123".
export function chatLabel(chat: ChatSource): string {
  return chat.folder.replace(/_\d+$/, '').replaceAll('_', ' ')
}

export function fileName(file: ExportFile): string {
  return file.path.slice(file.path.lastIndexOf('/') + 1)
}
