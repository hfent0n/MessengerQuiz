// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import * as fake from '../test/fakeExport'
import { ChatParseError, parseChat, parseTimestamp } from './parseChat'

const DEC_12_2023_9_05_03PM = Date.UTC(2023, 11, 12, 21, 5, 3) / 1000

describe('parseChat', () => {
  it('reads the sender, text and time', () => {
    const chat = parseChat([fake.page('Friends', fake.message('Ana', 'Hello there'))])

    expect(chat.title).toBe('Friends')
    expect(chat.messages).toEqual([
      { sender: 'Ana', text: 'Hello there', sentAt: DEC_12_2023_9_05_03PM, reactions: [], hasMedia: false, media: [] },
    ])
  })

  it('keeps line breaks and decodes entities', () => {
    const chat = parseChat([fake.page('Friends', fake.message('Ana', "It's <fine>\n& good"))])

    expect(chat.messages[0].text).toBe("It's <fine>\n& good")
  })

  it('splits reactions, including emoji made of several code points', () => {
    const chat = parseChat([
      fake.page('Friends', fake.message('Ana', 'Hello', fake.DEFAULT_TIMESTAMP, '😆Ben Smith', '👍🏽Cal Jones')),
    ])

    expect(chat.messages[0].reactions).toEqual([
      { emoji: '😆', reactor: 'Ben Smith' },
      { emoji: '👍🏽', reactor: 'Cal Jones' },
    ])
    expect(chat.messages[0].text).toBe('Hello')
  })

  it('gives media messages no text and keeps their reactions', () => {
    const chat = parseChat([fake.page('Friends', fake.photo('Ana', '123.jpg', '😆Ben'))])

    expect(chat.messages[0]).toMatchObject({ text: '', hasMedia: true, media: ['photos/123.jpg'] })
    expect(chat.messages[0].reactions).toHaveLength(1)
  })

  it('records GIF and video paths', () => {
    const chat = parseChat([fake.page('Friends', fake.gif('Ana', '1.gif'), fake.video('Ben', '2.mp4'))])

    expect(chat.messages.map((message) => [message.sender, message.media])).toEqual([
      ['Ben', ['videos/2.mp4']],
      ['Ana', ['gifs/1.gif']],
    ])
  })

  it.each([
    `${fake.CHAT_FOLDER}/videos/thumbnail.jpg`,
    'your_facebook_activity/messages/stickers_used/s.png',
    'https://example.com/link-preview.jpg',
  ])('records no path for stickers, thumbnails and previews: %s', (source) => {
    const chat = parseChat([fake.page('Friends', fake.media('Ana', source))])

    expect(chat.messages[0]).toMatchObject({ hasMedia: true, media: [] })
  })

  it('skips header and group photo blocks', () => {
    const chat = parseChat([fake.page('Friends', fake.groupPhoto('Group photo'), fake.message('Ana', 'Hello'))])

    expect(chat.people).toEqual(['Ana'])
    expect(chat.messages).toHaveLength(1)
  })

  it('merges pages oldest first', () => {
    const newest = fake.page('Friends', fake.message('Ana', 'newest', 'Dec 12, 2023 9:05:03pm'))
    const oldest = fake.page('Friends', fake.message('Ben', 'oldest', 'Jan 2, 2023 1:00:00am'))

    expect(parseChat([newest, oldest]).messages.map((message) => message.text)).toEqual(['oldest', 'newest'])
  })

  it('keeps messages sent in the same second in sending order', () => {
    // The export lists messages newest first.
    const chat = parseChat([fake.page('Friends', fake.message('Ana', 'second'), fake.message('Ana', 'first'))])

    expect(chat.messages.map((message) => message.text)).toEqual(['first', 'second'])
  })

  it('orders people by how many messages they sent', () => {
    const chat = parseChat([
      fake.page('Friends', fake.message('Ana', 'one'), fake.message('Ben', 'two'), fake.message('Ben', 'three')),
    ])

    expect(chat.people).toEqual(['Ben', 'Ana'])
  })

  it('rejects no pages, pages that are not chats, and pages from different chats', () => {
    expect(() => parseChat([])).toThrow(ChatParseError)
    expect(() => parseChat(['<html><body>Hello</body></html>'])).toThrow(ChatParseError)
    expect(() =>
      parseChat([fake.page('One', fake.message('Ana', 'Hello')), fake.page('Two', fake.message('Ana', 'Hello'))]),
    ).toThrow(ChatParseError)
  })
})

describe('parseTimestamp', () => {
  it.each(['Dec 12, 2023 9:05:03pm', 'Dec 12, 2023 9:05:03 pm', 'Dec 12, 2023 9:05:03 PM'])(
    'reads old and new formats: %s',
    (value) => {
      expect(parseTimestamp(value)).toBe(DEC_12_2023_9_05_03PM)
    },
  )

  it('handles midnight and noon', () => {
    expect(parseTimestamp('Jan 1, 2024 12:00:00am')).toBe(Date.UTC(2024, 0, 1, 0) / 1000)
    expect(parseTimestamp('Jan 1, 2024 12:00:00pm')).toBe(Date.UTC(2024, 0, 1, 12) / 1000)
  })

  it.each(['12 décembre 2023', 'Feb 30, 2024 1:00:00pm', 'Dec 12, 2023 13:05:03pm', ''])(
    'returns null for unreadable times: %s',
    (value) => {
      expect(parseTimestamp(value)).toBeNull()
    },
  )
})
