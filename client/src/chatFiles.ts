const PAGE_FILE_NAME = /^message_\d+\.html$/
const PHOTO_FILE_NAME = /\.(jpe?g|png|webp)$/i

// Pages sit directly inside the chosen folder. Deeper pages mean it holds several chats, such as inbox.
export function findChatPages(files: File[]): File[] {
  return files.filter(
    (file) => PAGE_FILE_NAME.test(file.name) && file.webkitRelativePath.split('/').length === 2,
  )
}

// Keyed by the path inside the chat folder, such as "photos/123.jpg", which is how the server refers to them.
// The files stay in the browser. Only their names are sent.
export function findChatPhotos(files: File[]): Map<string, File> {
  const photos = new Map<string, File>()

  for (const file of files) {
    const [, folder, name, ...rest] = file.webkitRelativePath.split('/')
    if (folder === 'photos' && name && rest.length === 0 && PHOTO_FILE_NAME.test(name)) {
      photos.set(`photos/${name}`, file)
    }
  }

  return photos
}
