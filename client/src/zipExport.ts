import { BlobReader, BlobWriter, ZipReader } from '@zip.js/zip.js'
import type { ExportFile } from './chatFiles'

// Only the zip's index is read here. Each file is read from the zip when it's loaded, so large zips stay on disk.
export async function readZips(zips: File[]): Promise<ExportFile[]> {
  const files: ExportFile[] = []

  for (const zip of zips) {
    let entries
    try {
      entries = await new ZipReader(new BlobReader(zip)).getEntries()
    } catch {
      throw new Error(`${zip.name} isn't a zip file from your Facebook export.`)
    }

    for (const entry of entries) {
      if (entry.directory || !entry.filename.includes('messages/')) continue
      files.push({ path: entry.filename, load: () => entry.getData(new BlobWriter()) })
    }
  }

  return files
}
