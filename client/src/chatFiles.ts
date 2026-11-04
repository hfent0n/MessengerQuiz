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
  // Keyed by the path inside the chat folder, such as "photos/123.jpg", which is how the server refers to them.
  // The photos stay in the browser. Only their names are sent.
  photos: Map<string, ExportFile>
}

const PAGE_FILE_NAME = /^message_\d+\.html$/
const PHOTO_PATH = /^photos\/[^/]+\.(jpe?g|png|webp)$/i
// Chats sit in messages/<inbox, archived_threads, ...>/<chat folder>/.
const CHAT_FILE_PATH = /(?:^|\/)messages\/([^/]+\/([^/]+))\/(.+)$/

// One export is split across several zips, and a chat's pages and photos can be in different ones.
// Files with the same path are kept once.
export function groupChats(files: ExportFile[]): ChatSource[] {
  const chats = new Map<string, ChatSource & { pageNames: Set<string> }>()

  for (const file of files) {
    const match = CHAT_FILE_PATH.exec(file.path)
    if (!match) continue
    const [, key, folder, rest] = match

    let chat = chats.get(key)
    if (!chat) {
      chat = { folder, pages: [], photos: new Map(), pageNames: new Set() }
      chats.set(key, chat)
    }

    if (PAGE_FILE_NAME.test(rest) && !chat.pageNames.has(rest)) {
      chat.pageNames.add(rest)
      chat.pages.push(file)
    } else if (PHOTO_PATH.test(rest) && !chat.photos.has(rest)) {
      chat.photos.set(rest, file)
    }
  }

  return [...chats.values()]
    .filter((chat) => chat.pages.length > 0)
    .map(({ folder, pages, photos }) => ({ folder, pages, photos }))
    .sort((a, b) => b.pages.length - a.pages.length || a.folder.localeCompare(b.folder))
}

// An unzipped chat folder. Pages sit directly inside it. Deeper pages mean it holds several chats, such as inbox.
export function chatFromFolder(files: File[]): ChatSource | null {
  const chat: ChatSource = { folder: '', pages: [], photos: new Map() }

  for (const file of files) {
    const [folder, ...rest] = file.webkitRelativePath.split('/')
    const path = rest.join('/')
    const exportFile = { path: file.webkitRelativePath, load: () => Promise.resolve(file) }

    if (rest.length === 1 && PAGE_FILE_NAME.test(path)) {
      chat.folder = folder
      chat.pages.push(exportFile)
    } else if (PHOTO_PATH.test(path)) {
      chat.photos.set(path, exportFile)
    }
  }

  return chat.pages.length > 0 ? chat : null
}

// Folder names end in a numeric ID, such as "friends_123".
export function chatLabel(chat: ChatSource): string {
  return chat.folder.replace(/_\d+$/, '').replaceAll('_', ' ')
}

export function fileName(file: ExportFile): string {
  return file.path.slice(file.path.lastIndexOf('/') + 1)
}
