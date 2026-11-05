# Messenger Quiz plan

A web game where a player uploads one Facebook Messenger chat and is quizzed on its contents.

- **Backend:** .NET 10 Minimal API
- **Frontend:** React, TypeScript and Vite
- **Storage:** none. Chats stay in memory and are never written to disk.

"Who said this?" from a single uploaded chat is built, in single player and [multiplayer](#multiplayer). Everything else is under [Future work](#future-work).

## Running it

Start the API on port 5256:

```bash
dotnet run --project server/MessengerQuiz.Api --launch-profile http
```

Start the client on port 5173:

```bash
npm run dev --prefix client
```

Run the backend tests:

```bash
dotnet test MessengerQuiz.sln
```

Run the client tests:

```bash
npm test --prefix client
```

The `api-alt` and `client-alt` launch configurations run a second copy on ports 5257 and 5175. The client's `API_URL` variable points its `/api` proxy at a different API.

Limits are in `QuizOptions` and can be overridden through configuration, for example `Quiz__UploadsPerMinutePerIp=20`.

## What the export looks like

Measured against a real export of 64 chats and 102,779 messages.

| Finding | Design consequence |
|---|---|
| 1.27 GB total, but only 35 MB of chat HTML across 70 files | The browser sends only `message_*.html`, never media |
| The largest chat is under 15 MB of HTML | A single-chat upload fits comfortably in one request |
| 10,000 messages per file, newest first, up to 4 files per chat | All of a chat's pages are merged, then sorted by time |
| Median chat has 60 messages and 2 senders; only 10 chats have 500+ messages and 3+ senders | The server rejects chats too small to quiz on |
| About 80% of messages have no reactions, so many messages tie at any cut-off | Ties at the top 5% cut-off are broken randomly |
| 44% of top messages follow one or more messages from the same sender, and about 7% follow 3 or more | Questions show up to 3 earlier messages as context |
| About 1% of consecutive messages share a timestamp | Same-second messages keep their sending order |
| System lines look like normal messages ("X reacted ❤ to…", "joined the call"), roughly 680 found | They must be filtered out, or they give away the answer |
| 21 blocks have no timestamp, and all are group photo entries with only an image | The parser skips them because they aren't messages |
| Timestamps are in the export's language | `SentAt` is nullable, so another format doesn't reject the chat |
| About 10% of messages are photos, and 40% of photo messages got reactions | Photos can be questions too |
| Only about 38% of referenced photo files are in the download | Photo questions only use files the player's folder contains |
| One chat folder held 128 MB of photos, 432 MB of GIFs and 1.1 GB of video | Media can't be uploaded as part of a chat |
| The Nov 2026 export came as three zips of about 2.3 GB each. One chat's pages were in one zip, and its photos were split across all three (1,196, 1,468 and 542) | The browser reads every zip and merges each chat's files |
| Photos, GIFs and videos in the zips are stored uncompressed, and pages are deflated | Media is a byte range of the zip, so it's sliced out without copying, and videos stream from disk |
| In one chat, GIFs and videos were about 28% of media messages. Videos were up to 24 MB each and 1.1 GB in total | GIFs and videos are questions and context like photos |
| Videos are `<video src="…/videos/*.mp4">`, and images inside `videos/` are thumbnails | Only video sources count as videos |
| One chat's 11.8 MB of HTML gzips to 1.3 MB | Pages are gzipped before upload |
| Message text is always in the second inner `div` | Reading only that `div` excludes media captions and link previews |
| The participants header only appears on group chats | Participants come from message senders instead |
| Timestamps have no time zone | Treat them as local time |
| This export covers Dec 2022 to Dec 2023 only | Time-based questions are limited to the exported range |

### Reaction cut-off

Questions come from the top 5% of quizzable messages by reaction count. Quizzable means 30+ characters and not a system line or bare link.

| Chat size | Quizzable | Top 5% | Reactions at the cut-off | Messages with at least that many |
|---|---|---|---|---|
| 35,683 | 14,968 | 749 | 3 | 798 |
| 18,759 | 9,713 | 486 | 2 | 924 |
| 12,110 | 4,999 | 250 | 2 | 263 |
| 8,186 | 3,824 | 192 | 1 | 658 |

In 50 of 59 chats with quizzable messages, the top 5% is fewer than 50 messages, often 3 to 10. Small chats therefore use their top 50 messages instead.

### Earlier filter counts

Message counts passing the original filters, out of about 99,000 text messages (system lines excluded by an approximate, English-only pattern):

| Minimum length | Any reactions | 1+ reactions | 2+ reactions |
|---|---|---|---|
| 15 characters | 91,364 | 15,604 | 4,993 |
| 30 characters | 66,425 | 10,598 | 3,518 |
| 50 characters | 32,134 | 5,654 | 2,007 |

## Flow

1. The player chooses every zip from their export at once. The browser reads only each zip's file index and lists the chats found under `messages/<section>/<chat>/`, merging each chat's files across zips.
2. The player picks a chat. The browser gzips that chat's `message_*.html` files and posts them with the names of its photos, GIFs and videos. Media never leaves the device, and is read out of the zip only when a question shows it.
3. The server parses them, holds the result in memory and returns a secret chat ID.
4. The quiz sends that chat ID in a header on each request.

An unzipped chat folder can still be chosen instead. If it has no message pages directly inside it, such as `inbox` or `messages`, the browser shows an error and uploads nothing. If the zips hold no chats, for example a JSON export, the browser says so. If a chat is too small to quiz on, the server says so and the player can choose another.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/chats` | Upload and parse one chat |
| `GET` | `/api/questions/who-said-this` | Get the next question |
| `POST` | `/api/chats/demo` | Start a quiz on the built-in demo chat |
| `POST` | `/api/rooms` | Create a multiplayer game from the chat in `X-Chat-Id` |
| `POST` | `/api/rooms/{code}/players` | Join a game in its lobby |
| WebSocket | `/api/rooms/hub` | Play a game live, through SignalR |
| `DELETE` | `/api/chats/current` | Wipe the chat from memory |

The `GET` and `DELETE` routes identify the chat with an `X-Chat-Id` header. The ID acts as a password, so it stays out of URLs, where it would reach browser history and request logs.

### `POST /api/chats`

Multipart form with one or more `message_*.html` files, all from the same chat, and an optional `media` field. Each file may be gzipped, which the server detects from its first two bytes. Decompressed pages are limited to 100 MB in total, so a small upload can't expand into a huge one. The `media` field is a JSON array of the media paths the player has, such as `["photos/123.jpg", "gifs/4.gif", "videos/5.mp4"]`. The server only asks media questions about these, because exports often leave out older media.

```json
{
  "chatId": "6f1c...",
  "title": "Chat name",
  "participants": ["A", "B", "C", "D"],
  "messageCount": 18762,
  "eligibleCount": 2140
}
```

| Status | When |
|---|---|
| `201` | The chat was parsed |
| `400` | No files, or a file isn't a recognisable Messenger page |
| `400` | The `media` field isn't a JSON array of strings, or lists more than 50,000 paths |
| `400` | A gzipped file is corrupt |
| `400` | The files come from different chats, based on the page title and participants |
| `413` | The upload is over the 50 MB limit, or its pages are over 100 MB once decompressed |
| `422` | The chat has fewer than 2 senders or too few eligible messages |
| `429` | The IP address has hit the upload rate limit |
| `503` | The server's memory cap is reached, or too many chats are already being parsed |

Kestrel's default body limit is 30 MB. The endpoint sets its own 50 MB limit, so the cap is deliberate and leaves room for larger chats than this export contains.

### `POST /api/chats/demo`

No body. Starts a new session on a made-up chat, "Flat 4B", so people without a Facebook export can try the game. It returns the same response as an upload and shares its rate limit. Every call gets its own session, so players don't share which questions they've had.

The chat is `Demo/demo-chat.json`, embedded in the API. Each message has a sender, text or media, reactions, and either an `at` time or `after`, the minutes since the previous message. Its three photos are SVGs in `client/public/demo/photos`, and the client lists them in `demoChat.ts`. The demo's photos ship with the app, so unlike a player's media they still show after a refresh.

### `GET /api/questions/who-said-this`

```json
{
  "messageId": "m_1042",
  "text": "...",
  "media": ["photos/123.jpg"],
  "context": [
    { "text": "Earlier message from the same sender", "hasMedia": false, "media": [] },
    { "text": "", "hasMedia": true, "media": ["videos/122.mp4"] }
  ],
  "options": ["A", "B", "C", "D"],
  "answer": "B",
  "reactions": 3
}
```

| Status | When |
|---|---|
| `200` | A question was returned |
| `204` | Every eligible message has been asked |
| `401` | The `X-Chat-Id` header is missing or unknown, or the chat has expired |

The answer is in the response so the client can mark it. That's fine for a casual single-player game, even though it's visible in dev tools.

## Multiple users

Each upload gets its own random chat ID, and that ID is the only thing separating users. There are no accounts, and no endpoint lists chats. If two friends upload the same chat, they get separate copies with separate IDs.

ASP.NET handles requests in parallel, so the shared parts need these protections:

| Risk | Protection |
|---|---|
| The ID leaks through URLs or logs | Send it in the `X-Chat-Id` header |
| Two tabs corrupt or repeat the asked-message list | Lock per chat around choosing a message and recording it |
| Large uploads exhaust memory and crash the server, wiping every chat | Cap total stored chat size, 1 GB by default, and return `503` when full |
| Simultaneous parsing slows everyone down | Parse at most 2 uploads at once and return `503` beyond a short queue |
| One client floods uploads | Limit each IP address to 5 uploads per minute and return `429` |

The limits are configuration values, so they can be tuned without code changes.

Parsed messages never change after upload, so they can be read in parallel without locking.

## Build steps

### 1. Scaffold

```
MessengerQuiz/
  your_activity_across_facebook/    git-ignored, never committed
  server/MessengerQuiz.Api/
  server/MessengerQuiz.Tests/
  client/
  plan.md
```

- Add `your_activity_across_facebook/` to `.gitignore` before the first commit.
- Vite proxies `/api` to the backend during development.

### 2. Ingestion

- `IChatParser` with an `HtmlChatParser` built on AngleSharp, leaving room for JSON later.
- Domain types:
  - `Chat(Id, Title, Participants, Messages)`
  - `Message(Id, Sender, Text, SentAt?, Reactions, HasMedia)`
  - `Reaction(Emoji, Reactor)`
- Keep all CSS selectors in one place. Classes like `_a6-i` are auto-generated and could change in future exports.
- Keep line breaks from `<br>` and decode HTML entities.
- Split reactions with `StringInfo`, because some emoji are several code points.
- Merge pages, then sort by `SentAt`.

### 3. Upload and chat storage

- `POST /api/chats` validates the files, parses them and stores the chat.
- `ChatStore` wraps `IMemoryCache` with a 2-hour sliding expiry and a `SizeLimit` for the total memory cap.
- Each entry is a `ChatSession`: the read-only `Chat`, the asked message IDs and a lock.
- `DELETE /api/chats/current` removes it immediately.
- The chat ID is a random GUID, so it can't be guessed.
- ASP.NET's rate limiting middleware handles the per-IP upload limit.
- A `ConcurrencyLimiter` wraps form reading and parsing, because it returns `503` where the middleware's one rejection code returns `429`.
- `ExpiredChatSweeper` removes expired chats every minute, because the cache otherwise only does this when it's accessed.

### 4. Message selection

- Candidates are messages with 30+ characters, excluding system lines, link-only messages and media-only messages.
- The pool is the top 5% of candidates by reaction count, or the top 50 if that's larger, capped at the number of candidates.
- Ties at the cut-off are broken randomly.
- Each question includes up to 3 earlier messages from the same sender, oldest first. The run stops at another sender, a system line, or a gap over 5 minutes. Media-only messages appear as a placeholder.
- Context messages are marked as asked, because a player who has seen them knows who sent them.
- Photo, GIF and video messages are quizzable without text, as long as the file is in the player's export. The folder in the path says which kind it is. They're ranked with text messages and make up at most about 16% of questions in your chats.
- Pick a random sender first, then one of their messages. Otherwise the most talkative person is nearly always the answer.
- Track asked message IDs per chat so questions don't repeat. Choosing a message and recording it happen under the chat's lock, so the two steps can't interleave.

### 5. Question generation

- `IQuestionGenerator`, so future question types plug in the same way.
- `WhoSaidThisGenerator` picks up to 4 options: the real sender plus random other senders from the chat.
- Shuffle the options so the answer's position gives nothing away.

### 6. React front end

Two screens:

1. **Select a chat:** a zip picker that lists the export's chats with a search box, or a folder picker for one unzipped chat. Clear errors for zips with no chats, a wrong folder or a rejected chat.
2. **Quiz:** the message with any earlier context, a button per option, feedback with the answer, a running score and a next button.

A "wipe my chat and exit" button calls the delete endpoint.

The chat ID is kept in `sessionStorage`, so it survives a refresh but is cleared when the tab closes.

### 7. Tests

- Parser tests against a hand-written fixture covering plain text, reactions, media, a group photo block, an unreadable timestamp and multiple pages.
- Selector tests for thresholds, the fallback, exclusions and non-repetition.
- Generator tests confirming the answer is always among the options.
- An API test for the upload-to-question path.
- Isolation tests confirming one chat ID can't reach another chat, and a missing or wrong ID returns `401`.
- A concurrency test confirming parallel question requests on one chat never repeat a message.
- Limit tests for the memory cap and upload rate limit.
- Gzip tests for a gzipped upload, the decompressed size limit and corrupt data.
- Client tests for merging chats across zips, duplicate files, sections and which media count, plus reading pages and slicing stored media out of a real zip.
- A manual check against the real export, run locally only.

## Decisions

| Decision | Why |
|---|---|
| One chat per upload | Only the chosen chat leaves the device, and the API is simpler with no session layer |
| Top 5% by reactions, with a floor of 50 | Strict 5% would leave most chats with only a handful of questions |
| Context is the same sender's run only | It answers "what were they talking about?" without giving away other people's replies |
| Photos, GIFs and videos are shown from the player's own files | No upload size problem, no extra server memory, and no media or its location data leaves the device |
| Voice messages and stickers stay as placeholders | A voice gives the answer away, and stickers are shared images rather than something the sender made |
| Media isn't restored after a refresh | Browsers can't keep file access across reloads, so photos show a note asking the player to choose the chat again |
| Player chooses the export's zips, then a chat | A chat's photos are split across zips, and players don't have to unzip gigabytes to find one folder |
| Zips are read in the browser with `@zip.js/zip.js` | It reads only the index and the entries needed, so multi-gigabyte zips aren't loaded into memory. It's loaded only when zips are chosen |
| Pages are gzipped, not parsed in the browser | Gzip makes uploads about 9 times smaller while the server keeps the only parser. Sending parsed JSON would be smaller still, but the server would have to trust the client's parsing |
| All of a chat's photo names are sent | They're a small part of the upload, and the server keeps only those the pages mention |
| Chats are held in memory only | Private messages should not be stored |
| 2-hour idle expiry, rather than the server's lifetime | A long-running server would otherwise hold everyone's messages indefinitely |
| Message text is never logged | Logs are written to disk, which would break the no-storage rule |
| Multipart uploads are buffered in memory | ASP.NET writes uploads over 64 KB to temp files by default, which would break the no-storage rule |
| Cached chats are never evicted to make room | A new upload gets `503` rather than deleting an active player's chat |
| Answer buttons, not free text | Typed names would need fuzzy matching |
| Answer returned with the question | Simplest option for a casual game |
| Test fixtures are hand-written | Real messages must never enter the repository |
| Chat ID in a header, not the URL | The ID acts as a password, and URLs reach browser history and request logs |
| A secret ID rather than accounts | A short-lived game doesn't justify storing user identities |
| One server instance | Chats exist only in the memory of the instance that received them |
| Keep the .NET server, rather than running everything in the browser | Multiplayer needs a server to share questions and scores between players' devices, and heavier games such as sentiment analysis may need server-side processing |
| Multiplayer is text only at first | Media only exists in the host's browser, and sending it to the server would break "media never leaves your device" |
| The server scores multiplayer answers | Sending answers with questions lets players read them in developer tools |
| Ready step after each reveal, with disconnected players skipped | Players get time to react to the message, and a player who leaves can't stall the game |
| A built-in demo chat | Recruiters and other visitors can try the game without requesting a Facebook download |
| No pseudonyms or encryption yet | Readable names and text make debugging much easier while the game is built. See [Privacy](#privacy) under future work |

## Multiplayer

Built. Up to 8 people play 10 "Who said this?" questions from one chat, each on their own device, with live scores.

### Flow

1. After choosing a chat or the demo, the player picks **Single player** or **Multiplayer**. Single player is the current game.
2. For multiplayer, the host enters their name and a time limit per question: 10, 20, 30 or 60 seconds, defaulting to 20.
3. The server creates a room and returns a link such as `/play/k7f3q9xw2m`. The host sees it with a copy button and a warning: "Anyone with this link will see messages from this chat. Only share it with people in the chat."
4. Other players open the link, enter a name and appear in the lobby. Joining is only possible in the lobby. After the game starts, the link shows "This game has already started".
5. The host presses **Start** once at least one other player has joined.
6. Every player sees the same question and a countdown. A player is finished when they answer or their time runs out.
7. When everyone is finished, the reveal shows the right answer, what each player picked and the points they scored. Each player presses **Ready**, and the next question starts when every connected player is ready.
8. After the last question, a results screen shows the ranking and a **Rematch** button for the host, which starts 10 new questions from the same chat with the same players.

Each player has a score bar that fills towards the most points possible in the game, so bars can be compared at a glance.

### Rules

- **Questions:** 10 per game, or as many as the chat has if fewer. They're chosen when the game starts, with the same selection as single player. A room keeps its own record of what's been asked, so it doesn't use up the host's single-player questions, and a rematch avoids questions from earlier games in that room.
- **Text only:** questions with photos, GIFs or videos are left out, and so is media context, because media only exists in the host's browser. Context messages that had media show "Sent something that can't be shown". See [future work](#game) for sharing media.
- **Scores during a question:** other players' scores and ranking leave out that question's points until the reveal, so a rising score can't show who got it right. Players can see who has answered, but not what.
- **Scoring:** a wrong answer or no answer scores 0. A right answer scores 500 points plus up to 500 more for speed, falling in a straight line from the moment the question appears to the deadline. Ties are broken by total answer time.
- **Names:** trimmed, 1 to 20 characters, letters and digits only, with single spaces between words. Unicode letters are allowed, so names such as "José" work. Control characters and invisible characters are rejected. Names must be unique in the room, ignoring case. React escapes names when it shows them, so they can't inject HTML.
- **Players:** up to 8, including the host.

### Server

- **SignalR hub** at `/api/rooms/hub`, built into ASP.NET, for real-time events: player joined or left, game started, question started, player answered, reveal, player ready and game over.
- **The server runs the game.** Rooms are held in memory alongside their chat, with the players, the questions, the answers, the scores and the deadlines. Each room has its own lock, as chats do.
- **Answers stay on the server.** Questions are sent without their answer. The server scores each guess and sends the answer only at the reveal, so nobody can read it in developer tools.
- **The server owns the timer.** Each question carries its deadline. The server allows about 1 second of slack for network delay, then ends the question itself, so a player in a background tab or with a dropped connection can't stall the game.
- **Room codes** are random and unguessable, about 10 characters from an unambiguous alphabet, giving roughly 50 bits. They're separate from the chat ID, which never appears in a link. Joining is rate-limited per IP address.
- **Player tokens:** joining returns a secret token, kept in `sessionStorage`. After a refresh or a dropped connection, the token puts the player back in their seat with their score.
- **Disconnects:** a disconnected player's questions time out as wrong. Disconnected players don't hold up the Ready step. If the host disconnects, the game carries on.
- **Ending a game:** wiping the chat ends the room, and every player sees "The host ended the game". Rooms otherwise expire with their chat. `ChatStore` raises `Removed` when a chat is wiped or expires, and `GameService` ends that chat's rooms. Playing counts as using the chat, so it doesn't expire mid-game.
- **Endpoints:** `POST /api/rooms` takes `{ name, timeLimitSeconds }` and the host's `X-Chat-Id`. `POST /api/rooms/{code}/players` takes `{ name }`. Both return `{ code, playerToken }` and share a rate limit of 20 per minute per IP address. A chat can have up to 5 games at once.
- **Hub methods:** `Connect(code, token)`, `Start()`, which is also used for a rematch, `Answer(option)` and `Ready()`. Each returns an error to show, or null. After every change, each player gets their own `room` event, which has a version number so the client can ignore updates that arrive out of order.
- **Time in tests:** timers use .NET's `TimeProvider`, so tests can move the clock forward instead of waiting.

### Client

- **Routing.** A join link has to open the join screen instead of the home screen, so `routes.ts` reads `location.pathname` and shows the game for `/play/<code>`. Players who haven't joined from that tab choose a name first. A routing library isn't needed for one route. In production, whatever serves the client must return `index.html` for `/play/...` paths too. Vite already does this in development.
- **Screens:**
  - mode choice
  - host setup, with name and time limit
  - lobby, with the link, the players and Start
  - join, with a name
  - question, with the countdown and score bars
  - reveal, with everyone's picks, points and Ready
  - results
- **Score bars** sit beside the question on wide screens and above it on phones, labelled with each name and score.

### Tests

- Integration tests that drive real SignalR connections through `WebApplicationFactory`. They cover:
  - a two-player game from start to finish
  - eight players
  - joining after the start
  - a full room
  - duplicate and invalid names
  - an answer after the deadline
  - a timeout that scores 0
  - reconnecting with a token
  - Ready with a disconnected player
  - a rematch
  - wiping the chat mid-game
- Checks that questions never include the answer before the reveal, and that a guessed room code can't be joined.
- Unit tests for scoring at the start of a question, halfway through and at the deadline, and for the tie-breaker.

### Hosting

- WebSockets must pass through any reverse proxy, with a fallback to long polling, which SignalR handles.
- Rooms live in one server instance's memory, like chats. Scaling past one instance would need sticky sessions or a SignalR backplane.

## Future work

### Question types

Each is a new `IQuestionGenerator`, scoped to the uploaded chat.

| # | Question | Notes |
|---|---|---|
| 2 | When did they say that? | Needs `SentAt`. Multiple-choice dates or a closeness score. Skip messages with no timestamp. |
| 3 | How long ago was the last use of [word]? | Needs a word index and a way to pick interesting words. |
| 4 | Who used [word] the most? | Same word index. Filter out stop words, and require a clear winner. |
| 5 | Who sent the most messages? | Simple count per sender. Only one question per chat, so vary it by month or year. |
| 6 | Who received the most reactions? | Total reactions on each sender's messages. Consider a per-message rate as a variant. |
| 7 | How many reactions did this message get? | Numeric guess, scored by closeness. Sample from messages with reactions, since most have none. |
| 8 | Which of two messages had more reactions? | Pick pairs with different counts. |
| 9 | Whose sentiment profile is this? | Compute sentiment per sender and show it without the name. See below. |

**Sentiment (question 9):** run it locally, with a lexicon such as VADER or an ML.NET model. Sending messages to a third-party AI API would conflict with the privacy goal, so that option would need explicit player consent.

### Ingestion

- JSON export support through a second `IChatParser`.
- Restoring media after a refresh, for example by asking for the zips again without a new upload.
- Showing chat titles in the chat list. The list uses folder names, because titles are inside the pages and reading every chat's pages would be slow.
- Non-English exports, since system lines are matched in English only.
- Parser-failure detection that reports an unsupported export format clearly.

### Game

- Quizzes that mix question types.
- Difficulty settings driven by `SelectionCriteria`.
- End-of-round summary and shareable score.
- Photos, GIFs and videos in multiplayer, sent from the host's browser directly to the other players with WebRTC, so they still never reach the server. The server would only pass connection messages between browsers. Some networks block direct connections, which would need a relay (TURN) server, so media questions should be skipped when the connection fails.
- Watching a multiplayer game in progress.

### Hosting

- HTTPS, so chats and chat IDs are encrypted in transit.
- Serving the built client from the API, or a reverse proxy, since `/api` is only proxied in development.
- Measuring real process memory under load. The server reached about 1 GB after parsing 64 chats back to back, but the 1 GB cap only counts estimated chat size, so it doesn't bound the whole process.
- Correct client IP handling behind a proxy, or the per-IP limit would treat all users as one.
- A privacy note explaining what is uploaded and when it's deleted.
- Scaling past one instance, using sticky sessions or a shared in-memory store. A shared store must not write chats to disk.

### Privacy

Held off while the game is in development, because readable data makes debugging easier. Revisit before it goes live.

The server currently receives the chosen chat's full pages: every message's text, sender and reactor names, timestamps, reactions, links, system lines and media file names. It also sees the player's IP address. Photos, GIFs and videos never leave the browser.

**Pseudonyms**

- The browser replaces each sender and reactor with "Person 1", "Person 2" and so on before upload, keeps the mapping, and shows real names again.
- This only partly anonymises a chat. Names inside message text, such as mentions, nicknames and system lines, can't be reliably removed.

**Encrypting message text in the browser**

- Selecting messages and building "Who said this?" questions need only metadata: senders, timestamps, ordering, reaction counts and whether a message is a system line. The text itself isn't needed.
- The browser would parse the chat, encrypt each message's text with its own key and send only metadata plus encrypted text. Questions come back with encrypted text, and the browser decrypts them for display.
- The server would still learn the number of people, when messages were sent, reaction counts and roughly how long each message is. Padding text to fixed length buckets would blur the lengths.
- Word-based questions, such as "who used [word] most?", would have to run in the browser, or use keyed hashes of words, which still reveal how often each word appears.
- Sentiment analysis on the server would conflict with this. It would need to run in the browser, or need the player's explicit consent to send text.
- Moving parsing to the browser means the server must check the metadata it receives, because it can no longer parse the pages itself.

**Keys the server never sees**

- Generate the key in the browser with Web Crypto (`crypto.subtle.generateKey` with `extractable: false`), so page code can use it but can't read it out. Keep it in IndexedDB so it survives a refresh.
- For multiplayer, share the key in the link's fragment, such as `/play#key=...`. Browsers never send the part after `#` to the server.

**Limits of any guarantee**

- Encryption protects against a hacked server, accidental logging and memory dumps, but not against a dishonest operator. The same server delivers the page's JavaScript and could serve a version that leaks the key.
- That risk can be narrowed by making the code open source, hosting the front end separately from the API, for example as a static site built from the public repository, and letting players check in the browser's network tab what's sent.
- A stronger guarantee would need confidential computing with remote attestation, such as AWS Nitro Enclaves or Azure confidential containers.
