import { useCallback, useState } from 'react'
import { loadActiveChat, saveActiveChat, type ActiveChat } from './activeChat'
import { deleteChat } from './api'
import QuizScreen from './screens/QuizScreen'
import SelectChatScreen from './screens/SelectChatScreen'

export default function App() {
  const [activeChat, setActiveChat] = useState<ActiveChat | null>(loadActiveChat)

  const changeActiveChat = useCallback((chat: ActiveChat | null) => {
    saveActiveChat(chat)
    setActiveChat(chat)
  }, [])

  const clearActiveChat = useCallback(() => changeActiveChat(null), [changeActiveChat])

  async function wipeChat() {
    if (activeChat) await deleteChat(activeChat.chatId)
    changeActiveChat(null)
  }

  return (
    <main className="app">
      <h1>Messenger Quiz</h1>
      {activeChat ? (
        <QuizScreen
          key={activeChat.chatId}
          chat={activeChat}
          onWipe={wipeChat}
          onExpired={clearActiveChat}
        />
      ) : (
        <SelectChatScreen onUploaded={changeActiveChat} />
      )}
    </main>
  )
}
