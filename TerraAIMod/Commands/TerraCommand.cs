using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerraAIMod.NPCs;
using TerraAIMod.Systems;

namespace TerraAIMod.Commands
{
    /// <summary>
    /// Chat command handler for Terra AI operations.
    /// Provides commands to spawn, remove, control, and interact with Terra NPCs.
    /// </summary>
    public class TerraCommand : ModCommand
    {
        /// <summary>
        /// The command trigger text.
        /// </summary>
        public override string Command => "terra";

        /// <summary>
        /// The command type - World means it can be used in-game.
        /// </summary>
        public override CommandType Type => CommandType.World;

        /// <summary>
        /// Usage syntax for the command.
        /// </summary>
        public override string Usage => "/terra <spawn|remove|tell|list|stop|clear> [args]";

        /// <summary>
        /// Description of what the command does.
        /// </summary>
        public override string Description => "Manage Terra AI companions - spawn, remove, command, and control them.";

        /// <summary>
        /// Processes the command with the given arguments.
        /// </summary>
        /// <param name="caller">The command caller (player or server).</param>
        /// <param name="input">The raw input string.</param>
        /// <param name="args">Parsed command arguments.</param>
        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length == 0)
            {
                caller.Reply("Usage: " + Usage, Color.Yellow);
                caller.Reply("Subcommands:", Color.Yellow);
                caller.Reply("  spawn [name] - Spawn a Terra at your position", Color.White);
                caller.Reply("  remove <name> - Remove a Terra by name", Color.White);
                caller.Reply("  tell <name> <command> - Send a natural language command to Terra", Color.White);
                caller.Reply("  list - List all active Terras", Color.White);
                caller.Reply("  stop <name> - Stop Terra's current action", Color.White);
                caller.Reply("  clear - Remove all Terras", Color.White);
                return;
            }

            string subcommand = args[0].ToLowerInvariant();

