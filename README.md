# Messenger Quiz

A game that quizzes you and your friends on your own Facebook Messenger group chat. Choose a chat from your Facebook download and guess who sent each message, photo, GIF or video.

**No Facebook data? Press "Try a demo chat"** on the home screen to play with a made-up group chat between four flatmates.

Right now there's one question type, **"Who said this?"**. It picks the chat's most-reacted messages and shows up to three earlier messages from the same sender, so you have the context. More question types are planned in [plan.md](plan.md).

## Privacy

**If you run Messenger Quiz on your own computer, none of your data leaves it.** The browser and the server both run on your machine and only talk to each other over `localhost`. Nothing is sent to Facebook, to a third party or to the internet.

The exception is **multiplayer**, when you choose it. The players you invite see the game's questions, which are messages from your chat. Photos, GIFs and videos are never used in multiplayer.

How your chat is handled:

- **Only the chat you choose is read.** The browser reads that chat's message pages out of your download and sends them to the local server. Nothing else in the download is sent.
- **Photos, GIFs and videos never leave the browser.** They're shown straight from your zip files. The server only gets their file names, so it knows which ones exist.
- **Messages are held in memory only.** They're never saved to disk or a database, and never written to logs. A chat is deleted after 2 hours without use, when you press **Wipe my chat and exit**, or when the server stops.
- **Each chat is private to the browser tab that uploaded it.** The server only answers requests that include that chat's secret ID.

**The hosted version only plays the demo chat.** Real chats can't be uploaded to it, so it never holds anyone's messages. Everything above applies when you run Messenger Quiz on your own computer, which is the only way to play your own chats.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20.19 or later, or 22.12 or later

## Running it

Install the client's packages once:

```bash
npm install --prefix client
```

Start the server, which runs on http://localhost:5256:

```bash
dotnet run --project server/MessengerQuiz.Api --launch-profile http
```

In a second terminal, start the client:

```bash
npm run dev --prefix client
```

Then open http://localhost:5173 in your browser.

### Running the tests

Server tests:

```bash
dotnet test MessengerQuiz.sln
```

Client tests:

```bash
npm test --prefix client
```

## Getting your Facebook data

Facebook takes a while to prepare your download, from a few minutes to a day or more for a large account, so request it before you want to play. Facebook moves these settings around from time to time, so the names below may differ slightly.

1. On Facebook, open **Settings & privacy**, then **Settings**, then **Accounts Center**.
2. Go to **Your information and permissions**, then **Download your information**.
3. Choose **Download or transfer information** and pick your Facebook profile.
4. Choose **Specific types of information** and tick **Messages**. Nothing else is needed.
5. Choose **Download to device**, then set:
   - **Date range:** All time, or a shorter range for a smaller download
   - **Format:** **HTML**. Messenger Quiz doesn't read the JSON format yet.
   - **Media quality:** any. Lower quality makes the download smaller.
6. Choose **Create files**. Facebook notifies you when the download is ready.
7. Download **every** zip file. Large downloads are split into several zips, and one chat's photos and videos are often spread across them.

You don't need to unzip anything.

## Playing

To try it straight away, open http://localhost:5173 and press **Try a demo chat**. You don't need a Facebook download for this.

To play with your own chat:

1. Open http://localhost:5173 and choose **Choose your export zips**.
2. Select all the zips from your download at once.
3. Pick a chat from the list. Search helps if you have a lot of them. Group chats with three or more active people work best. Very small chats are rejected because there's not enough to quiz on.
4. Pick **Single player**, or **Multiplayer** to play with friends (below).
5. For each message, pick who sent it. You'll see whether you were right and a running score.
6. When you're done, press **Wipe my chat and exit** to delete the chat from the server straight away.

If you've already unzipped your download, choose **Or choose an unzipped chat folder** and select one chat's folder inside `messages/inbox`.

Photos and videos can't be shown again after you refresh the page, because browsers don't keep access to files you've chosen. Choose your zips again to get them back.

### Multiplayer

1. After choosing a chat or the demo, pick **Multiplayer**.
2. Enter your name and choose how long each question lasts. The default is 20 seconds.
3. Send the link to up to 7 other people. Anyone with the link sees messages from the chat, so only share it with people in the chat.
4. When everyone has joined, press **Start the game**. Everyone gets the same 10 questions. Right answers score 500 points, plus up to 500 more for answering quickly.
5. After each question, everyone sees the answer and each other's picks, then presses **Ready** for the next one.

When running locally, other people can only open the link if they're on your network. Start the client with `npm run dev --prefix client -- --host`, which prints a network address, and share the link using that address instead of `localhost`.

## Hosting

Messenger Quiz can be hosted on Google Cloud Run as a demo-only site, within Cloud Run's free allowance and with a billing cap. [deploy/README.md](deploy/README.md) explains how, step by step.

## Project layout

| Path | What's there |
|---|---|
| `server/MessengerQuiz.Api` | .NET Minimal API. Parses chats, picks messages and generates questions |
| `server/MessengerQuiz.Api/Demo` | The made-up demo chat. Its photos are in `client/public/demo` |
| `server/MessengerQuiz.Tests` | Server tests. Uses hand-written fake chats, never real messages |
| `server/MessengerQuiz.Api/Multiplayer` | Multiplayer rooms, rules, scoring and the SignalR hub |
| `client` | React, TypeScript and Vite front end. Reads the zips and plays the quiz |
| `Dockerfile` | Builds the client and server into one container for hosting |
| `deploy` | Hosting guide, and the function that caps Google Cloud spending |
| `plan.md` | Design notes, API details, decisions and future work |

Don't commit your Facebook download to the repository. The usual export folder and zip names are in `.gitignore`.
