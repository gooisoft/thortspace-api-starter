# Thortspace API (`Thortspace.Headless`)

Thortspace exposes its engine — the same one the desktop app uses for editing, cloud sync and realtime
collaboration — as a referenceable .NET library, **`Thortspace.Headless.dll`**. You reference it, construct an
engine, and call its methods **in your own process**. No socket, server or network layer sits between your code
and the engine. This page is the reference; the [starter project](../README.md) is a worked example.

> There is also an **MCP** surface that re-exposes this same engine as self-describing tools, so an AI client can
> drive Thortspace with no code: the bundled **standalone stdio server** `Thortspace.Mcp.exe` (the no-code twin of
> this DLL path), plus an in-app HTTP server for the running desktop app. See *The standalone MCP server* in the
> README. Everything below is the direct API.

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

## Requirements, versions & account tiers

**Minimum Thortspace version: 1.6.718.** `Thortspace.Headless.dll` first shipped in **1.6.717**, but the full
surface documented here — the complete `IAgentEngine`, journeys, cross-sphere linking and the topology-aware
layout tools — was finalised in **1.6.718**. Reference a **1.6.718-or-later** install. APIs added in later
releases will carry an explicit `Since:` note; unless stated otherwise everything here is **`Since: 1.6.718`**,
with no upper bound (works on current releases).

**Account tiers.** Almost the entire API works on a **free** account — creating a **public** sphere and *all*
content building: thorts, groups, typed paths, categories, arrangements, layout, and *authoring* journeys. Only
two things need a **sync-enabled** account (marked **⭐** below — that's Premium / Subscriber / Trial / a member
of a paid organisation):

- **⭐ Saving a _private_ sphere** — `SaveAsync` after `SetSpherePublic(false)`, or a sphere created private. On a
  free account a private sphere is frozen and `SaveAsync` returns `400`. (A **public** sphere saves on any tier.)
- **⭐ Persisting a journey** — `CreateTrip` / `AddTripStep` *author* fine on any account, but the trip only
  *saves* to the cloud on a sync-enabled account (same gate as a private sphere).

