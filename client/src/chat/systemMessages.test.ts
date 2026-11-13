import { describe, expect, it } from 'vitest'
import { canBeQuestion, isSystemLine } from './systemMessages'

const LONG = 'This message is comfortably long enough to use.'

describe('canBeQuestion', () => {
  it('needs text long enough to recognise', () => {
    expect(canBeQuestion('Too short', [])).toBe(false)
    expect(canBeQuestion(LONG, [])).toBe(true)
  })

  it.each([
    "Ana Smith reacted ❤ to your message, and that's fine",
    'Ana Smith set the nickname for Ben Jones to Benny',
    'Ana Smith joined the video call with everybody else',
    'Ana Smith named the group Holiday planning crew',
    'Ana Smith set the quick reaction to a heart emoji',
    'You can now see when other people open this chat',
    'https://example.com/a/very/long/link/that/is/over/thirty/characters',
  ])('leaves out system lines and bare links: %s', (text) => {
    expect(canBeQuestion(text, [])).toBe(false)
  })

  it('keeps long messages that only mention a system phrase', () => {
    expect(canBeQuestion(`I honestly think he missed a really big chance there. ${'x'.repeat(100)}`, [])).toBe(true)
  })

  it('counts photos, GIFs and videos whatever their caption', () => {
    expect(canBeQuestion('', ['photos/a.jpg'])).toBe(true)
  })

  it('does not count media messages with nothing to show', () => {
    expect(canBeQuestion('', [])).toBe(false)
  })
})

describe('isSystemLine', () => {
  it('marks event lines, which end a run of context', () => {
    expect(isSystemLine('Ana Smith named the group Weekend')).toBe(true)
    expect(isSystemLine(LONG)).toBe(false)
  })
})
