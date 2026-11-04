import { describe, expect, it } from 'vitest'
import { chatLabel, groupChats, mediaKind, type ExportFile } from './chatFiles'

function file(path: string): ExportFile {
  return { path, load: () => Promise.resolve(new Blob([path])) }
}

const inbox = 'your_facebook_activity/messages/inbox'

describe('groupChats', () => {
  it('combines one chat spread across several zips', () => {
    const zipOne = [file(`${inbox}/friends_1/message_1.html`), file(`${inbox}/friends_1/photos/a.jpg`)]
    const zipTwo = [file(`${inbox}/friends_1/photos/b.png`), file(`${inbox}/friends_1/videos/c.mp4`)]

    const [chat] = groupChats([...zipOne, ...zipTwo])

    expect(chat.folder).toBe('friends_1')
    expect(chat.pages.map((page) => page.path)).toEqual([`${inbox}/friends_1/message_1.html`])
    expect([...chat.media.keys()]).toEqual(['photos/a.jpg', 'photos/b.png', 'videos/c.mp4'])
  })

  it('keeps a file found in more than one zip once', () => {
    const page = `${inbox}/friends_1/message_1.html`
    const photo = `${inbox}/friends_1/photos/a.jpg`

    const [chat] = groupChats([file(page), file(photo), file(page), file(photo)])

    expect(chat.pages).toHaveLength(1)
    expect(chat.media.size).toBe(1)
  })

  it('keeps chats apart and leaves out folders without pages', () => {
    const chats = groupChats([
      file(`${inbox}/friends_1/message_1.html`),
      file(`${inbox}/family_2/message_1.html`),
      file(`${inbox}/family_2/message_2.html`),
      file(`${inbox}/photos_only_3/photos/a.jpg`),
      file('your_facebook_activity/messages/stickers_used/x.png'),
    ])

    expect(chats.map((chat) => chat.folder)).toEqual(['family_2', 'friends_1'])
  })

  it('keeps same-named chats in different sections apart', () => {
    const chats = groupChats([
      file(`${inbox}/friends_1/message_1.html`),
      file('your_facebook_activity/messages/archived_threads/friends_1/message_1.html'),
    ])

    expect(chats).toHaveLength(2)
  })

  it('collects photos, GIFs and videos', () => {
    const [chat] = groupChats([
      file(`${inbox}/friends_1/message_1.html`),
      file(`${inbox}/friends_1/photos/a.jpg`),
      file(`${inbox}/friends_1/gifs/b.gif`),
      file(`${inbox}/friends_1/videos/c.mp4`),
    ])

    expect([...chat.media.keys()].map(mediaKind)).toEqual(['photo', 'gif', 'video'])
  })

  it('ignores voice messages, other files and nested folders', () => {
    const [chat] = groupChats([
      file(`${inbox}/friends_1/message_1.html`),
      file(`${inbox}/friends_1/audio/a.mp4`),
      file(`${inbox}/friends_1/files/b.pdf`),
      file(`${inbox}/friends_1/videos/thumbnail.jpg`),
      file(`${inbox}/friends_1/photos/nested/a.jpg`),
    ])

    expect(chat.media.size).toBe(0)
  })
})

describe('chatLabel', () => {
  it('drops the numeric ID from the folder name', () => {
    expect(chatLabel({ folder: 'big_men_123', pages: [], media: new Map() })).toBe('big men')
  })
})
