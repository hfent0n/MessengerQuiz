import { describe, expect, it } from 'vitest'
import { parseRoute } from '../routes'
import { keyFromHash, linkWithKey } from './keys'

const KEY = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123456'

describe('game links', () => {
  it('carry the key after the #, which browsers never send to the server', () => {
    const link = linkWithKey('https://example.com/play/k7f3q9xw2m', KEY)

    expect(link).toBe(`https://example.com/play/k7f3q9xw2m#k=${KEY}`)
    expect(new URL(link).pathname).toBe('/play/k7f3q9xw2m')
    expect(keyFromHash(new URL(link).hash)).toBe(KEY)
  })

  it.each(['', '#', '#k=', '#k=short', `#k=${KEY}!`, `#other=${KEY}`])('ignore missing or damaged keys: %s', (hash) => {
    expect(keyFromHash(hash)).toBeNull()
  })

  it('have a privacy page alongside', () => {
    expect(parseRoute('/privacy')).toEqual({ kind: 'privacy' })
  })
})
