import { useCallback, useState } from 'react'
import { loadActiveChat, saveActiveChat, type ActiveChat } from './activeChat'
import { deleteChat } from './api'
import type { ExportFile } from './chatFiles'
import { demoMedia } from './demoChat'
import { releaseMediaUrls } from './mediaUrls'
import { loadRoomToken, saveRoomToken } from './rooms'
import { navigate, useRoute } from './routes'
import GameScreen from './screens/GameScreen'
import HostSetupScreen from './screens/HostSetupScreen'
import JoinScreen from './screens/JoinScreen'
import ModeScreen from './screens/ModeScreen'
import QuizScreen from './screens/QuizScreen'
import SelectChatScreen from './screens/SelectChatScreen'

export default function App() {
  const route = useRoute()
  const [activeChat, setActiveChat] = useState<ActiveChat | null>(loadActiveChat)
  // Files can't be stored, so a player's media is lost on refresh until the chat is chosen again.
  const [media, setMedia] = useState<ReadonlyMap<string, ExportFile>>(() =>
    activeChat?.demo ? demoMedia() : new Map(),
  )

  const changeActiveChat = useCallback((chat: ActiveChat | null) => {
    saveActiveChat(chat)
    setActiveChat(chat)
  }, [])

  const clearActiveChat = useCallback(() => changeActiveChat(null), [changeActiveChat])

  function handleUploaded(chat: ActiveChat, chatMedia: ReadonlyMap<string, ExportFile>) {
    releaseMediaUrls()
    setMedia(chatMedia)
    changeActiveChat(chat)
  }

  async function wipeChat() {
    if (activeChat) await deleteChat(activeChat.chatId)
    releaseMediaUrls()
    setMedia(new Map())
    changeActiveChat(null)
    if (route.kind === 'play') {
      saveRoomToken(route.code, null)
      navigate('/')
    }
  }

  function leaveGame(code: string) {
    saveRoomToken(code, null)
    if (activeChat) changeActiveChat({ ...activeChat, mode: undefined })
    navigate('/')
  }

  return (
    <main className="app">
      <h1>Messenger Quiz</h1>
      {route.kind === 'play' ? (
        <PlayRoute key={route.code} code={route.code} onWipe={wipeChat} onLeave={() => leaveGame(route.code)} />
      ) : !activeChat ? (
        <SelectChatScreen onUploaded={handleUploaded} />
      ) : activeChat.mode === 'single' ? (
        <QuizScreen
          key={activeChat.chatId}
          chat={activeChat}
          media={media}
          onWipe={wipeChat}
          onExpired={clearActiveChat}
        />
      ) : activeChat.mode === 'multi' ? (
        <HostSetupScreen chat={activeChat} onBack={() => changeActiveChat({ ...activeChat, mode: undefined })} />
      ) : (
        <ModeScreen chat={activeChat} onChoose={(mode) => changeActiveChat({ ...activeChat, mode })} onWipe={wipeChat} />
      )}
    </main>
  )
}

// A game link. Players who haven't joined from this tab choose a name first.
function PlayRoute({ code, onWipe, onLeave }: { code: string; onWipe: () => void; onLeave: () => void }) {
  const [token, setToken] = useState(() => loadRoomToken(code))

  return token ? (
    <GameScreen code={code} token={token} onWipe={onWipe} onLeave={onLeave} />
  ) : (
    <JoinScreen code={code} onJoined={setToken} />
  )
}
