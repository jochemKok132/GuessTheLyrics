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
- **Countdown**: every song gets 3 seconds per different word (at least 1 minute, at most 20, rounded up to 15 s);
  the clock starts on your first keystroke and turns orange/red near the end. When it runs out the game ends with
  *Time's up* and the missed words are shown. Live word count, score and progress bar
- Give up reveals the missed words in red; Play again starts a new attempt
- **Accounts**: sign up with a username and password (JWT, passwords hashed with BCrypt)
- **Progress tracking**: your current attempt is autosaved every 10 seconds, on give up/finish and when you leave the
  page, so you can continue later. Per song the best score, fastest completion time and number of attempts are kept.
- **Multiplayer**: race other accounts on the same song with the same countdown and a live scoreboard
  (see [Multiplayer](#multiplayer))
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
- Status is `InProgress`, `Completed` (every word found within the time limit, decided by the server), `GaveUp` or
  `TimeUp` (the countdown ran out). The server checks the time too: all words found but long after the limit
  (more than 5 s grace) counts as `TimeUp`, not `Completed`.
- The time limit comes from `Shared/Game/TimeLimits.cs` and is sent with the lyrics (`timeLimitSeconds`), so API and
  UI always use the same number.
- An attempt counts once when it finishes; saving a finished game again does not add attempts.
- Play again resets the current attempt; best score and fastest time stay.
- Leaderboard ranking: songs completed, then average best score, then words found.

## Multiplayer

Accounts race each other on the same song: same lyrics, same countdown, live scoreboard.

1. **Create**: on a song press *Play against others*, or search a song on the *Multiplayer* page. You are the host
   and get a 6-character code (no 0/O or 1/I/L) and a link to share.
2. **Join**: open the link, type the code on the *Multiplayer* page, or pick a match from *Open matches*
   (refreshed every 5 s). Up to 8 players.
3. **Start**: the host starts; everyone gets a 5-second countdown, then the song's normal time limit.
4. **Race**: every found word is sent to the server immediately; the page polls the scoreboard every second.
   Only your own words are revealed to you.
5. **End**: when the time is up, or when every remaining player has found all words or given up.

**Ranking:** players who found every word first (fastest wins), then most words found, then whoever reached that
count first. Players who gave up stay on the board, at the bottom.

**The server is the referee.** It owns the clock (start/end times are sent with the server time, and the UI corrects
for a wrong local clock), validates every word against the real lyrics, only adds words (never removes them), accepts
words until 5 s after the end, and decides the ranking. Refreshing the page keeps your found words.

**Where matches live.** Matches are short-lived, so they are kept in memory (`MatchStore`), not in the database. A
background job removes lobbies after 1 hour and finished matches after 30 minutes. An API restart (for example an
automatic update) ends running matches. Single-instance only, like the puzzle cache.

**Why polling instead of WebSockets.** A once-a-second scoreboard is plenty for a typing race, needs no extra packages
or WebSocket proxy settings in nginx, and survives flaky connections. Match endpoints get their own rate limit
(240 requests per minute per player) so polling never hits the normal 60-per-minute limit. Moving to SignalR later
only changes how the UI gets the scoreboard; `MatchService` stays the same.

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
| GET | `/api/matches` | 🔒 Open lobbies you can join |
| POST | `/api/matches` | 🔒 Create a match (`songId`); you are the host |
| GET | `/api/matches/{code}` | 🔒 Match state and live scoreboard (poll this) |
| POST | `/api/matches/{code}/join` | 🔒 Join a lobby |
| POST | `/api/matches/{code}/leave` | 🔒 Leave the lobby, or give up during the race |
| POST | `/api/matches/{code}/start` | 🔒 Host only: start the countdown |
| PUT | `/api/matches/{code}/progress` | 🔒 Send your found words (`foundKeys`); answers with the scoreboard |
| GET | `/api/system/version` | Running version |

🔒 = needs `Authorization: Bearer <token>`. Every response uses the `ApiResponse<T>` envelope (`value`, `succeeded`,
`message`, and on failures `errorCode` + `traceId`); see [Error handling](#error-handling).
Swagger UI runs at `https://localhost:7180/api` in Development.

## Error handling

Every failure, from any layer, ends as the same envelope:

```json
{ "succeeded": false, "message": "The lyrics service (LRCLIB) is temporarily unavailable. Try again in a moment.",
  "errorCode": "lyrics_unavailable", "traceId": "7a71708ebfdbc197cbddab6519f5e670" }
```

`message` is written for players, `errorCode` (see `Shared/ErrorCodes.cs`) is for code, and `traceId` is in the API
log line of that request, so a screenshot of an error is enough to find what happened.

### How errors flow

1. **Services throw meaningful exceptions** and never build error responses themselves: `ArgumentException`
   (validation), `AuthenticationException`, `KeyNotFoundException`, and the domain exceptions in `Domain/Exceptions`
   (`ConflictException`, `LyricsServiceException`, `DatabaseUnavailableException`).
2. **Infrastructure translates outside failures** into those exceptions:
   - `LrclibLyricsProvider` retries temporary LRCLIB failures (5xx such as *503 Service Unavailable*, 429, timeouts,
     connection errors) twice with a short backoff, respecting LRCLIB's `Retry-After` (up to 3 s). After that it throws
     `LyricsServiceException` (Unavailable / Timeout / RateLimited / InvalidResponse). Other 4xx and unreadable answers
     (HTML instead of JSON) are not retried.
   - `Repository<T>` turns a unique-index violation (two requests creating the same user or progress row at the same
     moment) and concurrency conflicts into `ConflictException`.
3. **The application degrades gracefully:** when LRCLIB is down, a song that was opened before is still served from the
   puzzle cache (also after its 30-minute lifetime), so players already in a game can keep playing and saving.
4. **`ApiError.FromException`** (`API/Handlers/ApiError.cs`) is the one mapping table from exception to status, error
   code, player message, log level and `Retry-After`. `GlobalExceptionHandler` applies it, writes the envelope and logs:
   4xx at Information without a stack trace, temporary problems at Warning, real bugs at Error with the full exception.
   Unknown exceptions never leak their message (it may contain internals); the player gets a generic text + trace id.
5. **Errors without an exception** use the envelope too: unknown routes / wrong method / 415 (`UseApiStatusCodePages`),
   invalid JSON or wrong types in a request body (`InvalidModelStateResponseFactory`), 401/403 from JWT (expired token
   gets its own message) and 429 from the rate limiter (with `Retry-After`).

| Status | errorCode | When |
| --- | --- | --- |
| 400 | `validation` | Invalid input, broken JSON, wrong types |
| 401 | `unauthorized` | Not logged in, wrong password, expired or invalid token |
| 403 | `forbidden` | Logged in but not allowed |
| 404 | `not_found` | Unknown song, player or API route |
| 405 / 415 / 413 | `method_not_allowed` / `unsupported_media_type` / `payload_too_large` | Wrong HTTP method, no JSON body, body too large |
| 409 | `conflict` | Username taken, the same row created twice at once, or a match that is full or already started |
| 429 | `rate_limited` | More than 60 requests per minute (per player, or per IP when logged out); 240 for `/api/matches` |
| 502 | `lyrics_invalid_response` | LRCLIB answered something unreadable |
| 503 | `lyrics_unavailable` | LRCLIB down/overloaded after retries (with `Retry-After`) |
| 503 | `database_unavailable` | SQL Server unreachable after EF Core's retries |
| 504 | `lyrics_timeout` / `timeout` | LRCLIB or another call took too long |
| 500 | `unexpected` | A bug: full stack trace in the log under the trace id |
| 499 | `client_closed_request` | The player left before the answer was ready (logged at Debug, nothing sent) |

### In the UI

- `HttpService` never throws: it returns the API's envelope, or builds one when there is none (no connection →
  `network`, timeout, an nginx error page while the server updates → `server_unavailable`, ...).
- Pages show errors with the `ErrorMessage` component: the message, the trace id, and a **Try again** button when the
  error code is temporary (`ErrorCodes.IsRetryable`).
- An `ErrorBoundary` in the layout catches crashes inside a page: the sidebar keeps working, the page offers
  "Try again", and navigating elsewhere resets it.
- While playing, a failed save shows "Not saved: ..." and is retried automatically on the next 10-second autosave.

> **Visual Studio stops on handled exceptions.** When the debugger is attached, Visual Studio breaks on exceptions such
> as `LyricsServiceException` because they leave your code before the error handler catches them ("user-unhandled").
> The API still answers correctly: press *Continue* (F5), or untick *Break when this exception type is user-unhandled*
> in the exception popup for the expected types (`LyricsServiceException`, `ConflictException`, `KeyNotFoundException`,
> `ArgumentException`, `AuthenticationException`).

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
  - `Lrclib:BaseUrl`, `Lrclib:UserAgent` (LRCLIB asks clients to identify themselves), `Lrclib:TimeoutSeconds`,
    and the retry settings `Lrclib:RetryCount` (2), `Lrclib:RetryBaseDelayMilliseconds` (300, doubles per retry) and
    `Lrclib:MaxRetryAfterSeconds` (3)
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

- The version comes from the Git tag (`v1.2.3`, optionally `v1.2.3-beta.1`); never edit version numbers by hand.
  `Directory.Build.props` sets `0.0.0-dev` for local builds, release builds pass `-p:Version=1.2.3`.
- The version is shown at the bottom of the sidebar and served at `/api/system/version` (the updater's health check).
- `.github/workflows/ci-build-test.yml` restores, builds and tests on every push and pull request to `master`.
- `.github/workflows/release.yml` runs when a GitHub release is published: it validates the tag, builds and tests
  with that version (failing tests = no images), then pushes two images to GitHub Container Registry:
  `ghcr.io/jochemkok132/guessthelyrics-api` and `ghcr.io/jochemkok132/guessthelyrics-ui`, tagged with the version and,
  unless it is a pre-release, `latest`.

## Releases and automatic updates

### Releasing a new version

1. Merge everything into `master` and make sure CI is green.
2. On GitHub: *Releases* → *Draft a new release* → new tag `vX.Y.Z` on `master` → *Generate release notes* → *Publish*.
   Or from the command line: `gh release create v1.2.3 --generate-notes`.
3. The *Release* workflow builds the images (a few minutes). Within 5 minutes after that the server installs the version.

Drafts and pre-releases (`v1.3.0-beta.1`, "Set as a pre-release") are never installed automatically, but still get an
image, so you can try one on the server with `update.sh v1.3.0-beta.1`.

### How the server updates itself

The server can't be reached from the internet over SSH, so nothing is pushed to it. Instead a systemd timer runs
`deploy/update.sh` every 5 minutes, which:

1. asks the GitHub API for the latest release, and stops quietly when that version is already running, when it failed
   before, or when a version is pinned;
2. backs up the database (`BACKUP DATABASE` to `/opt/guessthelyrics/backups`);
3. sets `APP_VERSION` in `.env` and runs `docker compose pull && docker compose up -d`;
4. waits until `/api/system/version` reports the new version (3 minutes max);
5. if it doesn't, puts the previous `APP_VERSION` back, restores the database backup, and remembers the failed
   version (in `state/failed-versions`) so it is not retried every 5 minutes.

The API runs the EF Core migrations at startup, so database changes ship with the release. Backups are kept; clean up
`/opt/guessthelyrics/backups` now and then.

### Server layout

| Path | What |
| --- | --- |
| `/opt/guessthelyrics/docker-compose.yml` | Copy of `deploy/docker-compose.yml`: SQL Server 2022 Express, API, UI (nginx) |
| `/opt/guessthelyrics/.env` | Secrets and settings, from `deploy/.env.example` (`chmod 600`, never committed) |
| `/opt/guessthelyrics/update.sh` | The updater |
| `/opt/guessthelyrics/backups/` | Database backups made before every update |
| `/opt/guessthelyrics/state/` | `failed-versions`, `pinned`, lock file |
| `/etc/guessthelyrics/updater.env` | Optional `GITHUB_TOKEN` when the repository is private |
| `/etc/systemd/system/guessthelyrics-update.{service,timer}` | From `deploy/systemd/` |

Ports (bound to `127.0.0.1` only, the host nginx is the public entrance): **API 5090, UI 5091**. StockFlow uses
5080/5081 on the same server; check that 5090/5091 are free with `sudo ss -ltnp` and change `API_PORT`/`UI_PORT` in
`.env` (and the nginx snippet) if not.

Public URLs: `https://jochemkok.nl/guessthelyrics/` (UI) and `https://jochemkok.nl/guessthelyrics/api/` (API). The UI
image sets its `<base href>` from `BASE_PATH` at startup, and in production calls the API on its own origin and path
(`wwwroot/appsettings.Production.json`), so no CORS is involved.

### First-time server setup

```bash
# 1. Tools (Docker is already installed for StockFlow)
sudo apt install -y jq curl

# 2. App folder with the deploy files from this repo
sudo mkdir -p /opt/guessthelyrics /etc/guessthelyrics
sudo cp deploy/docker-compose.yml deploy/update.sh /opt/guessthelyrics/
sudo cp deploy/.env.example /opt/guessthelyrics/.env
sudo chmod +x /opt/guessthelyrics/update.sh
sudo chmod 600 /opt/guessthelyrics/.env
sudo nano /opt/guessthelyrics/.env        # DB_PASSWORD, JWT_KEY (openssl rand -base64 48), ports

# 3. Only if the repository is private: GitHub token for the release check, and a GHCR login for the images
sudo cp deploy/updater.env.example /etc/guessthelyrics/updater.env && sudo chmod 600 /etc/guessthelyrics/updater.env
echo <token-with-read:packages> | sudo docker login ghcr.io -u jochemKok132 --password-stdin

# 4. Updater timer
sudo cp deploy/systemd/guessthelyrics-update.* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now guessthelyrics-update.timer

# 5. Host nginx: include deploy/nginx/guessthelyrics.conf in the jochemkok.nl server block
sudo cp deploy/nginx/guessthelyrics.conf /etc/nginx/snippets/
#    add:  include /etc/nginx/snippets/guessthelyrics.conf;   then:
sudo nginx -t && sudo systemctl reload nginx

# 6. First install (after the first release exists)
sudo /opt/guessthelyrics/update.sh
```

With a public repository, make the two packages public once (GitHub → your profile → *Packages* →
`guessthelyrics-api` / `guessthelyrics-ui` → *Package settings* → *Change visibility*), so the server can pull without a
login. Changes to `deploy/` files are not copied to the server automatically; copy them again when they change.

### Server commands

| What | Command |
| --- | --- |
| Update now (instead of waiting for the timer) | `sudo /opt/guessthelyrics/update.sh` |
| Updater log | `journalctl -u guessthelyrics-update -n 50` |
| When does the timer run next | `systemctl list-timers guessthelyrics-update.timer` |
| App logs | `cd /opt/guessthelyrics && sudo docker compose logs -f api` |
| Running version | `curl -s http://127.0.0.1:5090/api/system/version` |
| Roll back / pin a version | `sudo /opt/guessthelyrics/update.sh v1.2.3` |
| Unpin and follow new releases again | `sudo /opt/guessthelyrics/update.sh latest` |
| Pause automatic updates | `sudo systemctl stop guessthelyrics-update.timer` (`start` to resume) |
| Retry a version that failed before | `sudo /opt/guessthelyrics/update.sh latest` |

Rolling back to an older version keeps the current database. If a newer release changed the database in a way the older
version can't handle, restore the matching backup from `backups/` as well.

### Building the images locally

```bash
docker build -f src/GuessTheLyrics.API/Dockerfile --build-arg VERSION=1.0.0-local -t guessthelyrics-api .
docker build -f src/GuessTheLyrics.UI/Dockerfile  --build-arg VERSION=1.0.0-local -t guessthelyrics-ui .
```

## Credits

Lyrics data: [LRCLIB](https://lrclib.net). Lyrics remain the property of their respective owners; this project only
displays them for personal play and does not store them.
