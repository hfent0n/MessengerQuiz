import { BlobWriter, TextReader, Uint8ArrayReader, ZipWriter } from '@zip.js/zip.js'
import { describe, expect, it } from 'vitest'
import { readZips } from './zipExport'

const chat = 'your_facebook_activity/messages/inbox/friends_1'

async function zip(entries: { path: string; data: string | Uint8Array; stored?: boolean }[]): Promise<File> {
  const writer = new ZipWriter(new BlobWriter('application/zip'))
  for (const { path, data, stored } of entries) {
    const reader = typeof data === 'string' ? new TextReader(data) : new Uint8ArrayReader(data)
    // An extra field makes the local header longer than the name alone, as in real exports.
    await writer.add(path, reader, { level: stored ? 0 : 6, extendedTimestamp: true })
  }
  return new File([await writer.close()], 'export.zip')
}

describe('readZips', () => {
  it('reads compressed pages and slices stored media out of the zip', async () => {
    const video = new Uint8Array(5000).map((_, index) => index % 251)
    const files = await readZips([
      await zip([
        { path: `${chat}/message_1.html`, data: '<html>page</html>'.repeat(100) },
        { path: `${chat}/videos/a.mp4`, data: video, stored: true },
      ]),
    ])

    const [page, media] = files
    expect(await (await page.load()).text()).toBe('<html>page</html>'.repeat(100))

    const blob = await media.load()
    expect(blob.type).toBe('video/mp4')
    expect(new Uint8Array(await blob.arrayBuffer())).toEqual(video)
  })

  it('leaves out files outside the messages folder', async () => {
    const files = await readZips([await zip([{ path: 'your_facebook_activity/other/a.html', data: 'x' }])])

    expect(files).toEqual([])
  })

  it('rejects a file that is not a zip', async () => {
    await expect(readZips([new File(['not a zip'], 'notes.txt')])).rejects.toThrow("notes.txt isn't a zip file")
  })
})
