import type { ExportFile } from './chatFiles'

// The demo chat's photos ship with the client, under public/demo. These match the paths in the server's
// Demo/demo-chat.json.
const DEMO_PHOTOS = ['photos/lasagne.svg', 'photos/quiz-trophy.svg', 'photos/pickle.svg']

export function demoMedia(): Map<string, ExportFile> {
  return new Map(
    DEMO_PHOTOS.map((path) => [
      path,
      {
        path,
        load: async () => {
          const response = await fetch(`${import.meta.env.BASE_URL}demo/${path}`)
          if (!response.ok) throw new Error(`The demo photo ${path} is missing.`)
          return response.blob()
        },
      },
    ]),
  )
}
