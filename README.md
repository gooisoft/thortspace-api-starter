# Thortspace API — Starter

A tiny C# starter for driving [Thortspace](https://thort.space) programmatically. It references the Thortspace
engine DLL — **`Thortspace.Headless.dll`** — directly and runs the engine **in your own process**, so a program
can build and edit a sphere (add thorts, group them, connect them with typed relationships, re-colour, re-arrange,
lay it out) and save it to the cloud. There is **no socket, server, or network layer** between your code and the
engine — you call its methods directly.

This repo is two things:

1. **A runnable example** (`src/`) — takes a **topic** (a `Topic` constant you edit), fetches a reference page
   (Wikipedia by default), and builds a whole sphere from it: groups of thorts in hex lattices, typed
   relationships, a pastel colour scheme, **two arrangements**, and a guided **journey** — then saves it. It
   exercises most of the engine surface.
2. **The reference** — the [API reference](docs/API.md) for the engine surface (`IAgentEngine`).

> The Thortspace, Thortcloud and web-client source repositories are private; this starter + docs are the public
> entry point to the API. You reference the **shipped, obfuscated** `Thortspace.Headless.dll` from an installed
> Thortspace — the engine source is never exposed.
>
> **Two ways to automate Thortspace headlessly, both on this same `Thortspace.Headless.dll`:** write code against
> the engine (this starter), **or** point an MCP-capable AI at the bundled **standalone MCP server** and just
> *talk* to it — no code. See [*The standalone MCP server*](#the-standalone-mcp-server-no-code) below.

## What you need

- **Windows** and the **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **An installed Thortspace** — it ships the SDK DLLs (`Thortspace.Headless.dll` + dependencies). If you
  installed it normally there's **nothing to configure**: the starter finds them automatically at
  `%LOCALAPPDATA%\ThortspaceX64\current`.
- **A Thortspace account** — the sphere is created in whichever account you log in as.

## Set up

You only need your login. The starter finds the Thortspace SDK DLLs automatically in the standard install
folder &mdash; **`%LOCALAPPDATA%\ThortspaceX64\current`** (that is
`C:\Users\<you>\AppData\Local\ThortspaceX64\current`):

```powershell
$env:THORTSPACE_EMAIL    = "you@example.com"
$env:THORTSPACE_PASSWORD = "..."
```

Only if your Thortspace is installed somewhere non-standard, point the starter at the install folder that
contains `Thortspace.Headless.dll`:

```powershell
$env:THORTSPACE_SDK_DIR = "D:\Apps\Thortspace\current"   # optional override
```

## Run it

```powershell
git clone https://github.com/gooisoft/thortspace-api-starter.git
cd thortspace-api-starter
dotnet run --project src
```

It **creates a new public sphere** from the topic so it never touches anything you already have open, then builds
and saves it:

```
Fetching "Photosynthesis" …
  "Photosynthesis" — 6 sections.
Logged in.
Created sphere (cloudId=...).
Built 6 groups, 21 thorts.
Arranged; added a 'Wide view' arrangement.
Authored a 7-step journey.
Save: OK
Done. Open Thortspace (or thort.space) on this account to see sphere ...
```

Change the topic by editing the `Topic` constant near the top of `src/Program.cs`. Open the sphere in Thortspace /
on thort.space to see the result; in **Present mode** you can **play the journey**. (Delete the sphere when you're
done.)

> The build references `Thortspace.Headless.dll` via the `ThortspaceSdkDir` MSBuild property (defaulting to the
> standard install path). Override it explicitly if your DLLs are elsewhere:
> `dotnet build -p:ThortspaceSdkDir="C:\path\to\sdk"`.

## How it works

`src/Program.cs` is the whole demo. The engine is in-process; you call its methods:

```csharp
var engine = new HeadlessEngine(cacheDir);           // an in-process engine
await engine.LoginAsync(email, password);

var (code, localId, cloudId) = await engine.CreateSphereAsync(title, isPublic: true);
await engine.OpenSphereAsync(localId);               // make it the editable session

var t = engine.AddThort("an idea");                  // a thort (in a group -> hex lattice)
engine.Connect(g1, g2, "leads-to");                  // a typed relationship
engine.Arrange(null, null, null, true);              // cluster related groups + reduce path crossings
var trip = engine.CreateTrip("a guided tour");       // a journey...
engine.AddTripStep(trip, "...", null, g1.ToString(), null, "Step 1", "group");

await engine.SaveAsync();                             // persist to the cloud
```

`Program.cs` does the full version: fetch the topic → a group per section → typed paths → pastel categories →
`Arrange` → a second arrangement → a journey. The complete engine surface — spheres, thorts, groups, paths,
categories, arrangements, layout, **journeys**, login/save — is the `IAgentEngine` interface, documented in
**[docs/API.md](docs/API.md)**.

A few facts worth knowing:

- **Create then open.** `CreateSphereAsync` creates the sphere but does not bind it as the editable session —
  call `OpenSphereAsync(localId)` next (the demo does). Subsequent edits target the open sphere.
- **Public vs private saves.** A **public** sphere saves on any account tier. A **private** sphere needs a
  sync-enabled account (Premium / Subscriber / Trial / paid-org member); otherwise the new sphere is frozen and
  `SaveAsync` is rejected. The demo creates a public sphere so it works on any account.
- **Journeys need a sync-enabled account.** Authoring a journey (`CreateTrip` / `AddTripStep`) works on any
  account, but *persisting* it to the cloud is gated like a private sphere — a free account builds the sphere
  fine but the journey stays local. Use a Premium / Subscriber / Trial / paid-org account to keep journeys.
- **Colours: keep backgrounds pastel.** A sphere starts with a deliberately **pastel** colour palette so
  full-colour paths and dark text read over it. *Rename* those categories (don't recolour them strong); reserve
  full colours for path types (`RecolourPathType`). If you do set a strong/dark background, give it a whitish
  text colour (`RecolourCategory` takes an optional text colour).

## Content source

`Program.cs` fetches the topic through an `IContentSource` (`src/ContentSource/`). Two adapters ship:

- **`WikipediaContentSource`** (default) — the MediaWiki extracts API; reliable, works from any HTTP client.
- **`GrokipediaContentSource`** (stub) — grokipedia.com currently 403-blocks plain HTTP (Cloudflare bot
  protection), so it's left unimplemented; wire it up with your own access by mirroring the Wikipedia adapter.

Swap the source by changing the `Source` field near the top of `Program.cs`, or add your own `IContentSource`.

## The standalone MCP server (no code)

Alongside this code path, Thortspace ships a **standalone MCP server** — `Thortspace.Mcp.exe`, bundled in the
`mcp\` folder of an installed Thortspace — that re-exposes this **same `Thortspace.Headless` engine** as
self-describing MCP tools. It's the no-code way to do the same headless production: an MCP-capable AI
(Claude Code, Gemini CLI, Claude Cowork, …) launches it over stdio and drives Thortspace by calling tools.

Register it with your AI client (example for Claude Code):

```powershell
claude mcp add thortspace `
  -e THORTSPACE_EMAIL=you@example.com -e THORTSPACE_PASSWORD=... `
  -- "%LOCALAPPDATA%\ThortspaceX64\current\mcp\Thortspace.Mcp.exe"
```

- It authenticates at startup from `THORTSPACE_EMAIL` / `THORTSPACE_PASSWORD` (set in your MCP client config, as
  above) or a `credentials.json` (`%LOCALAPPDATA%\ThortspaceMcp\credentials.json` by default). There is **no
  `login` MCP tool**, so an AI never sees your credentials.
- It shares the one engine and the one tool/command vocabulary with the code path here — including journeys.
- Then just ask: *"build a Thortspace sphere about X with a few groups, connect them, and add a journey."*

There's also an **in-app** MCP server (the running desktop app, over HTTP) for editing the sphere you have
**open**, live — turn on *User Settings → AI Connection*, then
`claude mcp add --transport http thortspace http://127.0.0.1:8787/mcp`. Full setup guide:
[thort.space → Connect Your AI](https://thort.space/overlay/ajax/docs/connect-ai).

## License

MIT — see [LICENSE](LICENSE). Use it as the seed for your own integrations.
