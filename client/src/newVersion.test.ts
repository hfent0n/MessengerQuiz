import { describe, expect, it } from 'vitest'
import { isStaleVersionError } from './newVersion'

describe('isStaleVersionError', () => {
  it.each([
    'Failed to fetch dynamically imported module: https://example.com/assets/zipExport-WHLFTfjr.js',
    'error loading dynamically imported module',
    'Importing a module script failed.',
  ])('spots a part missing after a deploy: %s', (message) => {
    expect(isStaleVersionError(new TypeError(message))).toBe(true)
  })

  it('ignores other errors', () => {
    expect(isStaleVersionError(new Error("Those zips couldn't be read."))).toBe(false)
    expect(isStaleVersionError('not an error')).toBe(false)
  })
})
