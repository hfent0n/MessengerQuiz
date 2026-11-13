// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import * as fake from '../test/fakeExport'
import { demoChat } from './demoChat'
import { parseChat } from './parseChat'
import { isAutomated, sealChat, type SealedMessage } from './sealChat'
import { labels, newChatKey, open } from './sealing'

const LONG = 'A made-up message that is long enough to be a question.'

describe('sealChat', () => {
  it('sends no readable names or text', async () => {
    const chat = parseChat([
      fake.page(
        'Secret title',
        fake.message('Zebedee', `${LONG} Zebedee`, fake.DEFAULT_TIMESTAMP, '😆Quentin'),
        fake.message('Quentin', `${LONG} Quentin`),
      ),
    ])

    const upload = JSON.stringify(await sealChat(chat, await newChatKey(), new Set()))

    for (const secret of ['Secret title', 'Zebedee', 'Quentin', 'made-up', '😆']) {
      expect(upload).not.toContain(secret)
    }
  })

  it('opens again with the same key', async () => {
    const key = await newChatKey()
    const chat = parseChat([fake.page('Friends', fake.message('Ana', LONG, fake.DEFAULT_TIMESTAMP, '😆Ben'))])

    const upload = await sealChat(chat, key, new Set())

    expect(await open(key, labels.title, upload.title)).toBe('Friends')
    expect(await open(key, labels.people, upload.people)).toEqual(['Ana'])
    expect(await open<SealedMessage>(key, labels.message(0), upload.messages[0].sealed)).toEqual({
      text: LONG,
      reactions: [{ emoji: '😆', reactor: 'Ben' }],
      hasMedia: false,
      media: [],
    })
  })

  it('describes each message by number', async () => {
    const chat = parseChat([
      fake.page(
        'Friends',
        fake.message('Ben', 'https://example.com/a-link-that-is-long-enough'),
        fake.message('Ana', 'Ana named the group Friends'),
        fake.message('Ben', 'Too short'),
        fake.message('Ana', LONG, fake.DEFAULT_TIMESTAMP, '😆Ben', '❤️Cal'),
      ),
    ])

    const upload = await sealChat(chat, await newChatKey(), new Set())

    expect(upload.personCount).toBe(2)
    expect(upload.messages.map(({ sealed: _sealed, ...rest }) => rest)).toEqual([
      { sender: 0, sentAt: chat.messages[0].sentAt, reactions: 2, media: 0, quizzable: true, system: false },
      { sender: 1, sentAt: chat.messages[1].sentAt, reactions: 0, media: 0, quizzable: false, system: false },
      { sender: 0, sentAt: chat.messages[2].sentAt, reactions: 0, media: 0, quizzable: false, system: true },
      { sender: 1, sentAt: chat.messages[3].sentAt, reactions: 0, media: 0, quizzable: false, system: false },
    ])
  })

  it('marks automated accounts by number, so the server never sees their names', async () => {
    const chat = parseChat([
      fake.page('Friends', fake.message('Ana', LONG), fake.message('Meta AI', LONG), fake.message('Live Updates', LONG)),
    ])

    const upload = await sealChat(chat, await newChatKey(), new Set())

    expect(upload.automated.map((person) => chat.people[person]).sort()).toEqual(['Live Updates', 'Meta AI'])
    expect(JSON.stringify(upload)).not.toContain('Meta')
  })

  it.each([
    ['Meta AI', true],
    ['meta ai', true],
    ['MetaAI', true],
    ['Live Updates', true],
    ['Live updates', true],
    ['Meta Aiden', false],
    ['Ana', false],
  ])('recognises automated senders: %s', (name, automated) => {
    expect(isAutomated(name)).toBe(automated)
  })

  it('only counts media the player has', async () => {
    const key = await newChatKey()
    const chat = parseChat([fake.page('Friends', fake.photo('Ana', 'have.jpg'), fake.photo('Ben', 'missing.jpg'))])

    const upload = await sealChat(chat, key, new Set(['photos/have.jpg']))

    expect(upload.messages.map((message) => [message.media, message.quizzable])).toEqual([
      [0, false],
      [1, true],
    ])
    const missing = await open<SealedMessage>(key, labels.message(0), upload.messages[0].sealed)
    expect(missing).toMatchObject({ hasMedia: true, media: [] })
  })
})

describe('the demo chat', () => {
  const chat = demoChat()

  it('has four flatmates, in time order', () => {
    expect([...chat.people].sort()).toEqual(['Jess', 'Priya', 'Sam', 'Tom'])
    const times = chat.messages.map((message) => message.sentAt!)
    expect(times).toEqual([...times].sort((a, b) => a - b))
  })

  it('has plenty of questions from everyone, with no system lines', async () => {
    const upload = await sealChat(chat, await newChatKey(), new Set(['photos/lasagne.svg', 'photos/pickle.svg', 'photos/quiz-trophy.svg']))

    const quizzable = upload.messages.filter((message) => message.quizzable)
    expect(quizzable.length).toBeGreaterThanOrEqual(50)
    expect(new Set(quizzable.map((message) => message.sender)).size).toBe(4)
    expect(upload.messages.some((message) => message.system)).toBe(false)
  })

  it('has reactions only from other flatmates', () => {
    for (const message of chat.messages) {
      for (const reaction of message.reactions) {
        expect(chat.people).toContain(reaction.reactor)
        expect(reaction.reactor).not.toBe(message.sender)
      }
    }
  })

  it('keeps its photos in the photos folder', () => {
    expect(chat.messages.flatMap((message) => message.media).sort()).toEqual([
      'photos/lasagne.svg',
      'photos/pickle.svg',
      'photos/quiz-trophy.svg',
    ])
  })
})
