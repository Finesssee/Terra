# Terra AI

AI companions for Terraria. Port of the Steve AI Minecraft mod to tModLoader.

## What It Does

Terra AI creates NPC companions that respond to natural language commands. Describe what you want, and they figure out how to do it - mining, building, combat, following, exploration.

The interface is the in-game chat. Type `/terra tell Bob mine some copper` and Bob reasons about where copper spawns, navigates there, and starts mining. Ask for a house and it considers available materials, generates a structure, and builds it tile by tile.

**Capabilities:**
- **Resource extraction** - agents determine optimal mining locations and strategies
- **Autonomous building** - planning layouts and material usage
- **Combat and defense** - assess threats and coordinate responses
- **Exploration and gathering** - pathfinding and resource location
- **Collaborative execution** - multiple Terras coordinate on large tasks

## How It Works

Each Terra runs an agent loop:

1. Command goes to an LLM (Groq, OpenAI, or Gemini)
2. LLM breaks down request into structured actions
3. Actions execute using Terraria's game mechanics
4. If something fails, the agent replans

## Multi-Agent Coordination

When multiple Terras work on the same structure:
- Automatically split into sections
- Each takes a part
- No conflicts when placing tiles
- Rebalance work if someone finishes early

Coordination happens through `TerraManager` which tracks active builds and assigns work.

## Requirements

- **Terraria** with tModLoader 1.4.4+
- **.NET 8.0 SDK** (for building from source)
- **API Key** for one of: OpenAI, Groq, or Gemini

## Installation

### From Release
1. Download the `.tmod` file from releases
2. Place in `Documents/My Games/Terraria/tModLoader/Mods/`
3. Enable in tModLoader mod menu
4. Configure API key (see Configuration below)

### From Source
```bash
git clone <repo-url>
cd terraria/TerraAIMod
dotnet build
```

Output goes to your tModLoader mods folder.

## Configuration

Create a config file or use the in-game Mod Configuration menu.

Required settings:
- `ApiKey` - Your LLM provider API key
- `Provider` - One of: `openai`, `groq`, `gemini`
- `Model` - Model name (e.g., `gpt-3.5-turbo`, `llama-3.1-70b-versatile`)

## Commands

```
/terra spawn [name]           Spawn a Terra NPC at your position
/terra tell <name> <command>  Give natural language command
/terra list                   List all active Terras
/terra stop <name>            Stop current action
/terra remove <name>          Remove a specific Terra
/terra clear                  Remove all Terras
```

## Usage Examples

```
/terra spawn Bob
/terra tell Bob mine 20 copper ore
/terra tell Bob build a house near me
/terra tell Bob follow me
/terra tell Bob attack that zombie
/terra tell Bob dig down 50 tiles
/terra tell Bob gather wood from that forest
```

Terras understand context. You don't need to be super specific.

## Architecture

```
TerraAIMod/
├── NPCs/            # TerraNPC entity class
├── AI/              # LLM clients (OpenAI, Groq, Gemini), prompt building
├── Action/          # Action classes for mine, build, combat, etc
│   └── Actions/     # Individual action implementations
├── Memory/          # Context management and world state
├── Pathfinding/     # A* pathfinder adapted for Terraria
├── Systems/         # TerraManager, TerraSystem (tModLoader hooks)
├── Commands/        # Chat commands (/terra)
├── Config/          # Mod configuration
├── UI/              # Chat panel UI
└── Players/         # TerraModPlayer (per-player state)
```

## Known Issues

- **LLM quality matters** - GPT-3.5 works but GPT-4/Claude are better at complex planning
- **Actions are synchronous** - Terra can only do one thing at a time
- **Memory resets on world exit** - Context only persists during play session

## Ported From

This is a port of [Steve AI for Minecraft](https://github.com/YuvDwi/Steve) - same concept, adapted for Terraria's 2D tile-based world.

Key differences from Minecraft version:
- 2D pathfinding instead of 3D
- Tile-based building instead of block-based
- Terraria-specific ore locations and biomes
- tModLoader mod system instead of Forge

## License

MIT

## Credits

- Original Steve AI by YuvDwi
- OpenAI, Groq, Google for LLM APIs
- tModLoader team for the modding framework
