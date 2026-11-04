import type { ExportFile } from './chatFiles'

// Object URLs let <img> show the player's local files. Each file is loaded once and its URL reused across renders.
// A photo that can't be read resolves to null.
const urls = new Map<ExportFile, Promise<string | null>>()

export function photoUrl(file: ExportFile): Promise<string | null> {
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

export function releasePhotoUrls(): void {
  urls.forEach((url) => url.then((loaded) => loaded && URL.revokeObjectURL(loaded)))
  urls.clear()
}
