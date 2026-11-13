import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { Reaction, RoomView } from './rooms'

type Connection = {
  view: RoomView | null
  // Set when the server refuses this player, such as for an ended game. Not set while reconnecting.
  error: string | null
  reconnecting: boolean
  // Reactions from the last few seconds, including this player's own.
  reactions: Reaction[]
  // Sends an action. Resolves to an error to show, or null.
  send: (method: Action, ...args: (string | number)[]) => Promise<string | null>
}

export type Action = 'Start' | 'Answer' | 'Ready' | 'Chat' | 'React'

// How long a reaction stays on screen.
const REACTION_MS = 2500

// Keeps a live connection to one game, and reconnects to the same seat if the connection drops.
export function useRoom(code: string, token: string): Connection {
  const [view, setView] = useState<RoomView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [reconnecting, setReconnecting] = useState(false)
  const [reactions, setReactions] = useState<Reaction[]>([])
  const hub = useRef<HubConnection | null>(null)

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/api/rooms/hub')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    hub.current = connection

    // Updates can arrive out of order, so an older one never replaces a newer one.
    connection.on('room', (next: RoomView) => {
      setView((current) => (current && current.version > next.version ? current : next))
    })

    let nextReaction = 0
    connection.on('reaction', ({ playerId, emoji }: Omit<Reaction, 'id'>) => {
      const reaction = { id: ++nextReaction, playerId, emoji }
      setReactions((current) => [...current, reaction])
      setTimeout(() => setReactions((current) => current.filter((item) => item !== reaction)), REACTION_MS)
    })

    async function claimSeat() {
      const refused = await connection.invoke<string | null>('Connect', code, token)
      setError(refused)
    }

    connection.onreconnecting(() => setReconnecting(true))
    connection.onreconnected(() => {
      setReconnecting(false)
      void claimSeat()
    })

    let stopped = false
    connection
      .start()
      .then(() => (stopped ? undefined : claimSeat()))
      .catch(() => {
        if (!stopped) setError("Couldn't connect to the game. Check your connection and refresh.")
      })

    return () => {
      stopped = true
      hub.current = null
      void connection.stop()
    }
  }, [code, token])

  const send = useCallback(async (method: Action, ...args: (string | number)[]) => {
    const connection = hub.current
    if (!connection) return 'Not connected yet.'
    try {
      return await connection.invoke<string | null>(method, ...args)
    } catch {
      return "That didn't reach the game. Check your connection."
    }
  }, [])

  return { view, error, reconnecting, reactions, send }
}
