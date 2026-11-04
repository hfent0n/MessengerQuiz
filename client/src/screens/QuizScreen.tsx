import { useEffect, useState } from 'react'
import type { ActiveChat } from '../activeChat'
import { ApiError, nextQuestion, type ContextMessage, type Question } from '../api'
import type { ExportFile } from '../chatFiles'
import ChatPhoto from '../components/ChatPhoto'

type Props = {
  chat: ActiveChat
  photos: ReadonlyMap<string, ExportFile>
  onWipe: () => void
  onExpired: () => void
}

type Round =
  | { status: 'loading' }
  | { status: 'failed'; message: string }
  | { status: 'finished' }
  | { status: 'asking'; question: Question }
  | { status: 'answered'; question: Question; choice: string }

export default function QuizScreen({ chat, photos, onWipe, onExpired }: Props) {
  const [round, setRound] = useState<Round>({ status: 'loading' })
  const [roundNumber, setRoundNumber] = useState(0)
  const [score, setScore] = useState({ correct: 0, answered: 0 })

  useEffect(() => {
    let cancelled = false

    nextQuestion(chat.chatId)
      .then((question) => {
        if (cancelled) return
        setRound(question ? { status: 'asking', question } : { status: 'finished' })
      })
      .catch((caught: unknown) => {
        if (cancelled) return
        if (caught instanceof ApiError && caught.status === 401) {
          onExpired()
        } else {
          setRound({ status: 'failed', message: 'Could not load a question.' })
        }
      })

    return () => {
      cancelled = true
    }
  }, [chat.chatId, roundNumber, onExpired])

  function answer(question: Question, choice: string) {
    setRound({ status: 'answered', question, choice })
    setScore(({ correct, answered }) => ({
      correct: correct + (choice === question.answer ? 1 : 0),
      answered: answered + 1,
    }))
  }

  function nextRound() {
    setRound({ status: 'loading' })
    setRoundNumber((number) => number + 1)
  }

  return (
    <section>
      <header className="quiz-header">
        <h2>{chat.title}</h2>
        <p className="score" aria-label={`Score: ${score.correct} of ${score.answered}`}>
          {score.correct} / {score.answered}
        </p>
      </header>

      {round.status === 'loading' && <p className="note">Loading…</p>}

      {round.status === 'failed' && (
        <>
          <p role="alert" className="error">{round.message}</p>
          <button className="button primary" onClick={nextRound}>Try again</button>
        </>
      )}

      {round.status === 'finished' && <p>That's every message! Final score: {score.correct} / {score.answered}.</p>}

      {(round.status === 'asking' || round.status === 'answered') && (
        <>
          <p className="prompt">{prompt(round.question)}</p>
          <div className="thread">
            {round.question.context.map((message, index) => (
              <ContextBubble key={index} message={message} photos={photos} />
            ))}
            <blockquote className="message">
              {round.question.photos.map((path) => (
                <ChatPhoto key={path} file={photos.get(path)} />
              ))}
              {round.question.text && <span className="message-text">{round.question.text}</span>}
            </blockquote>
          </div>
          <div className="options">
            {round.question.options.map((option) => (
              <button
                key={option}
                className={optionClass(round, option)}
                disabled={round.status === 'answered'}
                onClick={() => answer(round.question, option)}
              >
                {option}
              </button>
            ))}
          </div>
        </>
      )}

      {round.status === 'answered' && (
        <div className="result">
          <p role="status">
            {round.choice === round.question.answer ? 'Correct!' : `It was ${round.question.answer}.`}{' '}
            {reactionSummary(round.question.reactions)}
          </p>
          <button className="button primary" autoFocus onClick={nextRound}>Next question</button>
        </div>
      )}

      <button className="link" onClick={onWipe}>Wipe my chat and exit</button>
    </section>
  )
}

function ContextBubble({ message, photos }: { message: ContextMessage; photos: ReadonlyMap<string, ExportFile> }) {
  if (message.photos.length === 0 && !message.text) {
    return <p className="message context media">Sent a photo or video</p>
  }

  return (
    <div className="message context">
      {message.photos.map((path) => (
        <ChatPhoto key={path} file={photos.get(path)} />
      ))}
      {message.text && <span className="message-text">{message.text}</span>}
    </div>
  )
}

function prompt(question: Question): string {
  if (question.context.length > 0) return 'Who sent these messages?'
  return question.photos.length > 0 && !question.text ? 'Who sent this photo?' : 'Who said this?'
}

function optionClass(round: Round, option: string): string {
  if (round.status !== 'answered') return 'button option'
  if (option === round.question.answer) return 'button option correct'
  return option === round.choice ? 'button option wrong' : 'button option'
}

function reactionSummary(reactions: number): string {
  if (reactions === 0) return ''
  return reactions === 1 ? 'It got 1 reaction.' : `It got ${reactions} reactions.`
}
