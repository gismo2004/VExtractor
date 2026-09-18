# VExtractor

VExtractor builds the **controller catalog** that the
[OptoV integration](https://github.com/gismo2004/optov) for Home Assistant needs.

The catalog is a single file describing your heating controller: every value it can report or
accept, what it is called and what its settings mean. OptoV cannot talk to a controller without
one, and the catalog is not distributed, so this is the first thing to do.

You run this tool once. It asks two questions and hands you a file. Ten minutes, most of it
downloading.

> **Why you have to build it yourself.** The information comes from Viessmann's own service
> software. Building it on your own machine, from your own copy, for your own controller is a
> different thing from publishing the result, so the tool is shared and its output is not.

---

## Before you start

**1. The service software installer.** It is called `Vitosoft300SID1_Setup.exe`, about 400 MB,
and Viessmann offers a free 90-day trial version on their connectivity pages. Download it and
remember where it landed, usually your Downloads folder.

You do **not** have to install it. VExtractor only reads the file; nothing from it is run, and
nothing is changed on your machine. If the service software is already installed on your
Windows machine, VExtractor uses that and you do not need the installer at all.

**2. The .NET 8 runtime.** VExtractor is built on it. Most Windows machines already have it.
How to get it is covered per system below.

That is all. The release archive brings everything else along, including a copy of 7-Zip's
console program, which VExtractor uses to open the installer.

---

## Windows

**Get the .NET 8 runtime**, if you do not have it:
[download it here](https://dotnet.microsoft.com/download/dotnet/8.0), under *Run desktop apps*,
the x64 installer. Run it and click through. If you are not sure whether you already have it,
skip this: `VExtractor.bat` checks and opens that page for you if it is missing.

**Get VExtractor.** From the
[releases page](https://github.com/gismo2004/VExtractor/releases), download the file ending in
`win-x64.zip`. Right-click it, choose *Extract All*, and pick or create an empty folder, for
example `Documents\VExtractor`. It holds `VExtractor.exe`, `VExtractor.bat`, `7za.exe` and a
licence file; keep them together.

**Put the installer next to them.** Move or copy `Vitosoft300SID1_Setup.exe` into that same
folder.

**Run it.** Double-click **`VExtractor.bat`**. A black console window opens, checks that
everything is in place, tells you if something is missing and what that means, and starts the
guided run. If the .NET runtime is missing it opens the download page for you.

> Windows may show **"Windows protected your PC"**. That appears for any program without a paid
> code-signing certificate. Click *More info*, then *Run anyway*. If you would rather not, you
> can build the tool yourself from this repository instead.

Answer the two questions below, and when it says `Done:` the catalog is in the same folder,
named something like `catalog-2048.db`. Press Enter to close the window.

That file is what you upload to Home Assistant when you add the OptoV integration.

---

## Linux and macOS

**Open a terminal** and install the .NET 8 runtime. Pick the line for your system:

```bash
# Ubuntu 24.04 and newer
sudo apt update && sudo apt install -y dotnet-runtime-8.0

# Fedora
sudo dnf install -y dotnet-runtime-8.0

# Arch
sudo pacman -S --needed dotnet-runtime
```

On **Debian** the .NET runtime is not in the standard repositories. Add Microsoft's first:

```bash
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-runtime-8.0
```

**Get VExtractor and run it.** Adjust the version in the first line to the current release, and
the path in the third line to wherever your installer download landed:

```bash
mkdir -p ~/vextractor && cd ~/vextractor
curl -L -O https://github.com/gismo2004/VExtractor/releases/download/v1.0.0/VExtractor-v1.0.0-linux-x64.tar.gz
tar -xzf VExtractor-v1.0.0-linux-x64.tar.gz
cp ~/Downloads/Vitosoft300SID1_Setup.exe .
./vextractor.sh
```

The script checks that everything is in place, tells you if something is missing and what
that means, and starts the guided run. The archive also holds `7zz`, 7-Zip's console program,
which VExtractor uses to open the installer.

On a Raspberry Pi or another ARM machine, use the `linux-arm64` file instead. macOS works the
same way with `osx-arm64` (Apple silicon) or `osx-x64` (Intel), and `brew install dotnet@8`
for the runtime.

Answer the two questions below. When it says `Done:` the catalog is in `~/vextractor`, named
something like `catalog-2048.db`.

---

## Docker

If you have Docker, nothing needs installing at all. In the folder that holds
`Vitosoft300SID1_Setup.exe`:

```bash
docker run --rm -it -v "$PWD:/data" ghcr.io/gismo2004/vextractor
```

The image carries VExtractor, the .NET runtime and 7-Zip, nothing else; your folder is mounted
as `/data`, the installer is read from there and the catalog written there. It is the same
guided run as above, with the same two questions, for `amd64` and `arm64` (Raspberry Pi,
Apple silicon). On Linux add `--user "$(id -u):$(id -g)"` before `-v` so that the files it
writes belong to you rather than to root; Docker Desktop on Windows and macOS does that by
itself. Each release publishes an image with the release's tag, and `latest` is the newest.

To run it without any questions, for example from a script, answer them as environment
variables: `VEXTRACTOR_ID` is the controller (a system id such as `2048`, several separated by
commas, `all`, or a name that matches exactly one system id) and `VEXTRACTOR_LANG` the
languages (default `de,en`). An existing catalog of the same name is overwritten.

```bash
docker run --rm -e VEXTRACTOR_ID=2048 -e VEXTRACTOR_LANG=de,en -v "$PWD:/data" ghcr.io/gismo2004/vextractor
```

The same two variables work for the native program as well.

---

## The two questions

### Languages

```
Languages [de,en]:
```

Which languages the names and texts should be in. Press **Enter** to accept German and English,
which is what most people want: German is what the controller's own manual uses, and English is
there as a fallback. You choose which of them Home Assistant displays later, in the
integration's options.

If you want another, type two-letter codes separated by commas, for example `fr,en`. Available:
de, en, fr, it, es, nl, pl, da, sv, cs, ru, tr, hu, hr, no, sk, ro, lt.

### Which controller

```
253 controllers are described. Which one is yours?
Controller (Enter to quit):
```

Type **part of the name printed on your unit** and press Enter. `Vitocal`, `Vitodens`, `WO1A`,
whatever you can read off the front or the type plate. You get a list like this:

```
2048
    V200WO1A                 Vitocal-G mit Vitotronic 200 (Typ WO1A) (ab 08/2010)
2049
    VBC702_AW                Vitocal 3xx mit Vitotronic 200 (Typ WO1A) (ab 08/2010)
```

The four-character code on the left, `2048` here, is the **system id**. Type the one that
matches your unit and press Enter. That is the last question. If you already know the system
id, you can type it straight away.

**Not sure which line is yours?** Two ways out. Type `all` and it builds a catalog covering
every controller, named `catalog.db`; it works exactly the same, the file is just much larger
and slower to load. Or pick your best guess: if it is wrong, OptoV refuses to start and tells you
the system id it actually found, and you run VExtractor again with that one.

Several controllers can share one system id. Building for it covers all of them, and OptoV
works out which one it is really talking to when it starts.

---

## What the files in the folder are for

After a run the folder holds a good deal more than you started with. Only one file goes to Home
Assistant.

| File | What to do with it |
|---|---|
| `catalog-2048.db` (or `catalog.db` after `all`) | **Upload this to Home Assistant** when you add OptoV, or later with *Reconfigure* on the OptoV entry. Keep a copy. |
| `VExtractor.bat` / `vextractor.sh`, `VExtractor.exe` / `VExtractor`, `7za.exe` / `7zz` | The program, its launcher and the bundled 7-Zip. Keep them together if you want to run it again. |
| `Vitosoft300SID1_Setup.exe` | The installer. Only needed for the first run, or after you deleted the working files. |
| Everything else | Working files. They let a second run skip the slow part. Safe to delete. |

## Running it a second time

Because of those working files, a second run skips straight to the controller question and
takes seconds. Useful for building a catalog for another controller, for adding a language, or
after an OptoV update asks for a rebuilt catalog.

The working files take a few hundred megabytes. Once you have your catalog you can delete
everything in the folder except your catalog and the program files from the table above; the
next run then simply needs the installer again.

## If something goes wrong

**"You must install .NET to run this application"** — step 2 above was skipped. Install the
runtime and run VExtractor again.

**"7-Zip was not found"** — `7za.exe` (or `7zz`) is not next to the program, usually because only
the executable was taken out of the archive. Extract the whole archive into one folder and run
it again.

**"No such file"** or it asks for a path — it could not find the installer. The simplest fix is
to put `Vitosoft300SID1_Setup.exe` in the same folder as the program. Otherwise type the full
path when asked, for example `C:\Users\you\Downloads\Vitosoft300SID1_Setup.exe`.

**"Not in this installer"** — the file is not the service software installer, or it is a
partial download. Check the size: it should be around 400 MB.

**"nothing matches"** when searching for a controller — try something shorter. `Vito` finds
plenty; a full model number with spaces and slashes usually finds nothing.

**OptoV asks you to rebuild the catalog** — the integration was updated and needs a catalog in a
newer format. Download the current VExtractor release and run it again; it takes a minute.

**The window closes instantly on Windows** — start it through `VExtractor.bat`, which keeps the
window open until you press a key, so the message stays on screen.

---

## Scripting and building from source

`VExtractor help` shows the command-line form, for running it without questions; the container
takes the same arguments after the image name. To build the executable yourself from a
checkout, run `./build.sh linux-x64` (or `win-x64`, `linux-arm64`, `osx-arm64`); it uses the
.NET 8 SDK, or Docker if the SDK is not installed, and puts the result in `build/bin/`. The
image is built from the `Dockerfile` with `docker build -t vextractor .`.

## How this was written

VExtractor was written together with AI coding assistants, Claude, Gemini and GPT among them,
with a person directing the work, making the design decisions, and testing the result against a
real controller. Some lines were typed by a human and many were drafted by a model; treat it as
you would any other code, read it before you trust it, and report what is wrong.

## Licence

MIT, see [LICENSE](LICENSE). The licence covers this tool. It does not cover what the tool
reads or what it produces: the service software belongs to its manufacturer, and the catalog
you build from it is yours to use and not something this project distributes.

The release archives include 7-Zip's console program (`7za.exe` or `7zz`), unchanged, from
[7-zip.org](https://www.7-zip.org). 7-Zip is copyright Igor Pavlov and licensed under the GNU
LGPL; its licence ships alongside as `7-Zip-License.txt`.

## Legal

This tool reads files you already have, from software you downloaded from the manufacturer, on
your own machine. It ships no vendor data of any kind, and neither its output nor anything
derived from it is distributed here. The manufacturer's names are used only to say which
equipment and which software are meant. Nothing here is endorsed by or affiliated with the
manufacturer.
