<p align="center">
  <img src="docs/logo.png" alt="" width="104" height="104">
</p>

<h1 align="center">Picky</h1>

<p align="center">
  <strong>Choose which browser opens each link.</strong><br>
  A Choosy-style browser chooser for Windows — in a single 60&nbsp;KB executable.
</p>

<p align="center">
  <img src="docs/picker.png" alt="The Picky picker, listing browser profiles for an incoming link" width="420">
</p>

<p align="center">
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <img alt=".NET Framework 4.8" src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4">
  <img alt="No dependencies" src="https://img.shields.io/badge/dependencies-none-success">
  <img alt="MIT" src="https://img.shields.io/badge/license-MIT-blue">
</p>

---

Windows lets you pick **one** default browser. Picky replaces that single choice
with a decision made per link: it registers itself as a browser, so Windows hands
it every link you click, and it either routes the link silently using a rule or
shows a picker at your cursor.

Click a work link, it opens in your work profile. Click anything else, you choose.

## Features

- **Per-profile targets** — Chrome and Edge profiles are separate destinations, not just "Chrome"
- **Rules** — host patterns route silently, so the links you always know about never interrupt you
- **Picker** — appears at the cursor; click, or press `1`–`9`
- **Reorderable** — drag to set the order, which becomes the number-key order
- **Keyboard-first** — arrows and `Enter`, `Esc` to cancel, `Shift` while clicking to force the picker
- **Nothing resident** — no tray icon, no startup entry, no background process
- **No installer, no dependencies** — one exe, ~60 KB

## Screenshots

<p align="center">
  <img src="docs/settings.png" alt="Picky settings: status, browser list, and rules" width="722">
</p>

## Install

Download `picky.exe` from [Releases](../../releases), or build it yourself (below).

1. Run `picky.exe` — the settings window opens
2. Click **Register** — writes to `HKCU` only, no administrator rights needed
3. Click **Set as default** and assign **http** and **https** to Picky

That last step is manual by design. Windows protects the default-browser setting
with a per-user hash specifically so that no program can reassign it silently.
Any tool that claims to do it for you is forging that hash, and Windows reverts it.

**Unregister** removes every key Picky added.

## Usage

| Action | Result |
|---|---|
| Click a link | A matching rule routes it silently; otherwise the picker appears |
| `1` – `9` | Open in that entry |
| `↑` `↓` then `Enter` | Move the selection and open |
| `Esc`, or click away | Dismiss without opening |
| **Shift** while clicking a link | Force the picker even when a rule matches |

### Rules

Rules match the **hostname**, first match wins. Add them in the settings window,
or edit `%APPDATA%\Picky\config.json`:

```json
{
  "rules": [
    { "pattern": "*.company.com", "targetId": "microsoftedge|Profile 1" },
    { "pattern": "github.com",    "targetId": "googlechrome|Default" }
  ],
  "order": ["microsoftedge|Default", "microsoftedge|Profile 1"],
  "showPrivate": true
}
```

Patterns are case-insensitive and anchored, so `github.com` does **not** match
`evil-github.com`. `*` is the only wildcard. A rule pointing at a browser or
profile that no longer exists falls through to the picker rather than silently
doing nothing.

## Build

```powershell
.\build.ps1
```

No SDK and no NuGet. It compiles with the C# compiler that ships in Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) against .NET
Framework 4.8, which is present on every Windows 10 and 11 install.

That compiler is the **legacy** one — C# 5 only. No string interpolation, no
null-conditional operators, no `nameof`. Source is kept pure ASCII (non-ASCII
characters are written as `\uXXXX` escapes) because it reads BOM-less files using
the system ANSI code page.

| Script | Purpose |
|---|---|
| `build.ps1` | Compiles `bin\picky.exe` |
| `tools\make-icon.ps1` | Generates `assets\picky.ico` from code — no binary source asset |
| `tools\make-screenshots.ps1` | Renders the README images from the live app in `--demo` mode |

## How it works

"Default browser" on Windows means a program registered as the handler for the
`http` and `https` protocols. Nothing requires that program to be a browser.

Picky registers itself as one, so its handler command is simply:

```
"picky.exe" "%1"
```

Windows launches it with the URL as an argument. It resolves your browsers and
profiles, applies your rules, shows the picker if needed, launches the real
browser, and exits. Nothing stays in memory.

### Profile detection

Chromium profiles are read from `<User Data>\Local State` → `profile.info_cache`.
The display label is the first non-empty of `shortcut_name`, `gaia_name`, `name`,
then the directory key.

That order matters. `shortcut_name` holds the real label you see in the browser
("Personal", "Work"), while `name` is frequently a meaningless `Profile N` that
**disagrees with the directory it lives in** — a profile in the `Default`
directory routinely reports `name: "Profile 1"`. Tools that read `name` show a
scrambled list. Launch arguments always use the directory key, never the label.

Supported: Edge, Chrome, Brave, Vivaldi (profiles plus private mode), and Firefox
(default plus private).

## Debugging

- `picky.exe --keep <url>` — show the picker without dismissing it on focus loss
- `picky.exe --demo` — placeholder profiles and sample rules; never writes config
- Unhandled errors append to `%APPDATA%\Picky\error.log`

## License

MIT
