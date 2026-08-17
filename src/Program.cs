using System.Net;
using System.Text.Json;
using Thortspace.Headless;

namespace ThortspaceApiStarter;

// Thortspace API starter — drives Thortspace via the Thortspace.Headless NuGet package (the in-process
// command API): the engine runs IN your own process. No socket, no server, no installed app needed —
// you call the engine's methods and it creates/edits/saves spheres in the cloud account you log in as.
//
// What it does: takes a TOPIC (edit the constant below), fetches a reference page (Wikipedia by default),
// and builds a sphere out of it — groups of thorts, typed relationships, a colour scheme, TWO arrangements,
// and a guided JOURNEY — demonstrating (almost) the whole engine surface. Then saves it to the cloud.
//
//   Requirements:  Windows, .NET 8 SDK (or newer)
//   Set:           THORTSPACE_EMAIL, THORTSPACE_PASSWORD   (the account the sphere is created in)
//   Run:           dotnet run --project src
internal static class Program
{
    // ===================== EDIT ME =====================
    // The topic to build a sphere about (any Wikipedia article title works).
    private const string Topic = "Photosynthesis";

    // Where the content comes from. Wikipedia works out-of-the-box; GrokipediaContentSource is a stub
    // (grokipedia.com 403-blocks plain HTTP) you can implement with your own access.
    private static readonly IContentSource Source = new WikipediaContentSource();
    // ===================================================

