import { BlobReader, BlobWriter, ZipReader, type FileEntry } from '@zip.js/zip.js'
import { mimeType, type ExportFile } from './chatFiles'

const STORED = 0
const LOCAL_HEADER_SIGNATURE = 0x04034b50
const LOCAL_HEADER_LENGTH = 30

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
      const stored = entry.compressionMethod === STORED && !entry.encrypted
      files.push({
        path: entry.filename,
        load: stored ? () => sliceStored(zip, entry) : () => entry.getData(new BlobWriter(mimeType(entry.filename))),
      })
    }
  }

  return files
}

// The export stores photos, GIFs and videos uncompressed, so a file is a byte range of the zip.
// Slicing it keeps a video on disk, where the browser can stream it, instead of copying it into memory.
async function sliceStored(zip: File, entry: FileEntry): Promise<Blob> {
  const header = new DataView(await zip.slice(entry.offset, entry.offset + LOCAL_HEADER_LENGTH).arrayBuffer())
  if (header.getUint32(0, true) !== LOCAL_HEADER_SIGNATURE) {
    throw new Error(`${entry.filename} couldn't be found in ${zip.name}.`)
  }

  // The local header's name and extra field can differ in length from the index's copy.
  const start = entry.offset + LOCAL_HEADER_LENGTH + header.getUint16(26, true) + header.getUint16(28, true)
  return zip.slice(start, start + entry.compressedSize, mimeType(entry.filename))
}
