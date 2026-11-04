// Object URLs let <img> show the player's local files. Each file gets one URL, reused across renders.
const urls = new Map<File, string>()

export function photoUrl(file: File): string {
  let url = urls.get(file)
  if (!url) {
    url = URL.createObjectURL(file)
    urls.set(file, url)
  }
  return url
}

export function releasePhotoUrls(): void {
  urls.forEach((url) => URL.revokeObjectURL(url))
  urls.clear()
}
