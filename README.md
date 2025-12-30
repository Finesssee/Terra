# Terra AI (tModLoader)

AI companions for Terraria. Spawn Terra NPCs and give them natural language tasks like mining, building, combat, and following.

> Status: early/experimental. Expect rough edges.

## Features

- Natural language commands via in-game chat (`/terra tell ...`)
- Action planner + executor (mining, digging, building, combat, follow)
- Multi-agent coordination for shared building tasks
- Supports multiple LLM providers: **Groq**, **OpenAI**, **Gemini**

## Requirements

- Terraria with **tModLoader 1.4.4+**
- **.NET 8 SDK** (only needed if building from source)
- An API key for one provider:
  - Groq
  - OpenAI
  - Gemini

## Quickstart

1. Install/enable the mod in tModLoader.
2. Set your API key in tModLoader:
   - **Settings** → **Mod Configuration** → `TerraAIMod`
3. In a world, open chat and run:

```
/terra spawn Bob
/terra tell Bob follow me
/terra tell Bob mine some copper
```

## Commands

```
/terra spawn [name]           Spawn a Terra NPC at your position
/terra tell <name> <command>  Give a natural language command
/terra list                   List active Terras
/terra stop <name>            Stop current action
/terra remove <name>          Remove a specific Terra
/terra clear                  Remove all Terras
```

## Configuration

tModLoader: **Settings** → **Mod Configuration** → `TerraAIMod`.

### Provider + API keys

- `AIProvider`: `groq`, `openai`, or `gemini`
- `GroqApiKey`, `OpenAIApiKey`, `GeminiApiKey`: set the one matching `AIProvider`

Notes:
- Config is **client-side** (`ConfigScope.ClientSide`) so keys stay per-player.
- Don’t commit keys to git.

### Model/tuning

- `OpenAIModel`: e.g. `gpt-4-turbo-preview`, `gpt-4`, `gpt-3.5-turbo`
- `MaxTokens`, `Temperature`

### Behavior

- `ActionTickDelay`: ticks between action updates
- `EnableChatResponses`
- `MaxActiveTerras`

## Installation

### Option A: Build inside tModLoader (recommended)

1. Clone this repo:

```bash
git clone https://github.com/Finesssee/Terra.git
```

2. Copy `TerraAIMod/` into your tModLoader ModSources folder:

- Windows default:
  - `Documents/My Games/Terraria/tModLoader/ModSources/TerraAIMod/`

3. Open tModLoader → **Workshop** → **Develop Mods** (or **Mod Sources**) → **Build + Reload**.

### Option B: CLI build (`dotnet`)

You can build directly with `dotnet` as long as `tMLPath` points to your tModLoader install (needed for Terraria/tModLoader references).

Example (Windows, Steam):

```bat
set "tMLPath=D:\SteamLibrary\steamapps\common\tModLoader"
dotnet build TerraAIMod/TerraAIMod.csproj
```

The output `.tmod` should end up in:
- `Documents/My Games/Terraria/tModLoader/Mods/`

## Development

- Main mod source lives in `TerraAIMod/`.
- If you see compile errors like `The type or namespace name 'Terraria' could not be found`, you’re missing `tMLPath` or building outside of tModLoader.

## Project Layout

```
TerraAIMod/
├── NPCs/            # TerraNPC entity
├── Commands/        # /terra chat commands
├── Systems/         # TerraManager, TerraSystem (tML hooks)
├── Players/         # per-player state
├── AI/              # LLM clients + prompt building
├── Action/          # action executor + actions
│   └── Actions/     # mine/build/combat/etc
├── Memory/          # conversation + world knowledge
├── Pathfinding/     # pathfinding
├── UI/              # chat panel UI
└── Config/          # ModConfig definitions
```

## Ported From

Port of Steve AI for Minecraft: https://github.com/YuvDwi/Steve

## License

MIT

## Credits

- Original Steve AI by YuvDwi
- tModLoader team
- OpenAI / Groq / Google (Gemini) APIs
