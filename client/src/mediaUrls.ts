import type { ExportFile } from './chatFiles'

// Object URLs let <img> and <video> show the player's local files. Each file is loaded once and its URL reused
// across renders. A file that can't be read resolves to null.
const urls = new Map<ExportFile, Promise<string | null>>()

export function mediaUrl(file: ExportFile): Promise<string | null> {
  let url = urls.get(file)
  if (!url) {
    url = file.load().then(
      (blob) => URL.createObjectURL(blob),
      () => null,
    )
    urls.set(file, url)
  }
  return url
}

export function releaseMediaUrls(): void {
  urls.forEach((url) => url.then((loaded) => loaded && URL.revokeObjectURL(loaded)))
  urls.clear()
}
