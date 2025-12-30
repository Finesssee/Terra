<h1 align="center">Terra AI</h1>

<p align="center">
  <strong>AI-powered NPC companions for Terraria via tModLoader</strong>
</p>

<p align="center">
  <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-28a745" alt="MIT License"></a>
  <a href="https://docs.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/C%23-12.0-239120?logo=csharp" alt="C# Version"></a>
  <a href="https://github.com/tModLoader/tModLoader"><img src="https://img.shields.io/badge/tModLoader-1.4.4+-purple?logo=terraria" alt="tModLoader"></a>
  <a href="https://x.com/Finessse377721"><img src="https://img.shields.io/badge/Follow-%F0%9D%95%8F%2F%40Finessse377721-1c9bf0" alt="Follow on 𝕏"></a>
  <a href="https://github.com/Finesssee/Terra"><img src="https://img.shields.io/github/stars/Finesssee/Terra.svg?style=social&label=Star%20this%20repo" alt="Star this repo"></a>
</p>

<p align="center">
  Spawn intelligent NPCs that understand natural language commands. Tell them to mine, build, fight, or follow you—powered by LLMs (Groq, OpenAI, Gemini).
</p>

<p align="center">
  <strong>Status:</strong> Early/experimental. Expect rough edges.
</p>

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Commands](#commands)
- [Configuration](#configuration)
- [Installation](#installation)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

---

## Features

- **Natural Language Commands** — Talk to NPCs using plain English via `/terra tell`
- **Action System** — Mining, digging, building, combat, pathfinding, and follow behaviors
- **Multi-Agent Coordination** — Multiple NPCs can collaborate on building tasks
- **Multiple LLM Providers** — Choose between Groq, OpenAI, or Gemini
- **In-Game UI** — Chat panel for real-time interaction
- **Persistent Memory** — NPCs remember context and world knowledge

---

## Requirements

| Requirement | Notes |
|-------------|-------|
| Terraria + tModLoader | Version 1.4.4 or higher |
| .NET 8 SDK | Only needed if building from source |
| API Key | One of: Groq, OpenAI, or Gemini |

---

## Quick Start

1. Install the mod in tModLoader
2. Configure your API key:
   **Settings → Mod Configuration → TerraAIMod**
3. In-game, open chat and run:

```
/terra spawn Bob
/terra tell Bob follow me
/terra tell Bob mine some copper
```

---

## Commands

| Command | Description |
|---------|-------------|
| `/terra spawn [name]` | Spawn a Terra NPC at your position |
| `/terra tell <name> <command>` | Give a natural language command |
| `/terra list` | List all active Terra NPCs |
| `/terra stop <name>` | Stop current action |
| `/terra remove <name>` | Remove a specific Terra NPC |
| `/terra clear` | Remove all Terra NPCs |

### Example Commands

```
/terra tell Bob dig down 50 blocks
/terra tell Bob build a small house here
/terra tell Bob attack nearby slimes
/terra tell Bob follow me and stay close
```

---

## Configuration

Access via **Settings → Mod Configuration → TerraAIMod**

### Provider Settings

| Option | Values | Description |
|--------|--------|-------------|
| `AIProvider` | `groq`, `openai`, `gemini` | Which LLM provider to use |
| `GroqApiKey` | string | API key for Groq |
| `OpenAIApiKey` | string | API key for OpenAI |
| `GeminiApiKey` | string | API key for Gemini |

### Model Settings

| Option | Default | Description |
|--------|---------|-------------|
| `OpenAIModel` | `gpt-4-turbo-preview` | Model to use for OpenAI |
| `MaxTokens` | varies | Maximum tokens per request |
| `Temperature` | varies | Response randomness (0-1) |

### Behavior Settings

| Option | Description |
|--------|-------------|
| `ActionTickDelay` | Ticks between action updates |
| `EnableChatResponses` | Toggle NPC chat responses |
| `MaxActiveTerras` | Maximum concurrent NPCs |

> **Note:** Config is client-side (`ConfigScope.ClientSide`). API keys are stored per-player and never synced to servers.

---

## Installation

### Option A: tModLoader (Recommended)

1. Clone the repository:
   ```bash
   git clone https://github.com/Finesssee/Terra.git
   ```

2. Copy `TerraAIMod/` to your ModSources folder:
   ```
   Documents/My Games/Terraria/tModLoader/ModSources/TerraAIMod/
   ```

3. In tModLoader:
   **Workshop → Develop Mods → Build + Reload**

### Option B: CLI Build

Build with `dotnet` by setting `tMLPath` to your tModLoader install:

**Windows (Steam):**
```cmd
set "tMLPath=C:\Program Files (x86)\Steam\steamapps\common\tModLoader"
dotnet build TerraAIMod/TerraAIMod.csproj
```

**Linux/macOS:**
```bash
export tMLPath="$HOME/.steam/steam/steamapps/common/tModLoader"
dotnet build TerraAIMod/TerraAIMod.csproj
```

Output `.tmod` file goes to:
```
Documents/My Games/Terraria/tModLoader/Mods/
```

---

## Project Structure

```
TerraAIMod/
├── TerraAIMod.cs          # Main mod entry point
├── AI/                    # LLM clients and prompt building
│   ├── ILLMClient.cs      # Provider interface
│   ├── GroqClient.cs
│   ├── OpenAIClient.cs
│   ├── GeminiClient.cs
│   ├── PromptBuilder.cs
│   ├── ResponseParser.cs
│   └── TaskPlanner.cs
├── Action/                # Action execution system
│   ├── ActionExecutor.cs
│   ├── ActionResult.cs
│   ├── Task.cs
│   ├── CollaborativeBuildManager.cs
│   └── Actions/           # Individual action implementations
│       ├── BaseAction.cs
│       ├── MineTileAction.cs
│       ├── DigAction.cs
│       ├── BuildStructureAction.cs
│       ├── PlaceTileAction.cs
│       ├── CombatAction.cs
│       ├── FollowPlayerAction.cs
│       ├── IdleFollowAction.cs
│       └── PathfindAction.cs
├── Commands/              # Chat command handlers
│   └── TerraCommand.cs
├── Config/                # Mod configuration
│   └── TerraConfig.cs
├── Memory/                # Context and world state
│   ├── TerraMemory.cs
│   └── WorldKnowledge.cs
├── NPCs/                  # NPC entity definition
│   └── TerraNPC.cs
├── Pathfinding/           # Navigation system
│   ├── TerrariaPathfinder.cs
│   ├── PathExecutor.cs
│   └── PathNode.cs
├── Players/               # Per-player state
│   └── TerraModPlayer.cs
├── Systems/               # tModLoader system hooks
│   ├── TerraSystem.cs
│   └── TerraManager.cs
└── UI/                    # In-game interface
    ├── TerraUIState.cs
    ├── ChatPanel.cs
    └── InputField.cs
```

---

## Troubleshooting

### "The type or namespace 'Terraria' could not be found"

You're missing `tMLPath`. Set it to your tModLoader installation directory before building.

### API key not working

- Verify the key is correct and has available credits
- Check that `AIProvider` matches the key you're using
- Look at tModLoader logs for error details

### NPC not responding to commands

- Ensure you spelled the NPC name correctly
- Check that the NPC is spawned with `/terra list`
- Verify your API key is configured

### Pathfinding issues

- NPCs cannot path through solid blocks without mining
- Complex terrain may cause navigation failures
- Try giving simpler, more direct commands

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -m 'Add your feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a Pull Request

---

## License

[MIT](LICENSE)

---

## Credits

- Original concept: [Steve AI](https://github.com/YuvDwi/Steve) by YuvDwi
- [tModLoader](https://github.com/tModLoader/tModLoader) team
- LLM providers: OpenAI, Groq, Google (Gemini)
