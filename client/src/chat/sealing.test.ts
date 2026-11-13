import { describe, expect, it } from 'vitest'
import { exportKey, fromBase64Url, importKey, labels, newChatKey, open, seal, SealError, toBase64Url } from './sealing'

describe('sealing', () => {
  it('opens what it sealed', async () => {
    const key = await newChatKey()
    const value = { text: 'Made-up message ✨', media: ['photos/1.jpg'] }

    const sealed = await seal(key, labels.message(3), value)

    expect(await open(key, labels.message(3), sealed)).toEqual(value)
  })

  it('never contains the text it sealed', async () => {
    const key = await newChatKey()

    const sealed = await seal(key, labels.title, 'Flatmates')

    expect(sealed).not.toContain('Flatmates')
    expect(new TextDecoder().decode(fromBase64Url(sealed)!)).not.toContain('Flatmates')
  })

  it('seals the same value differently each time', async () => {
    const key = await newChatKey()

    expect(await seal(key, labels.title, 'Same')).not.toBe(await seal(key, labels.title, 'Same'))
  })

  it('refuses to open a box under another label, so text cannot be moved to another message', async () => {
    const key = await newChatKey()
    const sealed = await seal(key, labels.message(1), 'First')

    await expect(open(key, labels.message(2), sealed)).rejects.toThrow(SealError)
  })

  it("refuses to open a chat message as someone else's", async () => {
    const key = await newChatKey()
    const sealed = await seal(key, labels.chatLine('player-a'), 'hello')

    expect(await open(key, labels.chatLine('player-a'), sealed)).toBe('hello')
    await expect(open(key, labels.chatLine('player-b'), sealed)).rejects.toThrow(SealError)
  })

  it('refuses to open a changed box', async () => {
    const key = await newChatKey()
    const box = fromBase64Url(await seal(key, labels.title, 'Title'))!
    box[box.length - 1] ^= 1

    await expect(open(key, labels.title, toBase64Url(box))).rejects.toThrow(SealError)
  })

  it('refuses to open with another key', async () => {
    const sealed = await seal(await newChatKey(), labels.title, 'Title')

    await expect(open(await newChatKey(), labels.title, sealed)).rejects.toThrow(SealError)
  })

  it('refuses damaged boxes', async () => {
    const key = await newChatKey()

    await expect(open(key, labels.title, 'not base64!')).rejects.toThrow(SealError)
    await expect(open(key, labels.title, 'AAAA')).rejects.toThrow(SealError)
  })

  it('shares keys as text, as in a game link', async () => {
    const key = await newChatKey()
    const sealed = await seal(key, labels.people, ['Ana', 'Ben'])

    const text = await exportKey(key)
    const copy = await importKey(text)

    expect(text).toMatch(/^[A-Za-z0-9_-]{43}$/)
    expect(await open(copy, labels.people, sealed)).toEqual(['Ana', 'Ben'])
  })

  it('rejects keys of the wrong length', async () => {
    await expect(importKey('abc')).rejects.toThrow(SealError)
  })
})
