import { mimeType, type ExportFile } from '../chatFiles'
import type { SealedMessage } from './sealChat'
import { labels, open, openBytes, SEAL_OVERHEAD_BYTES, sealBytes } from './sealing'

// Multiplayer media. The host's browser seals each photo, GIF or video with the game link's key and sends it through
// the server, which can't see it. Other players fetch and open it. Files over the server's limit are skipped.

export const MAX_RELAYED_FILE_BYTES = 8 * 1024 * 1024

export type MediaRequest = { position: number; sealed: string; count: number }

const TOKEN_HEADER = 'X-Player-Token'
const mediaUrl = (code: string, position: number, index: number) =>
  `/api/rooms/${encodeURIComponent(code)}/media/${position}/${index}`

// Sends one message's files. A file the host doesn't have, or that's too large, is sent empty, so players stop
// waiting for it and see a placeholder.
export async function sendMedia(
  code: string,
  token: string,
  key: CryptoKey,
  request: MediaRequest,
  files: ReadonlyMap<string, ExportFile>,
): Promise<void> {
  const { media } = await open<SealedMessage>(key, labels.message(request.position), request.sealed)

  for (let index = 0; index < request.count; index++) {
    const file = files.get(media[index])
    let body: Uint8Array<ArrayBuffer> = new Uint8Array()
    if (file) {
      const blob = await file.load().catch(() => null)
      if (blob && blob.size + SEAL_OVERHEAD_BYTES <= MAX_RELAYED_FILE_BYTES) {
        body = await sealBytes(key, labels.media(request.position, index), new Uint8Array(await blob.arrayBuffer()))
      }
    }

    await fetch(mediaUrl(code, request.position, index), {
      method: 'PUT',
      headers: { [TOKEN_HEADER]: token, 'Content-Type': 'application/octet-stream' },
      body,
    })
  }
}

const RETRY_MS = 700
const GIVE_UP_MS = 30_000

// Files for one message, shown like the player's own. Each waits for the host's browser to send it, then opens it.
export function relayedFiles(
  code: string,
  token: string,
  key: CryptoKey,
  position: number,
  paths: string[],
): Map<string, ExportFile> {
  return new Map(
    paths.map((path, index) => [
      path,
      {
        path,
        load: async () => {
          const bytes = await fetchWhenReady(mediaUrl(code, position, index), token)
          const file = await openBytes(key, labels.media(position, index), bytes)
          return new Blob([file], { type: mimeType(path) })
        },
      },
    ]),
  )
}

async function fetchWhenReady(url: string, token: string): Promise<Uint8Array<ArrayBuffer>> {
  const giveUpAt = Date.now() + GIVE_UP_MS
  while (Date.now() < giveUpAt) {
    const response = await fetch(url, { headers: { [TOKEN_HEADER]: token } })
    if (response.ok) return new Uint8Array(await response.arrayBuffer())
    // 404 means the host hasn't sent it yet. Anything else means it won't come.
    if (response.status !== 404) break
    await new Promise((resolve) => setTimeout(resolve, RETRY_MS))
  }
  throw new Error("The host's file isn't available.")
}
