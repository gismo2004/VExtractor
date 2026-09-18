# CLAUDE.md

Guidance for Claude Code (or any agent) working in this repository.

## What this is

**VExtractor**, the tool that compiles the controller catalog which the
[OptoV](https://github.com/gismo2004/optov) Home Assistant integration reads. It takes the
controller service software's own installer and produces one SQLite file describing every
controller reachable over the Optolink optical port: datapoints, addresses, byte layouts,
scaling, enumerations, fault texts, menu structure and weekly programmes.

The catalog is **not distributed**. Each user runs this tool on their own copy of the service
software. This repository ships the compiler, never its output. MIT licensed, copyright
gismo2004; the README says openly that it was written together with AI assistants, and that
sentence stays.

## Layout

```
VExtractor.bat          what a Windows user double-clicks: checks for the .NET runtime, which the
vextractor.sh           program cannot report missing itself, starts the guided run, keeps the
                        window open. The .sh is the same for Linux and macOS. Both ship in the
                        archives. Everything else (7-Zip, installer) the guided run checks and explains
build.sh                compiles the executable from a checkout into build/bin/<runtime>/, with
                        the host .NET SDK or the SDK container when there is none
VExtractor/
  Program.cs            entry point: no arguments runs Guided, otherwise subcommands
  Guided.cs             the interactive run: find a source, prepare, ask controller, build
  InstallerReader.cs    finds the archive inside the installer and reads the wanted files out
  SourceLoader.cs       the installer's serialized dataset -> a local SQLite source database
  SqliteExporter.cs     source database -> the compiled catalog. The schema lives here
  Models/DataBase/      the data model the source is read through
  Models/Results/       shapes the exporter passes around
  Helper/               shared parsing helpers
.github/workflows/      release.yml publishes one archive per platform on a version tag
build/                  everything the build produces, git-ignored
work/                   git-ignored playground: notes, probes, scratch data, throwaway scripts
```

The deliverable for users is the executable from a release: framework-dependent, about 20 MB.
The only prerequisite is the .NET 8 runtime, documented per platform in the README. The release
archive also carries 7-Zip's console program (`7za.exe` on Windows, `7zz` elsewhere) and its
LGPL licence: the workflow fetches a pinned 7-Zip release by checksum at build time and puts the
binary beside ours, unchanged. Nothing is downloaded when the tool runs. `InstallerReader` looks
for 7-Zip beside the executable first, then on the PATH, and stops with an explanation when there
is none.

The same tag publishes `ghcr.io/gismo2004/vextractor` (amd64 and arm64) from the `Dockerfile`:
the .NET runtime image, Debian's `7zip` package (which provides `7zz`), and the program
published as a plain directory rather than the single file the archives use, because that file
unpacks itself into a home directory a container user does not have. `VEXTRACTOR_HOME=/data`
makes the guided run treat the mounted folder as home (it otherwise prefers "beside the
program", which in the image is `/app`), and `VEXTRACTOR_LAUNCHER=1` skips the closing pause.
`VEXTRACTOR_ID` and `VEXTRACTOR_LANG` answer the two questions so the run needs no terminal
(added for the container on a user's request, `Guided.ResolveIds`/`ParseLanguages`); the
interactive path is unchanged and the output is overwritten without asking in that mode.
Tested end to end on 2026-09-17: the container built the same 1,308-datapoint WO1A catalog as the
native run. `build.sh` still uses the SDK container only as a stand-in for a missing SDK.

`work/docs/` holds the working notes: `DATABASE.md` on the source data, `DESIGN.md` on decisions
already made, `STATUS.md` as the running log. They are the record of what was measured and what
was rejected; read them before re-deciding something. Nothing outside `work/` may reference
anything inside it.

The integration lives in its own repository, next to this one. A change that spans both -- and
the ones that matter usually do, because the point of this tool is to keep vendor specifics out
of the integration -- is made in both and verified together.

## Ground rules

**No catalog is committed, compiled or not, under any name.** It is derived from the vendor's
parameter definitions, so it is built locally and never enters the repository or its history.

**Comments explain behaviour and rationale, never history.** Say what the data means and why
the code responds that way. No "we discovered", no war stories, no names of tools that happened
to be on the desk when a fact was established; that narrative belongs in `work/docs/`, which is
not published. Source table and column names are unavoidable here, since this tool reads them,
and that is fine.

**Vendor naming stops here.** This is the boundary: naming inside the source is this tool's
business, and what the catalog hands to the integration is finished data. When the integration
would otherwise have to know how the source names something, add a column here that carries the
resolved value. `ScheduleLevelStems` is the worked example: the integration used to build the
source's text keys itself, and now reads a finished stem out of `datapoint_defs`.

**The build is deterministic.** The same installer and the same languages produce the same file,
byte for byte. Nothing that varies between two runs may enter the catalog: no build timestamp,
no machine name, no unordered iteration. That property is how a change to the exporter is
checked, by rebuilding and comparing.

**No hardware, network or deployment specifics.** No IP addresses, API keys, hostnames or
personal paths anywhere outside `work/`.

## The catalog's structure version

`SqliteExporter.CatalogSchemaVersion` is written into the catalog's `catalog_meta` table, and the
integration carries the number it needs in `catalog_db.CATALOG_SCHEMA_VERSION`. The two are
checked against each other whenever a catalog is uploaded and whenever a config entry starts. A
catalog newer than the integration is refused with a message to update the integration. The
integration also carries a minimum it still reads (`CATALOG_SCHEMA_MIN`): a catalog between the
minimum and the current number runs, minus whatever the catalog gained since, and raises a repair
saying a rebuild is due; one below the minimum fails setup.

Raise this number together with the integration's, in the same change, whenever the catalog
gains something the integration reads, so that older catalogs are reported as behind. The
integration's minimum moves only when an older catalog would be wrong or unreadable: a table or
column the reader now needs, a changed meaning for an existing one, a different key format.

## Building

Started without arguments the program runs `Guided`: it looks for a source in a fixed order --
the installed service software on Windows, files beside the program, the installer beside the
program, a typed path -- prepares what is missing, asks for languages and the controller, and
writes the catalog beside itself. Every step it performs is one of the subcommands below, so
anything it does can be reproduced by hand. It writes only beside the program (or in the current
directory if that is not writable) and never anywhere else.

```bash
./build.sh                                    # the executable -> build/bin/linux-x64/
x=build/bin/linux-x64/VExtractor s=build/source
export VEXTRACTOR_SOURCE_DIR=$s VEXTRACTOR_SOURCE_DB=$s/source.db
$x prepare /path/to/Setup.exe $s de,en        # definitions + texts out of the installer
$x load $s/DPDefinitions.xml $s/source.db     # definitions -> source database, once
$x devices                                    # controllers and their system ids
$x sqlite build/catalog.db 2048 de,en         # one controller; "all" for every one
```

Give `sqlite` its languages explicitly: without them it takes every language the installer has.

No database server and no installation: the definitions are read straight out of the installer,
loaded into a local SQLite file, and exported from that. Everything lands in `build/`.

**Where the time goes**, measured on the full installer. `prepare` is about 15 s, once: the
archive is one solid LZMA2 stream (425 MB packed, 3.2 GB unpacked) that cannot be entered in the
middle, and the wanted files start 1.6 GB in. 7-Zip stops after the last one, so asking for
fewer files saves nothing, and decoding runs on about one core. .NET has no 7z support in
any version, there is no maintained package that ships native 7-Zip for all three platforms, and
py7zr cannot read BCJ2 at all, which is why 7-Zip's own binary is bundled and required. A managed
decoder was ten times slower and only mattered when the bundled binary had been left behind, so
there is no fallback. Loading the definitions into `source.db` is about 18 s, once. After that,
on a 16-thread machine: `devices` takes 2.2 s, one controller 5 s, all 253 11 s. Of that, about
1.5 s is Entity Framework starting up on the first query, 1.6 s reading the source tables (side
by side), and the controller loop 0.6 s for one controller and about 4 s for all, most of it
writing rows and decoding definitions. Set
`VEXTRACTOR_PROFILE=1` to get a per-section breakdown of the controller loop.

An installed copy of the software with its SQL Server database also works, without `prepare`:
point `VEXTRACTOR_CONNSTR` at the server. That path is not the supported one and is not what the
README describes.

## Things that are easy to get wrong

- **Load the source tables once, look up in memory.** The controller loop used to query the
  source per controller, and each query scanned a table of up to a quarter of a million rows
  with no index, 253 times. Loading every table once and using dictionaries took the loop from
  47 s to 9 s with byte-identical output. Do not put an EF query back inside the loop, and do
  not scan a whole table inside it either: group links and display conditions are looked up by
  group (`groupLinksByGroup`, `conditionsByGroup`), which took those two sections from 3.5 s to
  1.2 s on a full build. Where the order of rows matters, the lookup keeps table order.
- **One context per thread.** The nine tables are read side by side, each through its own
  `VDataBase`, because a context cannot be shared between threads. The source database takes
  concurrent readers, and the output does not change: every list keeps the order its table stores.
- **The data model maps only the tables that are read.** It used to map all 156 tables of the
  vendor database, and building that model cost 3 s on every start. Adding a query on another
  table means adding its entity and its configuration back to `VDataBase`.
- **Each language file is parsed once per run.** `TextsFor` keeps the result; the controller list,
  the error codes and the translations all need it, and one parse is about half a second.
- **Decode each definition once.** Extension values are serialized .NET objects and
  deserializing them is the most expensive thing done per datapoint. `decodedByPk` keeps the
  result per event type; a definition shared by thirty controllers is decoded once.
- **Prepared statements, via `Bind()`.** A command created per row is re-prepared per row.
  Commands are created once before the loop and only their parameter values change.
- **`dotnet run` rebuilds every time.** In a container that was 18 s per invocation, most of it a
  package restore. Compile once with `build.sh` and call the executable; it starts in a second.
- **A subset build must carry the definitions its rules point at.** A display condition on
  one controller often tests a datapoint that only a sibling family exposes, and the reader
  resolves that by the foreign definition's label. The exporter therefore writes every
  `condition_event_type_id` no exported controller carries, after the controller loop. When
  checking that a one-controller build matches the full one, compare under real probe values;
  with none, the rules never fire and the difference is invisible.
- **Inputs are opened read-only.** `FileMode.Open` alone asks for write access and fails on a
  read-only mount. Nothing this tool reads is ever written to.
- **7-Zip needs `-r` for bare names.** Without it a name only matches at the archive root,
  the run succeeds, and nothing comes out. `ExtractNative` checks what actually landed.
- **The SDK container runs as the calling user.** Otherwise everything it writes under
  `build/` and `obj/` is owned by root and the next host-side step cannot touch it.
- **A double quote inside a C# verbatim string must be doubled.** The schema is one long `@"..."`
  and a quotation mark in a comment inside it ends the string, producing hundreds of unrelated
  errors far away from the real edit.
- **Views are not loaded by the data model.** `AlignWithModel` skips entity types with no table.
- **The source and the model drift by the odd column.** `AlignWithModel` adds what is missing as
  empty, with a NOT NULL default where the model reads a value type, so a definition file from a
  slightly different release still loads.
- **Menu-tier ties.** Where two menu branches claim the same datapoint, the tie is broken with
  `string.CompareOrdinal` so the result does not depend on dictionary order.
- **Write-ahead logging is for the build only.** The finished catalog is switched back to
  `DELETE` before `VACUUM`; a WAL catalog makes every reader create a `-wal` and a `-shm` beside
  it in the user's configuration directory, and cannot be read from a read-only mount at all.

## Verifying a change to the exporter

Rebuild and compare against the previous catalog at the SQL level, not by eye:

```bash
python3 - <<'PY'
import hashlib, sqlite3
def digest(p):
    c = sqlite3.connect(f"file:{p}?mode=ro", uri=True)
    h = hashlib.sha256()
    for line in c.iterdump():
        h.update(line.encode())
    c.close()
    return h.hexdigest()
print(digest("old.db"))
print(digest("build/catalog.db"))
PY
```

A change meant to add something should differ only in that something. A change meant to change
nothing should produce an identical digest.