    private static async Task<int> Main()
    {
        // Opt-in diagnostics: the engine logs failure detail (login errors, exceptions) via System.Diagnostics.Trace,
        // which has no listener by default. Set THORTSPACE_DEBUG=1 to route Trace → stderr so you can see WHY a login
        // or save failed. Off by default so a normal run stays quiet.
        if (Environment.GetEnvironmentVariable("THORTSPACE_DEBUG") == "1")
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error));

        try { return await Run(); }
        catch (Exception ex) { Console.Error.WriteLine("ERROR: " + ex); return 1; }
    }

    private static async Task<int> Run()
    {
        var (email, password) = ResolveCredentials();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("No credentials. Provide them either way:");
            Console.Error.WriteLine("  - env vars THORTSPACE_EMAIL + THORTSPACE_PASSWORD, or");
            Console.Error.WriteLine("  - a credentials.json {\"email\":\"...\",\"password\":\"...\"} at THORTSPACE_CREDENTIALS,");
            Console.Error.WriteLine("    or beside the project (gitignored), or in %LOCALAPPDATA%\\ThortspaceMcp (shared with the MCP server).");
            return 2;
        }

        // ---- 0. Fetch the source material for the topic. ----
        Console.WriteLine($"Fetching \"{Topic}\" …");
        var page = await Source.FetchAsync(Topic);
        Console.WriteLine($"  \"{page.Title}\" — {page.Sections.Count} sections.");

        // ---- 1. Stand up the engine + log in. ----
        var engine = new HeadlessEngine(Path.Combine(Path.GetTempPath(), "ThortspaceApiStarter"));
        if (await engine.LoginAsync(email, password) != HttpStatusCode.OK) { Console.Error.WriteLine("Login failed."); return 3; }
        Console.WriteLine("Logged in.");

        // ---- 2. Create a fresh PUBLIC sphere (saves on any account tier) and open it as the editable session. ----
        var (createCode, localId, cloudId) = await engine.CreateSphereAsync($"{page.Title} — Thortspace API starter", isPublic: true);
        if (createCode != HttpStatusCode.OK) { Console.Error.WriteLine($"Create failed: {createCode}"); return 4; }
        await engine.OpenSphereAsync(localId);                                  // create does NOT open — do it explicitly
        var initialArrangement = ArrangementId(engine);
        Console.WriteLine($"Created sphere (cloudId={cloudId}).");

        // ---- 3. A GROUP per section; thorts inside form Thortspace's hex lattice. ----
        var groups = new List<(string Heading, Guid Group, List<Guid> Thorts)>();
        foreach (var section in page.Sections)
        {
            var thortIds = new List<Guid>();
            var first = engine.AddThort(section.Thorts[0]);                     // new thort -> its own new group
            var groupId = engine.GroupOfThort(first)!.Value;                    // find that group
            thortIds.Add(first);
            foreach (var line in section.Thorts.Skip(1))
                thortIds.Add(engine.AddThort(line, groupId: groupId));          // add into the group (hex default)
            engine.RenameGroup(groupId, section.Heading);
            engine.ArrangeGroup(groupId, "hex");                               // explicit: tidy into the hex lattice
            groups.Add((section.Heading, groupId, thortIds));
        }
        Console.WriteLine($"Built {groups.Count} groups, {groups.Sum(g => g.Thorts.Count)} thorts.");

        // ---- 4. Typed RELATIONSHIPS: chain the sections (group -> group). 'arrange' will untangle it. ----
        for (var i = 0; i < groups.Count - 1; i++)
            engine.Connect(groups[i].Group, groups[i + 1].Group, i == 0 ? "introduces" : "then");
        // Give the chain relationship a strong, full colour — full colours belong on PATHS (thort backgrounds stay pastel).
        engine.RecolourPathType("then", 45, 107, 255);

        // ---- 5. CATEGORIES (a cross-cutting COLOUR dimension). Thortspace's default palette is deliberately
        //         PASTEL (so paths + dark text read over it), so we RENAME the existing colours rather than recolour
        //         them. (Meaningful categorisation is a semantic job — the AI/MCP path does it best; here we use a
        //         simple structural scheme to show the API.)
        var cats = CategoryIds(engine);
        if (cats.Count >= 3)
        {
            engine.RenameCategory(Guid.Parse(cats[0]), "Overview");
            engine.RenameCategory(Guid.Parse(cats[1]), "Key point");
            engine.RenameCategory(Guid.Parse(cats[2]), "Detail");
            foreach (var (heading, _, thortIds) in groups)
                for (var i = 0; i < thortIds.Count; i++)
                    engine.SetThortCategory(thortIds[i], heading == "Overview" ? "Overview" : i == 0 ? "Key point" : "Detail");
        }

        // ---- 5b. GROUPS take categories too — the colour lands on the group's boundary circle and its name plate,
        //          so a cluster's meaning reads when you are zoomed out and no thort text is legible yet. This is a
        //          SEPARATE dimension from the thort colours above: here the thort colour says what a thought IS
        //          (Overview / Key point / Detail) while the group colour says where the cluster sits in the
        //          narrative — two layers you can read at two distances. Colouring a group does NOT colour the
        //          thorts inside it, which is what makes the two-layer read possible.
        if (cats.Count >= 6 && groups.Count >= 2)
        {
            engine.RenameCategory(Guid.Parse(cats[3]), "Opening");
            engine.RenameCategory(Guid.Parse(cats[4]), "Body");
            engine.RenameCategory(Guid.Parse(cats[5]), "Close");
            for (var i = 0; i < groups.Count; i++)
                engine.SetGroupCategory(groups[i].Group,
                    i == 0 ? "Opening" : i == groups.Count - 1 ? "Close" : "Body");
            // Pass "none" to put a group back on the theme's group colour — a group's default, and the only other
            // state it has (unlike a thort, which falls back to the set's default category).
            //   engine.SetGroupCategory(groups[0].Group, "none");
        }

        // ---- 6. LAY IT OUT. 'arrange' clusters related groups + reduces path crossings (the chain -> a clean arc). ----
        engine.Arrange(null, null, null, reduceCrossings: true);

        // ---- 7. A SECOND ARRANGEMENT — the same thorts, reframed. Here: a spread-out "wide view". ----
        var wide = engine.CreateArrangement("Wide view");
        engine.SwitchArrangement(wide);
        engine.Arrange(null, null, "spread", reduceCrossings: true);
        engine.SwitchArrangement(Guid.Parse(initialArrangement));              // back to the main arrangement
        Console.WriteLine("Arranged; added a 'Wide view' arrangement.");

        // ---- 8. A guided JOURNEY (Present-mode trip): a step per section, then a focus-less overview step. ----
        var tripId = engine.CreateTrip($"{page.Title} — a guided tour");
        foreach (var (heading, group, thortIds) in groups)
        {
            var narration = thortIds.Count > 0 ? $"{heading}." : heading;
            engine.AddTripStep(tripId, narration, arrangementId: null, focusGroupId: group.ToString(),
                focusThortId: null, name: heading, framing: "group");
        }
        // A whole-picture step with NO focus, in the Wide arrangement — the engine aims the camera at the content
        // centroid (so an overview step never faces the empty far side of the sphere).
        engine.AddTripStep(tripId, "Step back and see the whole landscape.", arrangementId: wide.ToString(),
            focusGroupId: null, focusThortId: null, name: "The whole picture", framing: "overview");
        Console.WriteLine($"Authored a {((JsonElement)JsonSerializer.SerializeToElement(engine.GetTrip(tripId))).GetProperty("steps").GetArrayLength()}-step journey.");

        // ---- 9. A LINKED companion sphere + a CROSS-SPHERE journey step. Thortspace spheres can be LINKED into a
        //         neighbourhood, and a single journey can SPAN linked spheres. Here we build a small companion sphere
        //         B (an index of the topic), link A <-> B, then add a final journey step that shows B as the neighbour.
        var (bCode, bLocalId, bCloudId) = await engine.CreateSphereAsync($"{page.Title} — in brief", isPublic: true);
        if (bCode == HttpStatusCode.OK)
        {
            await engine.OpenSphereAsync(bLocalId);                              // build on the companion sphere
            // Group 1: a one-line "in brief" from the lead. Group 2: the chapter headings as an index.
            var briefFirst = engine.AddThort(page.Sections[0].Thorts[0]);
            var briefGroup = engine.GroupOfThort(briefFirst)!.Value;
            foreach (var line in page.Sections[0].Thorts.Skip(1).Take(2)) engine.AddThort(line, groupId: briefGroup);
            engine.RenameGroup(briefGroup, "In brief");
            var chaptersFirst = engine.AddThort(page.Sections[0].Heading);
            var chaptersGroup = engine.GroupOfThort(chaptersFirst)!.Value;
            foreach (var s in page.Sections.Skip(1)) engine.AddThort(s.Heading, groupId: chaptersGroup);
            engine.RenameGroup(chaptersGroup, "Chapters");
            engine.Connect(briefGroup, chaptersGroup, "leads-to");
            engine.Arrange(null, null, null, reduceCrossings: true);
            var saveB = await engine.SaveAsync();
            Console.WriteLine($"Built + saved companion sphere (cloudId={bCloudId}): {saveB}.");

            await engine.OpenSphereAsync(localId);                              // back to the main sphere (A)
            var link = await engine.LinkSphereAsync(bLocalId);                  // bidirectional A <-> B neighbourhood link
            Console.WriteLine($"Linked the two spheres: {link.code} (linked id {link.otherLocalId}).");
            // A cross-sphere journey step: shows B in the neighbourhood view (networkSphereId), so the SAME journey
            // travels from A's content out to its linked companion. Sets neighbourhood framing automatically.
            engine.AddTripStep(tripId, "And here's the companion sphere — the topic in brief, linked alongside.",
                arrangementId: null, focusGroupId: null, focusThortId: null, name: "The linked companion",
                framing: "neighbourhood", networkSphereId: bLocalId);
            Console.WriteLine("Added a cross-sphere journey step spanning both spheres.");
        }
        else Console.WriteLine($"Skipped the linked companion sphere (create returned {bCode}).");

        // ---- 10. Save the main sphere to the cloud (with its now-cross-sphere journey). ----
        var save = await engine.SaveAsync();
        Console.WriteLine($"Save: {save}");
        if (save != HttpStatusCode.OK)
        {
            Console.Error.WriteLine("Save rejected — a PRIVATE sphere on a free account is frozen; this demo uses a " +
                                    "PUBLIC sphere, which saves on any account. Check the account/login.");
            return 5;
        }

        // ---- 11. Compose a meta-analysis from OTHER PEOPLE'S public spheres (the insight-graph move). ----
        // Search the shared/public library, EMBED the results into a fresh hub sphere (a one-way reference — works
        // for public spheres you don't own), and CONNECT them with pathsteps. In Thortspace, two embedded spheres
        // joined by a pathstep form a navigable "virtual network" — so this builds a meta-analysis across other
        // authors' insight. (search_spheres scope: "mine" = own + shared-with-you, "public", or "all".)
        Console.WriteLine();
        Console.WriteLine("Composing a meta-analysis from public spheres on this topic...");
        using (var sdoc = JsonDocument.Parse(JsonSerializer.Serialize(await engine.SearchSpheresAsync(page.Title, "public", 6))))
        {
            var results = sdoc.RootElement.GetProperty("results");
            if (results.GetArrayLength() >= 2)
            {
                var (hubCode, hubLocal, hubCloud) = await engine.CreateSphereAsync($"{page.Title} — a meta-analysis (public spheres)", isPublic: true);
                if (hubCode == HttpStatusCode.OK)
                {
                    await engine.OpenSphereAsync(hubLocal);
                    var portals = new List<string>();
                    foreach (var r in results.EnumerateArray())
                    {
                        var cid = r.GetProperty("cloudId").GetString();
                        var title = r.TryGetProperty("title", out var t) ? t.GetString() : cid;
                        var (embedCode, portal) = await engine.EmbedSphereAsync(cid!);   // one-way embed of a public sphere
                        if (embedCode == HttpStatusCode.OK && portal != null) { portals.Add(portal); Console.WriteLine($"  embedded '{title}' ({cid})"); }
                    }
                    for (int i = 0; i + 1 < portals.Count; i++)                            // chain them with pathsteps -> virtual network
                        engine.Connect(Guid.Parse(portals[i]), Guid.Parse(portals[i + 1]), "relates-to");
                    var hubSave = await engine.SaveAsync();
                    Console.WriteLine($"  meta-analysis hub {hubCloud} saved: {hubSave} — {portals.Count} embedded spheres, {Math.Max(0, portals.Count - 1)} pathsteps.");
                    await engine.OpenSphereAsync(localId);                                 // back to the main sphere
                }
            }
            else Console.WriteLine("  (public search returned fewer than 2 spheres; skipping the meta-analysis demo).");
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Open Thortspace (or thort.space) on this account to see sphere {cloudId}.");
        Console.WriteLine("In Present mode you can PLAY the journey. NOTE: saving/playing your own journeys needs a");
        Console.WriteLine("sync-enabled account (Premium / Subscriber / Trial / paid-org) — the same gate as private");
        Console.WriteLine("spheres. On a free account the sphere + content save (it's public) but the journey stays local.");

        // ---- Other engine operations you can explore (all on IAgentEngine / HeadlessEngine) ----
        //   Content:   SetThortText, DeleteThort, Disconnect, MoveThort, MoveGroup, Coagulate, Relayout
        //   Categories: AddCategory, RecolourCategory(+optional text colour), RemoveCategory, AddCategorySet,
        //               SetDefaultCategory, RenameCategorySet, RemoveCategorySet
        //   Path types: RenamePathType, ReorderPathTypes
        //   Arrangements: RenameArrangement, DeleteArrangement, ReorderArrangements
        //   Sphere:    RenameSphere, SetSpherePublic, ListSpheres, Snapshot, LinkSphereAsync (two-way link — shown above),
        //              SearchSpheresAsync (own/shared/public — shown above), EmbedSphereAsync (one-way embed — shown above)
        //   Journeys:  RenameTrip, SetTripPublic, EditTripStep, DeleteTripStep, ReorderTripSteps, DeleteTrip
        //   (In-app only — these need the running desktop app, not the headless engine: NavigateTo, SetWorkingMode, PlayTrip.)
        return 0;
    }

    // Credentials come from (1) THORTSPACE_EMAIL / THORTSPACE_PASSWORD env vars, or (2) a credentials.json file —
    // {"email":"...","password":"..."} — at THORTSPACE_CREDENTIALS, else beside the project (gitignored), else the
    // SAME %LOCALAPPDATA%\ThortspaceMcp\credentials.json the standalone MCP server (Thortspace.Mcp.exe) reads, so one
    // file serves both headless paths. Nothing is read from inside the repo. Plaintext on disk → use a dedicated /
    // TEST account (the file's contents are never printed; only the chosen path is logged).
    private static (string? email, string? password) ResolveCredentials()
    {
        var email = Environment.GetEnvironmentVariable("THORTSPACE_EMAIL");
        var password = Environment.GetEnvironmentVariable("THORTSPACE_PASSWORD");
        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password)) return (email, password);

        foreach (var path in CredentialFilePaths())
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                if (string.IsNullOrEmpty(email) && root.TryGetProperty("email", out var em)) email = em.GetString();
                if (string.IsNullOrEmpty(password) && root.TryGetProperty("password", out var pw)) password = pw.GetString();
                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                {
                    Console.WriteLine($"Using credentials file: {path}");
                    return (email, password);
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"Could not read {path}: {ex.Message}"); }
        }
        return (email, password);
    }

    // Where to look for a credentials.json, in order. THORTSPACE_CREDENTIALS wins; then a file beside the project /
    // built exe (keep it gitignored); finally the standalone MCP server's file, so the two headless stacks share one.
    private static IEnumerable<string> CredentialFilePaths()
    {
        yield return Environment.GetEnvironmentVariable("THORTSPACE_CREDENTIALS");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");
        yield return Path.Combine(AppContext.BaseDirectory, "credentials.json");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThortspaceMcp", "credentials.json");
    }

    // Snapshot() returns an anonymous object; serialise + read the bits we need.
    private static string ArrangementId(IAgentEngine engine)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(engine.Snapshot()));
        return doc.RootElement.GetProperty("arrangementId").GetString()!;
    }

    private static List<string> CategoryIds(IAgentEngine engine)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(engine.Snapshot()));
        var list = new List<string>();
        if (doc.RootElement.TryGetProperty("categorySet", out var cs) && cs.ValueKind == JsonValueKind.Object
            && cs.TryGetProperty("categories", out var categories))
            foreach (var c in categories.EnumerateArray())
                if (c.TryGetProperty("id", out var id)) list.Add(id.GetString()!);
        return list;
    }
}
