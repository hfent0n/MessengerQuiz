import { navigate } from '../routes'

const REPO_URL = 'https://github.com/hfent0n/MessengerQuiz'

// What the server receives and keeps. Kept in step with the README's privacy section.
export default function PrivacyScreen() {
  return (
    <section className="privacy">
      <h2>What the server sees</h2>
      <p>
        Messenger Quiz reads your chat <strong>in your browser</strong> and encrypts it there before anything is sent.
        The key is made on your device and never sent to the server, so the server stores your chat without being able
        to read it.
      </p>

      <h3>The server can't read</h3>
      <ul>
        <li>Message text</li>
        <li>People's names, including who reacted to what</li>
        <li>The chat's name</li>
        <li>Which emoji people reacted with</li>
        <li>Photo, GIF and video file names</li>
      </ul>

      <h3>The server can see</h3>
      <ul>
        <li>How many people are in the chat, numbered rather than named</li>
        <li>Which of those numbers are Messenger's automated accounts, Meta AI and Live Updates</li>
        <li>When each message was sent, and which numbered person sent it</li>
        <li>How many reactions and photos each message has, and roughly how long it is</li>
        <li>Which messages can be questions, so it can choose them</li>
        <li>Your IP address, and the names players choose in multiplayer</li>
      </ul>

      <h3>Never leaves your device</h3>
      <ul>
        <li>Your Facebook download. Only the chat you choose is read.</li>
        <li>Photos, GIFs and videos. They're shown straight from your files.</li>
        <li>The key, unless you share a multiplayer link</li>
      </ul>

      <h3>How long it's kept</h3>
      <p>
        Chats are held in memory only, never written to disk or a database, and never logged. A chat is deleted after
        2 hours without use, when you press <strong>Wipe my chat and exit</strong>, or when the server restarts. The
        key stays in this browser tab and is gone when you close it.
      </p>

      <h3>Multiplayer</h3>
      <p>
        A game link ends with the key, after a <code>#</code>. Browsers never send that part to the server, but anyone
        with the link can read the game's questions, so only share it with people in the chat. Players' chat messages
        are encrypted with the same key, so the server can't read those either. It does see which quick reactions
        players send.
      </p>

      <h3>What this doesn't protect against</h3>
      <p>
        The page you're using comes from the same server. Someone running a changed server could send a page that
        leaks your key, and encryption can't prevent that. The code is{' '}
        <a href={REPO_URL}>open source</a>, and your browser's network tab shows exactly what's sent. To be certain,
        run Messenger Quiz on your own computer, where nothing leaves it.
      </p>

      <button className="button" onClick={() => navigate('/')}>Back</button>
    </section>
  )
}
