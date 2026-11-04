import { useCallback, useState } from 'react'
import { loadActiveChat, saveActiveChat, type ActiveChat } from './activeChat'
import { deleteChat } from './api'
import type { ExportFile } from './chatFiles'
import { releaseMediaUrls } from './mediaUrls'
import QuizScreen from './screens/QuizScreen'
import SelectChatScreen from './screens/SelectChatScreen'

export default function App() {
  const [activeChat, setActiveChat] = useState<ActiveChat | null>(loadActiveChat)
  // Files can't be stored, so media is lost on refresh until the chat is chosen again.
  const [media, setMedia] = useState<ReadonlyMap<string, ExportFile>>(new Map())

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
  }

  return (
    <main className="app">
      <h1>Messenger Quiz</h1>
      {activeChat ? (
        <QuizScreen
          key={activeChat.chatId}
          chat={activeChat}
          media={media}
          onWipe={wipeChat}
          onExpired={clearActiveChat}
        />
      ) : (
        <SelectChatScreen onUploaded={handleUploaded} />
      )}
    </main>
  )
}
