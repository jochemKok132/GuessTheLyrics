# GuessTheLyrics

Search any song and type its lyrics from memory, in the style of [That's Your Jam](https://www.thatsyourjam.com/).
The lyrics are hidden as blank boxes. Type any word from the song: when it matches, it is revealed **everywhere** it
appears and the input clears itself, so you just keep typing. Finish every word, or give up to see what you missed in red.

Lyrics are fetched live from [LRCLIB](https://lrclib.net), a free, open lyrics database (no API key needed), so
practically every song is playable. With an account your progress on every song is saved, and you can see how
everyone else is doing on the leaderboard, on player profiles and next to the lyrics while you play.

## Features

- Song search by title and/or artist (duplicates and instrumental tracks filtered out)
- Lyrics laid out per verse in columns (as many as fit the screen), each verse with its `[Chorus]`-style label and a
  found/total counter that turns green when the verse is done; blanks are sized to the word length
- Forgiving matching: case, accents, punctuation and apostrophes are ignored (`dont` matches *Don't*, `cafe` matches *Café*)
- Smart accept: a word is accepted as soon as it is unambiguous. If a longer hidden word starts the same way (*a* vs
  *again*), keep typing, or press space/Enter to accept the short word. Space on a wrong guess clears it.
- Timer starts on your first keystroke, live word count and score, progress bar
- Give up reveals the missed words in red; Play again starts a new attempt
- **Accounts**: sign up with a username and password (JWT, passwords hashed with BCrypt)
- **Progress tracking**: your current attempt is autosaved every 10 seconds, on give up/finish and when you leave the
  page, so you can continue later. Per song the best score, fastest completion time and number of attempts are kept.
- **Other players**: a leaderboard (`/players`), a profile per player with all their songs (`/players/{username}`) and a
  "Players on this song" panel next to the lyrics
- Shareable URLs: `/play/{lrclibId}` for a song, `/?q=...` for a search
- Dark UI, works on phones (the sidebar becomes a top bar)

## Solution layout

| Project | Purpose |
| --- | --- |
| `GuessTheLyrics.Domain` | Entities (`User`, `SongProgress`), `LyricsTrack`, `PlayerSummary`, repository/provider/password interfaces. References nothing. |
| `GuessTheLyrics.Application` | `SongService`, `AuthService`, `ProgressService`, `PlayerService`, `LyricsTokenizer`, `PuzzleCache`, validation and mapping. |
| `GuessTheLyrics.Shared` | `ApiResponse<T>`, request/response DTOs, `WordNormalizer` (used by both API and UI so matching is identical), `AppVersion`. |
| `GuessTheLyrics.Infrastructure` | EF Core (`AppDbContext`, configurations, migrations, repositories), BCrypt, and `LrclibLyricsProvider`. |
| `GuessTheLyrics.API` | ASP.NET Core Web API: controllers, JWT auth, global exception handler, rate limiter, CORS, Swagger. |
| `GuessTheLyrics.UI` | Blazor WebAssembly frontend: search, game, login/sign-up, players and profiles. References only `Shared`. |
| `GuessTheLyrics.UnitTests` | xUnit + Moq tests for the normalizer, tokenizer and all services. |

### How a game works

1. The UI calls `GET /api/songs/search?query=...`; the API asks LRCLIB (`/api/search?q=`) and returns only songs with lyrics.
   LRCLIB data is incomplete for some tracks (`duration`, `albumName`, `instrumental` can be `null`); the provider treats
   those as unknown instead of failing the whole search.
2. Picking a song calls `GET /api/songs/{id}/puzzle`. The API fetches the lyrics (`/api/get/{id}`) and splits each line into
   tokens. Each word token carries a normalised `key`; spaces and punctuation have none and are always visible.
3. The UI keeps the game state in the browser. Every keystroke is normalised with the same `WordNormalizer`. When it matches
   a key that has not been found yet (and no longer hidden word starts with it), all its occurrences are revealed and the
   input clears.
4. When logged in, the UI sends `PUT /api/progress/songs/{id}` with the found keys and the time. The server does not trust
   the counts: it recounts the found words against the real lyrics (cached in memory for 30 minutes), ignores unknown
   words and decides itself when a song is completed.

### Progress rules

- One `SongProgress` row per player per song: the current attempt (found words, time, status) plus the best result.
- Status is `InProgress`, `Completed` (every word found, decided by the server) or `GaveUp`.
- An attempt counts once when it finishes; saving a finished game again does not add attempts.
- Play again resets the current attempt; best score and fastest time stay.
- Leaderboard ranking: songs completed, then average best score, then words found.

## API

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/songs/search?query={text}` | Search songs (2-100 characters), max 20 results |
| GET | `/api/songs/{id}/puzzle` | Lyrics of an LRCLIB track split into guessable words |
| POST | `/api/auth/register` | Create an account (`username` 3-20 chars of letters, digits, `-`, `_`; `password` 8+ chars), returns a JWT |
| POST | `/api/auth/login` | Log in, returns a JWT |
| GET | `/api/progress/songs/{id}` | 🔒 Your progress on a song (empty `value` when never played) |
| PUT | `/api/progress/songs/{id}` | 🔒 Save the current attempt (`foundKeys`, `elapsedSeconds`, `status`) |
| GET | `/api/progress/songs/{id}/players` | Everyone's progress on a song |
| GET | `/api/players` | Leaderboard |
| GET | `/api/players/{username}` | A player's stats and songs |
| GET | `/api/system/version` | Running version |

🔒 = needs `Authorization: Bearer <token>`. Every response uses the `ApiResponse<T>` envelope (`value`, `succeeded`,
`message`). Errors map to 400 (validation), 401 (not logged in / wrong password), 404 (unknown song, no lyrics, unknown
player), 429 (rate limit: 60 requests per minute per player, or per IP when logged out), 502/504 (LRCLIB unreachable or slow).
Swagger UI runs at `https://localhost:7180/api` in Development.

## Running locally

Requirements: the .NET 10 SDK (Visual Studio 2026, or `dotnet --list-sdks` shows `10.0.x`) and SQL Server LocalDB
(installed with Visual Studio's *ASP.NET and web development* workload; check with `sqllocaldb info`).

The API applies the EF Core migrations at startup, so the `GuessTheLyrics` database is created automatically on the
first run.

**Visual Studio:** right-click the solution → *Configure Startup Projects* → *Multiple startup projects* → set
`GuessTheLyrics.API` and `GuessTheLyrics.UI` to *Start*, then press F5.

**CLI** (two terminals):

```bash
dotnet run --project src/GuessTheLyrics.API --launch-profile https   # https://localhost:7180
dotnet run --project src/GuessTheLyrics.UI  --launch-profile https   # https://localhost:7181
```

Then open https://localhost:7181.

| App | HTTPS | HTTP |
| --- | --- | --- |
| API | 7180 | 5180 |
| UI | 7181 | 5181 |

### Configuration

- `src/GuessTheLyrics.API/appsettings.json`
  - `ConnectionStrings:DefaultConnection`: SQL Server. Defaults to LocalDB (`(localdb)\MSSQLLocalDB`).
  - `Jwt:Issuer`, `Jwt:ExpiryMinutes` (default 7 days). `Jwt:Key` (32+ characters) is only in
    `appsettings.Development.json`; in production set it with the `JWT__Key` environment variable and never commit it.
  - `Lrclib:BaseUrl`, `Lrclib:UserAgent` (LRCLIB asks clients to identify themselves), `Lrclib:TimeoutSeconds`
  - `Frontend:AllowedOrigins`: the UI origins allowed by CORS. Add your production domain here when deploying.
- `src/GuessTheLyrics.UI/wwwroot/appsettings.json`
  - `ApiBaseUrl`: where the UI finds the API.

### Database and migrations

Tables: `Users` (unique `NormalizedUsername` among non-deleted users) and `SongProgress` (unique per user + song,
indexed on `SongId`), both with soft delete (`DeletedAt` + query filter). The migration history table is `Migrations`.

Add a migration after changing an entity (and read it before applying it):

```bash
dotnet ef migrations add <Name> --project src/GuessTheLyrics.Infrastructure --startup-project src/GuessTheLyrics.API --output-dir Persistence/Migrations
```

Reset the local database: `dotnet ef database drop --project src/GuessTheLyrics.Infrastructure --startup-project src/GuessTheLyrics.API`.

## Tests

```bash
dotnet test GuessTheLyrics.slnx
```

## Versioning and CI

- `Directory.Build.props` sets `0.0.0-dev` for local builds; release builds pass `-p:Version=x.y.z` from the Git tag.
- The version is shown at the bottom of the sidebar and served at `/api/system/version`.
- `.github/workflows/ci-build-test.yml` restores, builds and tests on every push and pull request to `master`.

## Credits

Lyrics data: [LRCLIB](https://lrclib.net). Lyrics remain the property of their respective owners; this project only
displays them for personal play and does not store them.
