# Thortspace API (`Thortspace.Headless`)

Thortspace exposes its engine — the same one the desktop app uses for editing, cloud sync and realtime
collaboration — as a referenceable .NET library, **`Thortspace.Headless.dll`**. You reference it, construct an
engine, and call its methods **in your own process**. No socket, server or network layer sits between your code
and the engine. This page is the reference; the [starter project](../README.md) is a worked example.

> There is also an **MCP** surface that re-exposes this same engine as self-describing tools, so an AI client can
> drive Thortspace with no code (an in-app server for the running desktop app, and a standalone headless one).
> That's a separate front over the *same* engine — see *Connecting an AI* in the README. Everything below is the
> direct API.

## Referencing it

The DLL (and its dependency closure) ships inside an installed Thortspace. Reference it from there — the engine is
shipped **obfuscated**, so its public API names are preserved but its internals are not exposed:

```xml
<Reference Include="Thortspace.Headless">
  <HintPath>$(ThortspaceSdkDir)\Thortspace.Headless.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Target **`net8.0-windows`** (the engine is Windows-targeted). At runtime the dependency DLLs must be resolvable
from the same folder — the starter does this with an `AssemblyResolve` handler reading `THORTSPACE_SDK_DIR` (see
`src/Program.cs`).

## Construction

```csharp
using Thortspace.Headless;

var engine = new HeadlessEngine(cacheDir);   // cacheDir holds this client's install id + local state
```

One engine owns a single background work pump; all calls are marshalled onto it for you. Use a **distinct
`cacheDir` per concurrent instance** (each is a separate identity / collaboration participant). There is no
`Dispose` — the pump is a background thread, so the process can simply exit.

## The surface — `IAgentEngine`

`HeadlessEngine` implements `IAgentEngine`. Methods that hit the cloud are `async`; structural edits are
synchronous (marshalled + applied on the engine pump, laid out as they apply).

### Auth, session & lifecycle

| Member | Signature | Notes |
|---|---|---|
| `LoginAsync` | `Task<HttpStatusCode>` `(email, password)` | `OK` on success; sphere list syncs shortly after |
| `RegisterAsync` | `Task<HttpStatusCode>` `(email, password)` | |
| `ListSpheres` | `List<string>` `()` | cloud ids of synced spheres |
| `CreateSphereAsync` | `Task<(HttpStatusCode code, string localId, long cloudId)>` `(name, isPublic)` | creates only — **does not open**; call `OpenSphereAsync(localId)` next |
| `OpenSphereAsync` | `Task<(HttpStatusCode code, string sphereId)>` `(sphereIdOrCloud)` | local id **or** numeric cloud id; becomes the editable session |
| `NewLocalSphere` | `void` `()` | in-memory sphere, no cloud (handy for testing) |
| `SaveAsync` | `Task<HttpStatusCode>` `()` | cloud session only; `400` = sphere frozen (see below) |
| `HostSession` | `bool` `()` | host/join the realtime collaboration session for the open sphere |
| `IsLoggedIn` / `HasSession` / `SessionIsCloud` / `CurrentSphereId` | properties | state |
| `SphereChanged` | `event Action<object>` | coalesced change signal (incl. a collaborator's edits) — carries a fresh snapshot |

### Content (layout-aware)

| Member | Signature | Notes |
|---|---|---|
| `AddThort` | `Guid` `(text, groupId?, nearThortId?, nearGroupId?, link?, markAi=false)` | **placement:** `groupId` adds into that group; `nearThortId`/`nearGroupId` places near the target; none = spread. `link` attaches an http(s) URL. A leading `#` highlights a word. |
| `SetThortText` | `bool` `(thortId, text)` | |
| `DeleteThort` | `bool` `(thortId)` | also removes paths to/from it |
| `Connect` | `object` `(from, to, relationship)` | `from`/`to` may be thort **or** group ids; `relationship` is a free label reused by name |
| `Disconnect` | `bool` `(from, to)` | removes the path either direction |
| `CreateGroup` | `Guid` `(IEnumerable<Guid> thortIds)` | moves the given thorts into a new group, near where they were |
| `MoveGroup` | `bool` `(groupId, x, y, z)` | (x,y,z) is a direction vector projected onto the sphere surface |
| `MoveThort` | `bool` `(thortId, targetGroupId?, x?, y?)` | move into a group and/or reposition within it |
| `RenameGroup` | `bool` `(groupId, name)` | |
| `GroupOfThort` | `Guid?` `(thortId)` | |
| `SetThortCategory` | `bool` `(thortId, categoryRef)` | apply a category (colour / cross-cutting dimension) |
| `AddCategory` | `Guid` `(name, r?, g?, b?)` | new category in the primary set (rgb 0–255) |
| `RenameCategory` / `RecolourCategory` / `RemoveCategory` | `bool` | |
| `AddCategorySet` | `Guid` `(name)` | a separate colouring dimension |
| `SetDefaultCategory` | `bool` `(catId)` | |
| `RenamePathType` | `bool` `(nameOrId, newName)` | renames a relationship type everywhere it's used |
| `CreateArrangement` | `Guid` `(name)` | copy the current layout into a new, independently-rearrangeable view (non-destructive reframe) |
| `SwitchArrangement` | `bool` `(arrangementId)` | which arrangement subsequent edits/snapshots operate on |
| `RenameArrangement` | `bool` `(arrangementId, name)` | |
| `Relayout` | `void` `()` | auto-movement: coagulates thorts within groups **and** spreads groups apart |
| `Coagulate` | `void` `()` | tidy each group into a hex lattice **without** spreading groups apart |
| `Snapshot` | `object` `()` | current state of the open sphere (shape below) |

### `Snapshot()` shape

```json
{
  "arrangementId": "<guid>",
  "collaborating": true,
  "arrangements": [ { "id": "<guid>", "name": "Initial" } ],
  "thorts":  [ { "id": "<guid>", "text": "...", "groupId": "<guid|null>", "categoryId": "<guid|null>" } ],
  "groups":  [ { "id": "<guid>", "location": { "x": .., "y": .., "z": .. }, "thortIds": ["<guid>", ...] } ],
  "paths":   [ { "from": "<guid>", "to": "<guid>", "relationship": "responds-to" } ],
  "categorySet": { "id": "<guid>", "name": "...", "categories": [ { "id": "<guid>", "name": "...", "color": {"r":..,"g":..,"b":..} } ] }
}
```

`location` is the group's position on the sphere (radius ~140). Use group `location` + thort `groupId` to reason
about *what is near what* when placing new content.

## Notes that bite

- **Create then open.** `CreateSphereAsync` does not bind the session — follow it with `OpenSphereAsync(localId)`
  before editing. Subsequent edits target the open sphere.
- **Public vs private saves.** A **public** sphere saves on any account tier. A **private** sphere needs a
  sync-enabled account (Premium / Subscriber / Trial / paid-org member); otherwise the new sphere is frozen and
  `SaveAsync` returns `400`.
- **Collaboration connect is slow & variable** (it races candidate addresses + TLS). After `HostSession`, poll
  `Snapshot().collaborating` (or watch `SphereChanged`) rather than editing on a fixed timer.

## Concepts in one paragraph

A **sphere** holds **thorts** (notes) arranged into **groups** on the surface of a sphere; **paths** are typed
relationships between thorts and/or groups; **categories** colour thorts along a cross-cutting dimension; an
**arrangement** is a saved spatial layout, so the same content can be re-framed several ways non-destructively.
Spatial placement *is* meaning in Thortspace — put related things near each other and use relationships and
arrangements to express structure, rather than dumping a flat list.
