import { useEffect, useState } from 'react'
import type { ActiveChat } from '../activeChat'
import { ApiError, nextQuestion, type Question } from '../api'

type Props = {
  chat: ActiveChat
  onWipe: () => void
  onExpired: () => void
}

type Round =
  | { status: 'loading' }
  | { status: 'failed'; message: string }
  | { status: 'finished' }
  | { status: 'asking'; question: Question }
  | { status: 'answered'; question: Question; choice: string }

export default function QuizScreen({ chat, onWipe, onExpired }: Props) {
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
          <p className="prompt">Who said this?</p>
          <blockquote className="message">{round.question.text}</blockquote>
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

function optionClass(round: Round, option: string): string {
  if (round.status !== 'answered') return 'button option'
  if (option === round.question.answer) return 'button option correct'
  return option === round.choice ? 'button option wrong' : 'button option'
}

function reactionSummary(reactions: number): string {
  if (reactions === 0) return ''
  return reactions === 1 ? 'It got 1 reaction.' : `It got ${reactions} reactions.`
}
