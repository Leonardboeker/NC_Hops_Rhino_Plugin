# Todo App

Desktop to-do list built with Electron, React, SQLite, and Google Calendar integration.

## Quick Start

```bash
npm install
npm run rebuild   # rebuild better-sqlite3 for Electron
npm start         # build renderer + launch app
```

## Google Calendar Setup (optional)

1. Go to [Google Cloud Console](https://console.cloud.google.com/) and create a project
2. Enable the **Google Calendar API**
3. Create **OAuth 2.0 credentials** — choose **Desktop app** type
4. Under "Authorized redirect URIs" add: `http://localhost:3456/oauth2callback`
5. Download `credentials.json` and place it in your app data directory:

| Platform | Path |
|----------|------|
| Windows  | `%APPDATA%\todo-app\credentials.json` |
| macOS    | `~/Library/Application Support/todo-app/credentials.json` |
| Linux    | `~/.config/todo-app/credentials.json` |

On first use of the "Cal" button, a browser tab will open for Google authorization. After approving, a `token.json` is saved next to `credentials.json` and reused for subsequent requests.

## Scripts

| Command | Description |
|---------|-------------|
| `npm start` | Build renderer (webpack) then launch Electron |
| `npm run build` | Webpack production build only |
| `npm run dev` | Webpack watch mode (renderer only) |
| `npm run rebuild` | Rebuild `better-sqlite3` native module for Electron |

## Data

Tasks are stored in SQLite at your platform's `userData` path (same directory as `credentials.json`, file: `tasks.db`). The database is created automatically on first launch.

## Stack

- **Electron 29** — desktop shell
- **React 18** — UI
- **better-sqlite3** — local task persistence
- **googleapis** — Google Calendar OAuth2 + event creation
- **webpack 5 + Babel** — renderer bundling
