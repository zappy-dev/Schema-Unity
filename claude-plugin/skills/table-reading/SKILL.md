---
name: table-reading
description: Read Schema table data from Unity game code correctly and cheaply -- initialize once, fetch a table or row through the generated wrapper, survive a missing row or table, and keep lookups out of per-frame code. Use when game code reads design data from a table, when a lookup throws InvalidOperationException ("This result failed", "Entry not found"), when a table or screen is empty, or the log says "No Scheme ... loaded", only in a build or on the first frame, when one mistyped ID from a designer takes down a spawn or a whole screen, when reading table values allocates or shows in the profiler, or when a player build of a project using the package fails to compile with UnityEditor errors in Schema.Runtime.
metadata:
  kind: runbook
---

# Reading Schema tables from game code

**Every lookup returns a result struct, and `.Result` on a failed one throws.** Initialize once, read each
table once at load into plain objects, and treat a missing row as a normal outcome.

Describes package `com.devzappy.schema.unity` 0.3.x, checked at 0.3.1 plus commit `e9089e0`.
**ran** = executed against that commit by a project using Schema (2026-10-12); **read** = read in the package
source at that commit. Problems named here are open at that pin: check the package changelog first.
Background: [Runtime Guide](https://github.com/zappy-dev/Schema-Unity/blob/v0.3.1/docs/RuntimeGuide.md). The
generated wrappers come from [[table-authoring]].

## 1. Initialize once, before the first read

```csharp
var init = SchemaRuntime.Initialize();          // using Schema.Runtime; loads the Manifest and every table in it
if (init.Failed) { Debug.LogError(init.Message); return; }
```

Call it from one early entry point. Each call reloads every table, adds another `ManifestUpdated` handler and
logs the name of every text asset (read), so it is not a per-scene or per-object call. Anything that reads before it
runs gets a failed result, not an exception: `XScheme.Get()` says `No Scheme 'X' loaded.` and the screen simply
comes up empty (ran). Order the bootstrap first (`[DefaultExecutionOrder(-1000)]` on its component), or read in
a method the bootstrap calls. The editor may already hold the tables, so this order bug can hide there (unverified).

**Check:** play from a cold start; no `No Scheme` line, and a known row reads back.

## 2. Read a row with `Try`, not `.Result`

```csharp
if (!ShipsScheme.GetEntry(id).Try(out var ship))             // Ids.X for a row the code depends on
{
    Debug.LogWarning($"No ship '{id}' in the Ships table");   // a normal outcome: log, skip, carry on
    return null;
}
return new ShipStats(ship.Name, ship.Speed);
```

- `GetEntry` for an absent id is a quiet failed result. `.Result` on it throws
  `InvalidOperationException: This result failed, reason: Entry not found` (ran), so one mistyped id from a designer
  can take down a spawn loop or a screen. `Try(out v)` is true when it passed; `Try(out v, out err)` also hands
  you the failed result; `TryErr(out v, out err)` is true when it **failed**, the opposite way round (read).
- `GetEntries()` with no arguments returns an empty sequence if the table is not loaded, with no log (read).
  `GetEntries(context)` returns a result you can check.
- A column missing from a row's data reads as `0`, `""` or `false`, not an error (ran). A wrapper that is
  out of step with its table therefore fails silently: publish again ([[table-authoring]]).
- Code that depends on a row names it `XScheme.Ids.NAME`, so renaming or deleting the row fails to compile.
  Code that tolerates a missing row keeps a plain string.

**Check:** a deliberate bad id logs one warning and the rest of the loop still runs.

## 3. Cache at load; never look up per frame

`GetEntry` and `GetEntries` scan the table with LINQ and allocate on every call (read: `SchemeWrapper.GetEntryById`
filters all entries; ran: reads showed collections in a heap counter). Build what the game needs once and keep it:

```csharp
foreach (var e in PortsScheme.Get().Result.GetEntries())      // once, at load
    ports[e.ID] = new Port(e.Name, e.Fee);                    // a plain object, a Dictionary
```

Read the tables when the run, scene or level loads, hold plain structs or classes, and let hot code
(`Update`, spawn, hit, pickup) touch only those. Reference-column properties cost about 120 times a plain
read and allocate (ran): see [[column-types]].

**Check:** the Profiler shows no Schema call under `Update`; GC Alloc for it is 0 B.

## 4. Content is read-only at runtime

The runtime loads the published JSON from `Resources`. To change a value: edit the table, Publish, rebuild the
player. There is no write path from a build.

## When it fails

| You see | Cause | Do |
|---|---|---|
| `No Scheme 'X' loaded.`, empty screen, only on first frame or in a build | read before `Initialize()` | step 1 |
| `InvalidOperationException ... Entry not found` | `.Result` after a lookup of an id that is not there | step 2 |
| `InvalidOperationException ... Color not found`, or on a link column's property | a typed property of a Color or Reference column that is blank or bad | [[column-types]] |
| a table loads in the editor and not in a build | the table is missing from the published Manifest | [[table-authoring]], "A new table" |
| `CS1061` naming an `...Entry` class | the property is not published yet | [[table-authoring]] |
| player build fails to compile in `Schema.Runtime`, errors naming `UnityEditor` | the 0.3.1 release imports `UnityEditor` in runtime code; fixed in commit `e9089e0`, unreleased at this pin (read) | depend on that commit or later: `https://github.com/zappy-dev/Schema-Unity.git?path=/SchemaPlayground/Packages/com.devzappy.schema.unity#e9089e0` |
| a Color or Unity Asset column fails to load in a stripped player | the plugin types are found by assembly-qualified name; stripping may remove them (unverified; the Mono development player loaded them, ran) | keep `Schema.Runtime` types with a `link.xml` and test the real player |

## Related

[[table-authoring]], [[column-types]]
