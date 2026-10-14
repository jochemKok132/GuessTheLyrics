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