            switch (subcommand)
            {
                case "spawn":
                    SpawnTerra(caller, args);
                    break;
                case "remove":
                    RemoveTerra(caller, args);
                    break;
                case "tell":
                    TellTerra(caller, args);
                    break;
                case "list":
                    ListTerras(caller);
                    break;
                case "stop":
                    StopTerra(caller, args);
                    break;
                case "clear":
                    ClearTerras(caller);
                    break;
                default:
                    caller.Reply($"Unknown subcommand: {subcommand}", Color.Red);
                    caller.Reply("Use /terra for help.", Color.Yellow);
                    break;
            }
        }

        /// <summary>
        /// Spawns a Terra NPC at the player's position with an optional offset.
        /// </summary>
        /// <param name="caller">The command caller.</param>
        /// <param name="args">Arguments array where args[1] is the optional name.</param>
        private void SpawnTerra(CommandCaller caller, string[] args)
        {
            string name = args.Length > 1 ? args[1] : "Terra";

            if (caller.Player == null)
            {
                caller.Reply("This command must be run by a player.", Color.Red);
                return;
            }

            var manager = TerraManager.Instance;
            if (manager == null)
            {
                caller.Reply("TerraManager is not initialized.", Color.Red);
                return;
            }

            // Spawn at player position with a small offset in front of the player
            Vector2 spawnPosition = caller.Player.Center + new Vector2(caller.Player.direction * 48f, 0f);

            var terra = manager.SpawnTerra(spawnPosition, name);
            if (terra != null)
            {
                caller.Reply($"Spawned Terra '{name}' at ({(int)spawnPosition.X}, {(int)spawnPosition.Y}).", Color.Green);
            }
            else
            {
                caller.Reply($"Failed to spawn Terra '{name}'. Name may already exist or max limit reached.", Color.Red);
            }
        }

        /// <summary>
        /// Removes a Terra NPC by name.
        /// </summary>
        /// <param name="caller">The command caller.</param>
        /// <param name="args">Arguments array where args[1] is the required name.</param>
        private void RemoveTerra(CommandCaller caller, string[] args)
        {
            if (args.Length < 2)
            {
                caller.Reply("Usage: /terra remove <name>", Color.Yellow);
                return;
            }

            string name = args[1];

            var manager = TerraManager.Instance;
            if (manager == null)
            {
                caller.Reply("TerraManager is not initialized.", Color.Red);
                return;
            }

            if (manager.RemoveTerra(name))
            {
                caller.Reply($"Removed Terra '{name}'.", Color.Green);
            }
            else
            {
                caller.Reply($"Terra '{name}' not found.", Color.Red);
            }
        }

        /// <summary>
        /// Sends a natural language command to a Terra NPC.
        /// The command is processed asynchronously via Task.Run.
        /// </summary>
        /// <param name="caller">The command caller.</param>
        /// <param name="args">Arguments array where args[1] is the name and args[2+] is the command.</param>
        private void TellTerra(CommandCaller caller, string[] args)
        {
            if (args.Length < 3)
            {
                caller.Reply("Usage: /terra tell <name> <command>", Color.Yellow);
                return;
            }

            string name = args[1];

            // Join all remaining args as the command text
            string command = string.Join(" ", args, 2, args.Length - 2);

            var manager = TerraManager.Instance;
            if (manager == null)
            {
                caller.Reply("TerraManager is not initialized.", Color.Red);
                return;
            }

            var terra = manager.GetTerra(name);
            if (terra == null)
            {
                caller.Reply($"Terra '{name}' not found.", Color.Red);
                return;
            }

            // Process the natural language command asynchronously (fire-and-forget)
            // Using discard to avoid deadlock from .Wait() on the main thread
            _ = terra.ProcessCommandAsync(command).ContinueWith(task =>
            {
                if (task.Exception != null)
                {
                    TerraAIMod.Instance?.Logger.Error($"Error processing command for Terra '{name}': {task.Exception.InnerException?.Message}");
                }
            });

            caller.Reply($"Sent command to Terra '{name}': {command}", Color.Cyan);
        }

        /// <summary>
        /// Lists all active Terra NPCs with their positions.
        /// </summary>
        /// <param name="caller">The command caller.</param>
        private void ListTerras(CommandCaller caller)
        {
            var manager = TerraManager.Instance;
            if (manager == null)
            {
                caller.Reply("TerraManager is not initialized.", Color.Red);
                return;
            }

            var terras = manager.GetAllTerras().ToList();

            if (terras == null || terras.Count == 0)
            {
                caller.Reply("No active Terras.", Color.Yellow);
                return;
            }

            caller.Reply($"Active Terras ({terras.Count}):", Color.Green);
            foreach (var terra in terras)
            {
                string status = terra.GetCurrentStatus();
                Vector2 pos = terra.NPC.Center;
                caller.Reply($"  - {terra.TerraName}: ({(int)pos.X}, {(int)pos.Y}) - {status}", Color.White);
            }
        }

        /// <summary>
        /// Stops the current action for a Terra NPC.
        /// </summary>
        /// <param name="caller">The command caller.</param>
        /// <param name="args">Arguments array where args[1] is the required name.</param>
        private void StopTerra(CommandCaller caller, string[] args)
        {
            if (args.Length < 2)
            {
                caller.Reply("Usage: /terra stop <name>", Color.Yellow);
                return;
            }

            string name = args[1];

            var manager = TerraManager.Instance;
            if (manager == null)
            {
                caller.Reply("TerraManager is not initialized.", Color.Red);
                return;
            }

            var terra = manager.GetTerra(name);
            if (terra == null)
            {
                caller.Reply($"Terra '{name}' not found.", Color.Red);
                return;
            }

            terra.StopCurrentAction();
            caller.Reply($"Stopped current action for Terra '{name}'.", Color.Green);
        }

        /// <summary>
        /// Removes all active Terra NPCs.
        /// </summary>
        /// <param name="caller">The command caller.</param>
        private void ClearTerras(CommandCaller caller)
        {
            var manager = TerraManager.Instance;
            if (manager == null)
            {
                caller.Reply("TerraManager is not initialized.", Color.Red);
                return;
            }

            int count = manager.ActiveCount;
            manager.ClearAllTerras();
            caller.Reply($"Cleared {count} Terra(s).", Color.Green);
        }
    }
}
