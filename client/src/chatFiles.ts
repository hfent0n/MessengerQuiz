const PAGE_FILE_NAME = /^message_\d+\.html$/

// Pages sit directly inside the chosen folder. Deeper pages mean it holds several chats, such as inbox.
export function findChatPages(files: File[]): File[] {
  return files.filter(
    (file) => PAGE_FILE_NAME.test(file.name) && file.webkitRelativePath.split('/').length === 2,
  )
}
