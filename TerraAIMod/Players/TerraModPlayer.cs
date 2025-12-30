using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TerraAIMod.Systems;

namespace TerraAIMod.Players
{
    /// <summary>
    /// Handles keybind registration for the Terra AI mod.
    /// </summary>
    public class TerraKeybinds : ModSystem
    {
        /// <summary>
        /// Keybind for toggling the Terra AI GUI panel.
        /// </summary>
        public static ModKeybind ToggleGUI { get; private set; }

        /// <summary>
        /// Registers keybinds when the mod system loads.
        /// </summary>
        public override void Load()
        {
            ToggleGUI = KeybindLoader.RegisterKeybind(Mod, "Toggle Terra AI Panel", "K");
        }

        /// <summary>
        /// Cleans up keybind references when the mod system unloads.
        /// </summary>
        public override void Unload()
        {
            ToggleGUI = null;
        }
    }

    /// <summary>
    /// Per-player data storage and keybind handling for the Terra AI mod.
    /// Manages command history and Terra companion state for each player.
    /// </summary>
    public class TerraModPlayer : ModPlayer
    {
        /// <summary>
        /// The last command sent to Terra by this player.
        /// </summary>
        public string LastCommandedTerra { get; set; } = string.Empty;

        /// <summary>
        /// History of commands issued to Terra by this player.
        /// </summary>
        public List<string> CommandHistory { get; set; } = new List<string>();

        /// <summary>
        /// Maximum number of commands to store in history.
        /// </summary>
        private const int MaxCommandHistorySize = 100;

        /// <summary>
        /// Processes player input triggers each frame.
        /// Checks for keybind presses and triggers appropriate actions.
        /// </summary>
        /// <param name="triggersSet">The set of input triggers for this frame.</param>
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (TerraKeybinds.ToggleGUI != null && TerraKeybinds.ToggleGUI.JustPressed)
            {
                // Toggle the Terra AI GUI panel
                TerraSystem.Toggle();
            }
        }

        /// <summary>
        /// Called when the player enters a world.
        /// Can be used for auto-spawn logic or initialization.
        /// </summary>
        public override void OnEnterWorld()
        {
            // Optional: Auto-spawn Terra companion when entering world
            // This can be enabled via configuration
            // if (ModContent.GetInstance<TerraConfig>()?.AutoSpawnTerra == true)
            // {
            //     TerraSystem.SpawnTerra(Player);
            // }

            // Initialize or reset any per-session data
            TerraAIMod.Instance?.Logger.Info($"Player {Player.name} entered world. Terra AI ready.");
        }

        /// <summary>
        /// Called when the player disconnects from the server.
        /// Performs cleanup of Terra-related resources.
        /// </summary>
        public override void PlayerDisconnect()
        {
            // Cleanup Terra companion associated with this player
            // TODO: Implement TerraManager.CleanupForPlayer(Player) when TerraManager is created
            // TerraManager.CleanupForPlayer(Player);

            // Clear any temporary session data
            // Note: CommandHistory is persisted, but we might clear temporary flags here
        }

        /// <summary>
        /// Saves player-specific Terra AI data to the player file.
        /// </summary>
        /// <param name="tag">The tag compound to save data into.</param>
        public override void SaveData(TagCompound tag)
        {
            tag["LastCommandedTerra"] = LastCommandedTerra ?? string.Empty;
            tag["CommandHistory"] = CommandHistory ?? new List<string>();
        }

        /// <summary>
        /// Loads player-specific Terra AI data from the player file.
        /// </summary>
        /// <param name="tag">The tag compound to load data from.</param>
        public override void LoadData(TagCompound tag)
        {
            LastCommandedTerra = tag.GetString("LastCommandedTerra");
            CommandHistory = tag.Get<List<string>>("CommandHistory") ?? new List<string>();

            // Ensure command history doesn't exceed maximum size after loading
            if (CommandHistory.Count > MaxCommandHistorySize)
            {
                CommandHistory = CommandHistory.GetRange(
                    CommandHistory.Count - MaxCommandHistorySize,
                    MaxCommandHistorySize
                );
            }
        }

        /// <summary>
        /// Adds a command to the player's command history.
        /// </summary>
        /// <param name="command">The command to add to history.</param>
        public void AddCommandToHistory(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            CommandHistory.Add(command);
            LastCommandedTerra = command;

            // Trim history if it exceeds maximum size
            while (CommandHistory.Count > MaxCommandHistorySize)
            {
                CommandHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Clears the player's command history.
        /// </summary>
        public void ClearCommandHistory()
        {
            CommandHistory.Clear();
            LastCommandedTerra = string.Empty;
        }

        /// <summary>
        /// Gets the most recent commands from history.
        /// </summary>
        /// <param name="count">Number of recent commands to retrieve.</param>
        /// <returns>List of recent commands, most recent last.</returns>
        public List<string> GetRecentCommands(int count)
        {
            if (CommandHistory.Count <= count)
                return new List<string>(CommandHistory);

            return CommandHistory.GetRange(CommandHistory.Count - count, count);
        }
    }
}
