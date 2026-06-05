# Thortspace API — Starter

A tiny C# starter for driving [Thortspace](https://thort.space) programmatically. It references the Thortspace
engine DLL — **`Thortspace.Headless.dll`** — directly and runs the engine **in your own process**, so a program
can build and edit a sphere (add thorts, group them, connect them with typed relationships, re-colour, re-arrange,
lay it out) and save it to the cloud. There is **no socket, server, or network layer** between your code and the
engine — you call its methods directly.

This repo is two things:

1. **A runnable example** (`src/`) — logs in, **creates a fresh sphere**, and builds a little structure in it
   (thorts → typed paths → group → layout → save).
2. **The reference** — the [API reference](docs/API.md) for the engine surface (`IAgentEngine`).

> The Thortspace, Thortcloud and web-client source repositories are private; this starter + docs are the public
> entry point to the API. You reference the **shipped, obfuscated** `Thortspace.Headless.dll` from an installed
> Thortspace — the engine source is never exposed.
>
> Prefer to connect an **AI** and just *talk* to Thortspace instead of writing code? That is a **separate
> surface** — an MCP server that re-exposes this same engine as tools. See *Connecting an AI* below.

## What you need

- **Windows** and the **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **An installed Thortspace** that ships the SDK DLLs (`Thortspace.Headless.dll` + its dependencies). Point the
  build/runtime at that folder (see below).
- **A Thortspace account** — the sphere is created in whichever account you log in as.

## Set up

The project resolves `Thortspace.Headless.dll` and its dependencies from one folder — your Thortspace SDK
directory. Tell it where that is (skip this if Thortspace is installed at the default
`%LOCALAPPDATA%\ThortspaceX64\current`):

```powershell
$env:THORTSPACE_SDK_DIR = "C:\full\path\to\thortspace\sdk\dlls"   # contains Thortspace.Headless.dll + deps
$env:THORTSPACE_EMAIL    = "you@example.com"
$env:THORTSPACE_PASSWORD = "..."
```

## Run it

```powershell
git clone https://github.com/gooisoft/thortspace-api-starter.git
cd thortspace-api-starter
dotnet run --project src
```

It **creates a new sphere** ("Thortspace API starter — …") so it never touches anything you already have open,
then builds and saves it:

```
Logged in.
Created sphere (cloudId=...).
Added 3 thorts, connected them, grouped, and laid out.
Save: OK
Done. Open Thortspace (or thort.space) to see sphere ...
```

Open that sphere in Thortspace / on thort.space to see the result (and delete it when you're done).

> The build references `Thortspace.Headless.dll` via the `ThortspaceSdkDir` MSBuild property (defaulting to the
> standard install path). Override it explicitly if your DLLs are elsewhere:
> `dotnet build -p:ThortspaceSdkDir="C:\path\to\sdk"`.

## How it works

`src/Program.cs` is the whole demo. The shape:

```csharp
var engine = new HeadlessEngine(cacheDir);          // an in-process engine
await engine.LoginAsync(email, password);

var (code, localId, cloudId) = await engine.CreateSphereAsync("My sphere", isPublic: true);
await engine.OpenSphereAsync(localId);               // make it the editable session

var a = engine.AddThort("first idea");
var b = engine.AddThort("second idea");
engine.Connect(a, b, "leads-to");                    // typed relationship
engine.CreateGroup(new[] { a, b });                  // gather into a cluster
engine.Relayout();                                   // tidy positions

await engine.SaveAsync();                             // persist to the cloud
```

The complete engine surface — spheres, thorts, groups, paths, categories, arrangements, layout, login/save — is
the `IAgentEngine` interface, documented in **[docs/API.md](docs/API.md)**.

A couple of facts worth knowing:

- **Create then open.** `CreateSphereAsync` creates the sphere but does not bind it as the editable session —
  call `OpenSphereAsync(localId)` next (the demo does). Subsequent edits target the open sphere.
- **Public vs private saves.** A **public** sphere saves on any account tier. A **private** sphere needs a
  sync-enabled account (Premium / Subscriber / Trial / paid-org member); otherwise the new sphere is frozen and
  `SaveAsync` is rejected. The demo creates a public sphere so it works on any account.

## Connecting an AI (MCP)

You can also point an MCP-capable AI (Claude Code, Gemini CLI, …) at Thortspace and just *talk* to it — no code.
That is a **separate surface**: an MCP server that re-exposes this same engine as self-describing tools. Two
flavours ship with Thortspace — one **in the running desktop app** (turn on *User Settings → AI Connection*, then
`claude mcp add --transport http thortspace http://127.0.0.1:8787/mcp`) so the AI edits the sphere you have open,
and one **standalone/headless** for unattended production. Setup guide:
[thort.space → Connect Your AI](https://thort.space/overlay/ajax/docs/connect-ai). It is a different surface from
this code starter — both sit on the same `Thortspace.Headless` engine.

## License

MIT — see [LICENSE](LICENSE). Use it as the seed for your own integrations.
