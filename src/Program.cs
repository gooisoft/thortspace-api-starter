using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ThortspaceApiStarter;

// Thortspace API starter — drives Thortspace directly by referencing Thortspace.Headless.dll (the in-process
// command API) and running the engine IN your own process. No socket, no server, no network layer: you call the
// engine's methods and it creates/edits/saves spheres in the cloud account you log in as.
//
// What it does: logs in, CREATES A FRESH SPHERE (so it never touches anything you already have open), builds a
// little graph in it (thorts -> typed paths -> group -> layout), and saves it to the cloud.
//
//   Requirements:  Windows, .NET 8 SDK, and an installed Thortspace that provides the SDK DLLs (see README).
//   Set:           THORTSPACE_EMAIL, THORTSPACE_PASSWORD   (the account the sphere is created in)
//   Optional:      THORTSPACE_SDK_DIR                      (folder with Thortspace.Headless.dll + dependencies)
//   Run:           dotnet run --project src
internal static class Program
{
    // Where the Thortspace SDK DLLs live. Defaults to a standard Windows install; override via env var.
    private static readonly string SdkDir =
        Environment.GetEnvironmentVariable("THORTSPACE_SDK_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThortspaceX64", "current");

    private static async Task<int> Main()
    {
        // Resolve Thortspace.Headless.dll + its dependency DLLs from the SDK folder at runtime, so this starter can
        // live anywhere. Registered BEFORE any Thortspace type is touched — the engine work is in Run() (never
        // inlined), so its assembly only loads after this handler is in place.
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            var dll = Path.Combine(SdkDir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(dll) ? Assembly.LoadFrom(dll) : null;
        };

        try
        {
            return await Run();
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine("Could not load the Thortspace SDK: " + ex.Message);
            Console.Error.WriteLine($"Set THORTSPACE_SDK_DIR to the folder containing Thortspace.Headless.dll (looked in: {SdkDir}).");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex);
            return 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> Run()
    {
        var email = Environment.GetEnvironmentVariable("THORTSPACE_EMAIL");
        var password = Environment.GetEnvironmentVariable("THORTSPACE_PASSWORD");
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Set THORTSPACE_EMAIL and THORTSPACE_PASSWORD (the account to create the sphere in).");
            return 2;
        }

        // Stand up an in-process engine. The cache dir holds this client's own install id + local state.
        var cacheDir = Path.Combine(Path.GetTempPath(), "ThortspaceApiStarter");
        var engine = new Thortspace.Headless.HeadlessEngine(cacheDir);

        var login = await engine.LoginAsync(email, password);
        if (login != HttpStatusCode.OK) { Console.Error.WriteLine($"Login failed: {login}"); return 3; }
        Console.WriteLine("Logged in.");

        // 1. Create a FRESH sphere and open it as the editable session. public:true so it cloud-saves on any
        //    account tier (a PRIVATE sphere on a free account is frozen — it needs a sync-enabled account).
        var title = "Thortspace API starter — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var (createCode, localId, cloudId) = await engine.CreateSphereAsync(title, isPublic: true);
        if (createCode != HttpStatusCode.OK) { Console.Error.WriteLine($"Create failed: {createCode}"); return 4; }
        await engine.OpenSphereAsync(localId);
        Console.WriteLine($"Created sphere (cloudId={cloudId}).");

        // 2. Build a little graph: three thorts joined by typed paths, gathered into a group, then laid out.
        //    A leading # highlights a key word. Place ideas in context; here we chain them.
        var confusion = engine.AddThort("#Confusion — the tangle we start with");
        var breakItDown = engine.AddThort("Break it into smaller parts");
        var insight = engine.AddThort("#Insight — the part that clicks");
        engine.Connect(confusion, breakItDown, "leads-to");
        engine.Connect(breakItDown, insight, "leads-to");
        engine.CreateGroup(new[] { confusion, breakItDown, insight });
        engine.Relayout();
        Console.WriteLine("Added 3 thorts, connected them, grouped, and laid out.");

        // 3. Save to the cloud.
        var save = await engine.SaveAsync();
        Console.WriteLine($"Save: {save}");
        if (save != HttpStatusCode.OK)
        {
            Console.Error.WriteLine("Save was rejected. (A private sphere on a free account is frozen; this demo " +
                                    "uses a public sphere, which saves on any account — check the account/login.)");
            return 5;
        }

        Console.WriteLine($"Done. Open Thortspace (or thort.space) to see sphere {cloudId}.");
        return 0;
    }
}
