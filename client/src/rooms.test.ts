import { describe, expect, it } from 'vitest'
import { isValidName } from './rooms'
import { parseRoute } from './routes'

describe('isValidName', () => {
  it.each(['Ana', ' Mary Ann ', 'José', 'Player 2'])('accepts %s', (name) => {
    expect(isValidName(name)).toBe(true)
  })

  it.each(['', 'Mary  Ann', '<b>', "O'Brien", 'Ana​', 'A name that is far too long'])('rejects %s', (name) => {
    expect(isValidName(name)).toBe(false)
  })
})

describe('parseRoute', () => {
  it('reads a game code', () => {
    expect(parseRoute('/play/K7f3q9xw2m')).toEqual({ kind: 'play', code: 'k7f3q9xw2m' })
  })

  it('treats anything else as home', () => {
    expect(parseRoute('/')).toEqual({ kind: 'home' })
    expect(parseRoute('/play/')).toEqual({ kind: 'home' })
    expect(parseRoute('/play/abc/extra')).toEqual({ kind: 'home' })
  })
})
