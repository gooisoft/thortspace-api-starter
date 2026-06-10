# Driving Thortspace with an AI over MCP

This is the **no-code** way to drive Thortspace: point an MCP-capable AI (Claude Code, Claude
Desktop/Cowork, Gemini CLI, or any MCP host) at Thortspace and just *talk* to it. The AI calls
self-describing tools; you describe what you want built.

It's the same engine as the [code path](API.md) — `Thortspace.Headless`. The tool layer
(`ThortspaceTools`) is a thin projection of the engine command vocabulary, and **both MCP hosts expose
the byte-identical tool set**, so everything in the [Tool reference](#tool-reference) below works the same
way whichever host you use.

## Two MCP hosts, one tool set

| Host | What it drives | Transport | When to use |
|---|---|---|---|
| **Standalone** `Thortspace.Mcp.exe` | a **headless** engine in its own process — builds & saves spheres with no GUI | **stdio** | unattended production; let an AI build spheres into your account |
| **In-app** (the running desktop app) | the sphere you have **open**, live — you watch edits land in real time | **loopback HTTP** | co-creating live; teaching/demoing; editing an open sphere |

Both are part of an installed Thortspace (**version 1.6.718 or later** — the DLL debuted in 1.6.717).
There's nothing to build.

> **One conceptual model for everything below.** A *sphere* holds *thorts* (short ideas) gathered into
> *groups* on its surface, joined by typed *paths* (relationships). *Categories* are a cross-cutting
> colour dimension, independent of groups. *Arrangements* are alternate saved layouts of the same content.
> Spatial placement **is** meaning — related things sit near each other. The AI receives this model as the
> MCP `instructions` on connect, so you don't have to explain it.

---

## (b) Standalone stdio host — headless production

`Thortspace.Mcp.exe` lives in the `mcp\` folder of an installed Thortspace. An AI client launches it over
stdio; it logs in once at startup and builds spheres into that account.

**Register it** (example for Claude Code):

```powershell
claude mcp add thortspace `
  -e THORTSPACE_EMAIL=you@example.com -e THORTSPACE_PASSWORD=... `
  -- "%LOCALAPPDATA%\ThortspaceX64\current\mcp\Thortspace.Mcp.exe"
```

- **Gemini CLI:** add a stdio MCP server whose command is that exe path, with the same two env vars.
- **Claude Desktop / Cowork:** Settings → Developer / MCP → add a local (stdio) server pointing at the exe.

It authenticates from `THORTSPACE_EMAIL` / `THORTSPACE_PASSWORD` (set in your client config, as above) or a
`credentials.json` (`%LOCALAPPDATA%\ThortspaceMcp\credentials.json` by default — the **same file the
[code starter](../README.md) reads**). There is **no `login` MCP tool**, so the AI never sees your
credentials.

**Demo recipe — say this:**

> *"Use Thortspace to build a public sphere about the water cycle: a few groups of thorts, connect related
> ideas with typed relationships, colour them by theme, tidy the layout, add a short guided journey, and
> save it."*

The AI will `create_sphere` (public, so it saves on any account) → `open_sphere` → `add_thorts` into
groups → `connect` them → name + apply categories → `arrange` → `create_trip` + `add_trip_step` → `save`.
Open the sphere in Thortspace (or on thort.space) afterwards; in **Present mode** you can play the journey.

> Saving a **public** sphere works on any account tier. A **private** sphere — and *persisting* a journey —
> needs a sync-enabled account (Premium / Subscriber / Trial / paid-org member); otherwise the build
> succeeds locally but `save` is rejected (frozen). Ask for a *public* sphere on a free account.

---

## (c) In-app HTTP host — edit the sphere you have open, live

Turn it on in the desktop app: **User Settings → AI Connection** (on). The app hosts an MCP server at
`http://127.0.0.1:8787/mcp`. Open the sphere you want the AI to work on, then point your AI at the URL —
the **same URL for every client**, only the registration syntax differs:

```powershell
# Claude Code
claude mcp add --transport http thortspace http://127.0.0.1:8787/mcp
```

```jsonc
// Gemini CLI — an HTTP MCP server
{ "thortspace": { "httpUrl": "http://127.0.0.1:8787/mcp" } }
```

Any streamable-HTTP-capable MCP host works. The connection is **loopback-only** (127.0.0.1) and **off by
default** — the app behaves identically until you switch it on. Your AI runs on your machine with your own
AI account; Thortspace never sees your AI key.

**Demo recipe — say this** (with a sphere open):

> *"Look at the sphere I have open, then add a group of three risks, connect each to the idea it threatens,
> colour them as 'Risk', and fly to the first one so I can see it."*

The AI will `snapshot` (to see what's there) → `add_thorts` → `connect` → name a category + `set_categories`
→ `navigate_to`. You watch each step happen live. Tools that move the live camera/UI — `navigate_to`,
`set_working_mode`, `play_trip` — are **in-app only**.

> For driving the *live* app you need an HTTP-capable MCP client (Claude Code, Gemini CLI). Clients that
> only do stdio (some desktop apps) use the standalone host above.

---

## Let the AI find the workflow itself: `cookbook`

For any multi-step task, the AI can call the **`cookbook`** tool first — it returns proven step-by-step
recipes (build from a topic, summarise, categorise, reframe into a new arrangement, bring groups together,
tidy the layout, build a guided journey, span a journey across linked spheres). You don't have to spell out
the steps; just name the task and a well-behaved client will pull the recipe.

---

## Tool reference

55 tools, all on the shared `ThortspaceTools` layer (identical in both hosts). The AI reads these
descriptions from `tools/list` at connect time — this table is the human-readable mirror. Tools marked
**(in-app)** only do something in the in-app HTTP host (they move the live camera/UI); on the standalone
host they are inert.

> **The golden rule the AI follows:** call `snapshot` first, then use **only** the ids it returns when
> targeting a thort / group / category / arrangement.

### Connection & inspection
| Tool | What it does |
|---|---|
| `ping` | Check the connection is alive (returns *pong*). |
| `cookbook` | Return step-by-step recipes for common multi-step tasks. |
| `snapshot` | The current sphere as JSON: thorts, groups, paths, arrangements, the category set, and the arrangement `radius`. **Call first.** |
| `list_spheres` | List the account's spheres (their ids). |

### Spheres
| Tool | What it does |
|---|---|
| `create_sphere(name, isPublic=false)` | Create a sphere and make it current. Public ⇒ saves on any tier. |
| `open_sphere(sphereId)` | Open an existing sphere as the current editable session. |
| `rename_sphere(name)` | Rename the current sphere (no `/`). |
| `set_sphere_public(isPublic)` | Publish/unpublish; publishing also enables saving for a free account. |
| `link_sphere(sphereId, nearGroupId?)` | Bidirectional **neighbourhood link** to another sphere (build a connected *set*; a journey can span both). |
| `save()` | Save the current sphere to the cloud. |

### Thorts
| Tool | What it does |
|---|---|
| `add_thort(text, groupId?, nearThortId?, nearGroupId?, link?, placement?)` | Add one idea, in context. |
| `add_thorts(texts[], groupId?, placement?)` | Add many in one call (use for batches). |
| `set_thort_text(thortId, text)` | Edit a thort's text. |
| `delete_thort(thortId)` | Delete a thort (and its paths). |
| `move_thort(thortId, groupId?, x?, y?)` | Move into a group and/or reposition within its group. |

### Groups
| Tool | What it does |
|---|---|
| `create_group(thortIds[], placement?)` | Gather existing thorts into a new group. |
| `move_group(groupId, x, y, z)` | Move a group; (x,y,z) is a direction projected onto the sphere. |
| `rename_group(groupId, name)` | Set a group's label. |

### Paths (typed relationships)
| Tool | What it does |
|---|---|
| `connect(from, to, relationship)` | Typed path between two nodes (thort or group); e.g. *supports*, *causes*, *leads-to*. |
| `disconnect(from, to)` | Remove the path between two nodes. |
| `rename_path_type(pathType, name)` | Rename a relationship type everywhere it's used. |
| `recolour_path_type(pathType, r, g, b)` | Recolour every path of a type (paths carry the strong/full colours). |
| `reorder_path_types(pathTypes[])` | Reorder the path-type palette (permutation of all current). |

### Categories (the cross-cutting colour dimension)
| Tool | What it does |
|---|---|
| `set_category(thortId, category)` | Apply a category (name or id) to a thort. |
| `set_categories(thortIds[], category)` | Apply one category to many thorts (fast). |
| `add_category(name?, r?, g?, b?)` | Create a new category (prefer renaming the existing pastel palette). |
| `rename_category(categoryId, name)` | Give a starting unnamed colour meaning (e.g. 'Risk'). |
| `recolour_category(categoryId, r, g, b, textR?, textG?, textB?)` | Recolour a category background (**keep pastel**; pass whitish text if dark). |
| `remove_category(categoryId)` | Remove a category. |
| `set_default_category(categoryId)` | Make a category the default in its set. |

### Category sets (whole colouring dimensions)
| Tool | What it does |
|---|---|
| `add_category_set(name)` | Add a separate colouring dimension. |
| `rename_category_set(categorySetId, name)` | Rename a set. |
| `remove_category_set(categorySetId)` | Remove a set (not the last one). |

### Arrangements (alternate saved layouts)
| Tool | What it does |
|---|---|
| `create_arrangement(name)` | Copy the current layout as a new view to rearrange independently (non-destructive reframe). |
| `switch_arrangement(arrangementId)` | Choose which arrangement subsequent edits/snapshots target. |
| `rename_arrangement(arrangementId, name)` | Rename a view. |
| `delete_arrangement(arrangementId)` | Delete a view (not the last one). |
| `reorder_arrangements(arrangementIds[])` | Reorder the view-switcher (permutation of all current). |

### Layout
| Tool | What it does |
|---|---|
| `relayout()` | Auto-layout: coagulate thorts into hex lattices **and** spread groups apart. |
| `coagulate()` | Coagulate only: tidy each group into a hex lattice, keep groups close. |
| `arrange_group(groupId, formation="hex")` | Shape one group: hex (signature) / line / ring / square / freeform. |
| `arrange(scope?, style?, spacing?, reduceCrossings=true)` | Smart tidy: reduce **path crossings**, cluster related groups; detects chain/star/tree/cycle. Call after building connected structure. |

### Journeys (Present-mode "trips" — authoring works everywhere; **playing is in-app**)
| Tool | What it does |
|---|---|
| `create_trip(name)` | Create a guided journey; returns its id. |
| `add_trip_step(tripId, description, arrangementId?, focusGroupId?, focusThortId?, name?, framing?, networkSphereId?, networkArrangementId?)` | Add a viewpoint: one arrangement + a focus + narration; `framing` sets zoom; `networkSphereId` makes it span a linked sphere. |
| `list_trips()` | List journeys (id, name, step count). |
| `get_trip(tripId)` | A journey's steps in order. |
| `delete_trip(tripId)` | Delete a journey. |
| `play_trip(tripId)` **(in-app)** | Play the journey in Present mode. |
| `rename_trip(tripId, name)` | Rename a journey. |
| `set_trip_public(tripId, isPublic)` | Publish/unpublish a journey. |
| `edit_trip_step(tripId, stepIndex, ...)` | Edit one step in place (0-based index from `get_trip`). |
| `delete_trip_step(tripId, stepIndex)` | Delete one step. |
| `reorder_trip_steps(tripId, order[])` | Reorder steps (permutation of current indices). |

### Presentational navigation **(in-app only)**
| Tool | What it does |
|---|---|
| `navigate_to(nodeId)` | Bring a thort/group front-and-centre in the live app. |
| `set_working_mode(mode)` | Switch mode: think / do / explore / present / archive / none. |

---

## Result shape

Every tool returns JSON the model can read: `{"ok":true,"result":{...}}` on success, or
`{"ok":false,"error":{...}}` on failure (e.g. a `save` on a frozen private sphere returns an `error` that
explains how to fix it — make it public, or use a sync-enabled account).

## See also

- **[README](../README.md)** — the runnable code starter and the standalone-host quick start.
- **[API.md](API.md)** — the in-process `IAgentEngine` reference for driving the engine from C# directly.
- **[thort.space → Connect Your AI](https://thort.space/overlay/ajax/docs/connect-ai)** — the maintained
  end-user setup page for the in-app host.