Everything not marked ⭐ is **free**. Status codes you'll see: `OK` success · `Unauthorized` bad login ·
`400` save rejected (frozen private sphere on a free account) · `403` sphere-count limit reached.

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
| `SaveAsync` | `Task<HttpStatusCode>` `()` | cloud session only; **⭐ for a _private_ sphere** (a public sphere saves on any tier); `400` = sphere frozen (see below) |
| `HostSession` | `bool` `()` | host/join the realtime collaboration session for the open sphere |
| `IsLoggedIn` / `HasSession` / `SessionIsCloud` / `CurrentSphereId` | properties | state |
| `SphereChanged` | `event Action<object>` | coalesced change signal (incl. a collaborator's edits) — carries a fresh snapshot |

### Content (layout-aware)

| Member | Signature | Notes |
|---|---|---|
| `AddThort` | `Guid` `(text, groupId?, nearThortId?, nearGroupId?, link?, markAi=false, placement?)` | **placement:** `groupId` adds into that group (thorts form a hex lattice); `nearThortId`/`nearGroupId` places near the target; else `placement` `"spread"`/`"cluster"` or the radius default. `link` attaches an http(s) URL. A leading `#` highlights a word. |
| `SetThortText` | `bool` `(thortId, text)` | |
| `DeleteThort` | `bool` `(thortId)` | also removes paths to/from it |
| `Connect` | `object` `(from, to, relationship)` | `from`/`to` may be thort **or** group ids; `relationship` is a free label reused by name |
| `Disconnect` | `bool` `(from, to)` | removes the path either direction |
| `CreateGroup` | `Guid` `(IEnumerable<Guid> thortIds, placement?)` | moves the given thorts into a new group, near where they were |
| `MoveGroup` | `bool` `(groupId, x, y, z)` | (x,y,z) is a direction vector projected onto the sphere surface |
| `MoveThort` | `bool` `(thortId, targetGroupId?, x?, y?)` | move into a group and/or reposition within it |
| `RenameGroup` | `bool` `(groupId, name)` | |
| `GroupOfThort` | `Guid?` `(thortId)` | |
| `SetThortCategory` | `bool` `(thortId, categoryRef)` | apply a category (colour / cross-cutting dimension) |
| `AddCategory` | `Guid` `(name, r?, g?, b?)` | new category in the primary set (rgb 0–255) |
| `RenameCategory` / `RemoveCategory` | `bool` | |
| `RecolourCategory` | `bool` `(catId, r, g, b, textR?, textG?, textB?)` | background + optional text colour. Keep backgrounds **pastel** (prefer `RenameCategory`); if a background is strong/dark, set whitish text. |
| `AddCategorySet` / `RenameCategorySet` / `RemoveCategorySet` | `Guid` / `bool` | a category set is a separate colouring dimension (keeps ≥1) |
| `SetDefaultCategory` | `bool` `(catId)` | |
| `RenamePathType` / `RecolourPathType` / `ReorderPathTypes` | `bool` | relationship types; full colours belong on paths |
| `CreateArrangement` | `Guid` `(name)` | copy the current layout into a new, independently-rearrangeable view (non-destructive reframe) |
| `SwitchArrangement` / `RenameArrangement` / `DeleteArrangement` / `ReorderArrangements` | `bool` | which arrangement edits/snapshots target; manage the set (keeps ≥1) |
| `RenameSphere` / `SetSpherePublic` | `bool` | the open sphere's title / publish state. **⭐ a _private_ sphere needs a sync-enabled account to save** |
| `LinkSphereAsync` | `Task<(HttpStatusCode code, string otherLocalId, long otherCloudId)>` `(otherSphereIdOrCloud, nearGroupId?)` | link the open sphere to ANOTHER (bidirectional neighbourhood link, same as the app's "Add Link to Sphere"); loads the other sphere if needed. Both spheres must be on your account. Returns the linked sphere's ids — pass `otherLocalId` to `AddTripStep`'s `networkSphereId` so one journey **spans both spheres**. |
| `Relayout` | `void` `()` | auto-movement: coagulates thorts within groups **and** spreads groups apart |
| `Coagulate` | `void` `()` | tidy each group into a hex lattice **without** spreading groups apart |
| `ArrangeGroup` | `bool` `(groupId, formation)` | re-lay a group's thorts: `"hex"` (default), `"line"`, `"ring"`, `"square"`, `"freeform"` |
| `Arrange` | `object` `(scope?, style?, spacing?, reduceCrossings)` | topology-aware layout: clusters related groups + **reduces path crossings** (chain→arc, star→spokes, tree→layered, cycle→ring). `scope` null = whole arrangement; `spacing` `"spread"`/`"cluster"`. |
| `Snapshot` | `object` `()` | current state of the open sphere (shape below) |

### Journeys (guided "trips") &nbsp; ⭐ *authoring is free; persisting needs a sync-enabled account*

A journey is an ordered sequence of view-steps; each step records an arrangement, a focus node and a narration.
The camera is **derived from the focus at playback**, so authoring just sets focus + framing. Authoring works
headless; **persisting a journey to the cloud needs a sync-enabled account** (same gate as a private sphere).
Playback is in-app (Present mode) only. A journey can **span linked spheres**: `LinkSphereAsync` two spheres,
then give a step a `networkSphereId` so it shows the linked sphere as a neighbour — the tour travels between them.

| Member | Signature | Notes |
|---|---|---|
| `CreateTrip` | `string` `(name)` | returns the trip id |
| `AddTripStep` | `bool` `(tripId, description, arrangementId?, focusGroupId?, focusThortId?, name?, framing?, networkSphereId?, networkArrangementId?)` | `framing`: `"group"` (default), `"thort"`, `"wide"`/`"overview"`, `"neighbourhood"`. A focus-less overview step aims at the content centroid. Set `networkSphereId` (a sphere linked via `LinkSphereAsync`) to make a **cross-sphere step** that shows the linked sphere as a neighbour — neighbourhood framing is applied automatically. |
| `ListTrips` / `GetTrip` | `object` | list / fetch one trip's steps |
| `RenameTrip` / `SetTripPublic` | `bool` | |
| `EditTripStep` / `DeleteTripStep` / `ReorderTripSteps` | `bool` | edit by 0-based step index |
| `DeleteTrip` | `bool` `(tripId)` | |

### `Snapshot()` shape

```json
{
  "arrangementId": "<guid>",
  "radius": 140,
  "collaborating": true,
  "arrangements": [ { "id": "<guid>", "name": "Initial" } ],
  "thorts":  [ { "id": "<guid>", "text": "...", "groupId": "<guid|null>", "categoryId": "<guid|null>" } ],
  "groups":  [ { "id": "<guid>", "location": { "x": .., "y": .., "z": .. }, "thortIds": ["<guid>", ...] } ],
  "paths":   [ { "from": "<guid>", "to": "<guid>", "relationship": "responds-to" } ],
  "pathTypes":   [ { "id": "<guid>", "name": "...", "color": {"r":..,"g":..,"b":..} } ],
  "categorySet": { "id": "<guid>", "name": "...", "categories": [ { "id": "<guid>", "name": "...", "color": {"r":..,"g":..,"b":..} } ] },
  "categorySets": [ { "id": "<guid>", "name": "Colours" } ]
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
- **Journeys need a sync-enabled account.** `CreateTrip`/`AddTripStep` work on any account, but the trip only
  *persists* to the cloud on a sync-enabled account (same gate as a private sphere).
- **Keep category backgrounds pastel.** The default palette is pastel by design so paths + dark text read over
  it — *rename* categories rather than recolouring them strong; reserve full colours for path types.

## Concepts in one paragraph

A **sphere** holds **thorts** (notes) arranged into **groups** on the surface of a sphere; **paths** are typed
relationships between thorts and/or groups; **categories** colour thorts along a cross-cutting dimension; an
**arrangement** is a saved spatial layout, so the same content can be re-framed several ways non-destructively.
Spatial placement *is* meaning in Thortspace — put related things near each other and use relationships and
arrangements to express structure, rather than dumping a flat list.
