import { useCallback, useState } from 'react'
import { loadActiveChat, saveActiveChat, type ActiveChat } from './activeChat'
import { deleteChat } from './api'
import { releasePhotoUrls } from './photoUrls'
import QuizScreen from './screens/QuizScreen'
import SelectChatScreen from './screens/SelectChatScreen'

export default function App() {
  const [activeChat, setActiveChat] = useState<ActiveChat | null>(loadActiveChat)
  // Files can't be stored, so photos are lost on refresh until the chat is chosen again.
  const [photos, setPhotos] = useState<ReadonlyMap<string, File>>(new Map())

  const changeActiveChat = useCallback((chat: ActiveChat | null) => {
    saveActiveChat(chat)
    setActiveChat(chat)
  }, [])

  const clearActiveChat = useCallback(() => changeActiveChat(null), [changeActiveChat])

  function handleUploaded(chat: ActiveChat, chatPhotos: ReadonlyMap<string, File>) {
    releasePhotoUrls()
    setPhotos(chatPhotos)
    changeActiveChat(chat)
  }

  async function wipeChat() {
    if (activeChat) await deleteChat(activeChat.chatId)
    releasePhotoUrls()
    setPhotos(new Map())
    changeActiveChat(null)
  }

  return (
    <main className="app">
      <h1>Messenger Quiz</h1>
      {activeChat ? (
        <QuizScreen
          key={activeChat.chatId}
          chat={activeChat}
          photos={photos}
          onWipe={wipeChat}
          onExpired={clearActiveChat}
        />
      ) : (
        <SelectChatScreen onUploaded={handleUploaded} />
      )}
    </main>
  )
}
