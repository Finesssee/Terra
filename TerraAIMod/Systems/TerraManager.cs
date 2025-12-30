using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using TerraAIMod.NPCs;
using TerraAIMod.Config;

namespace TerraAIMod.Systems
{
    /// <summary>
    /// Singleton manager for all Terra NPC instances.
    /// Handles spawning, tracking, and cleanup of Terra entities.
    /// </summary>
    public class TerraManager : ModSystem
    {
        /// <summary>
        /// Singleton instance of the TerraManager.
        /// </summary>
        public static TerraManager Instance { get; private set; }

        /// <summary>
        /// Dictionary mapping Terra names to their NPC.whoAmI index.
        /// </summary>
        private Dictionary<string, int> activeTerras;

        /// <summary>
        /// Reverse lookup dictionary mapping NPC.whoAmI to Terra names.
        /// </summary>
        private Dictionary<int, string> terrasByWhoAmI;

        /// <summary>
        /// Lock object for thread-safe operations.
        /// </summary>
        private readonly object lockObj = new object();

        /// <summary>
        /// Gets the number of currently active Terra instances.
        /// </summary>
        public int ActiveCount
        {
            get
            {
                lock (lockObj)
                {
                    return activeTerras?.Count ?? 0;
                }
            }
        }

        /// <summary>
        /// Called when the mod system is loaded. Initializes the singleton instance.
        /// </summary>
        public override void Load()
        {
            Instance = this;
            activeTerras = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            terrasByWhoAmI = new Dictionary<int, string>();
        }

        /// <summary>
        /// Called when the mod system is unloaded. Cleans up resources.
        /// </summary>
        public override void Unload()
        {
            ClearAllTerras();
            activeTerras = null;
            terrasByWhoAmI = null;
            Instance = null;
        }

        /// <summary>
        /// Called after a world is loaded. Reinitializes dictionaries for new world.
        /// </summary>
        public override void OnWorldLoad()
        {
            lock (lockObj)
            {
                activeTerras.Clear();
                terrasByWhoAmI.Clear();
            }
        }

        /// <summary>
        /// Called when a world is unloaded. Clears all Terra instances.
        /// </summary>
        public override void OnWorldUnload()
        {
            ClearAllTerras();
        }

        /// <summary>
        /// Spawns a new Terra NPC at the specified position with the given name.
        /// </summary>
        /// <param name="position">World position to spawn the Terra.</param>
        /// <param name="name">Unique name for the Terra instance.</param>
        /// <returns>The spawned TerraNPC instance, or null if spawn failed.</returns>
        public TerraNPC SpawnTerra(Vector2 position, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TerraAIMod.Instance?.Logger.Warn("Cannot spawn Terra with empty name");
                return null;
            }

            lock (lockObj)
            {
                // Check if name already exists
                if (activeTerras.ContainsKey(name))
                {
                    TerraAIMod.Instance?.Logger.Warn($"Terra name '{name}' already exists");
                    return null;
                }

                // Check max active Terras limit from config
                int maxTerras = GetMaxActiveTerras();
                if (activeTerras.Count >= maxTerras)
                {
                    TerraAIMod.Instance?.Logger.Warn($"Max Terra limit reached: {maxTerras}");
                    return null;
                }

                // Get the TerraNPC type
                int terraType = ModContent.NPCType<TerraNPC>();
                if (terraType <= 0)
                {
                    TerraAIMod.Instance?.Logger.Error("Failed to get TerraNPC type");
                    return null;
                }

                // Spawn the NPC
                int npcIndex = NPC.NewNPC(
                    new EntitySource_WorldEvent(),
                    (int)position.X,
                    (int)position.Y,
                    terraType
                );

                if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
                {
                    TerraAIMod.Instance?.Logger.Error($"Failed to spawn Terra NPC at {position}");
                    return null;
                }

                NPC npc = Main.npc[npcIndex];
                if (npc == null || !npc.active || npc.ModNPC is not TerraNPC terraNPC)
                {
                    TerraAIMod.Instance?.Logger.Error("Spawned NPC is not a valid TerraNPC");
                    return null;
                }

                // Set the Terra's name
                terraNPC.SetTerraName(name);
                npc.GivenName = name;

                // Register in dictionaries
                activeTerras[name] = npc.whoAmI;
                terrasByWhoAmI[npc.whoAmI] = name;

                TerraAIMod.Instance?.Logger.Info($"Successfully spawned Terra '{name}' at {position} (whoAmI: {npc.whoAmI})");

                // Sync to multiplayer if on server
                SyncSpawn(npc);

                return terraNPC;
            }
        }

        /// <summary>
        /// Gets a Terra NPC by its name.
        /// </summary>
        /// <param name="name">The name of the Terra to find.</param>
        /// <returns>The TerraNPC instance, or null if not found.</returns>
        public TerraNPC GetTerra(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            lock (lockObj)
            {
                if (!activeTerras.TryGetValue(name, out int whoAmI))
                    return null;

                if (whoAmI < 0 || whoAmI >= Main.maxNPCs)
                    return null;

                NPC npc = Main.npc[whoAmI];
                if (npc == null || !npc.active || npc.ModNPC is not TerraNPC terraNPC)
                    return null;

                return terraNPC;
            }
        }

        /// <summary>
        /// Gets a Terra NPC by its NPC.whoAmI index.
        /// </summary>
        /// <param name="whoAmI">The NPC.whoAmI index.</param>
        /// <returns>The TerraNPC instance, or null if not found.</returns>
        public TerraNPC GetTerraByWhoAmI(int whoAmI)
        {
            if (whoAmI < 0 || whoAmI >= Main.maxNPCs)
                return null;

            lock (lockObj)
            {
                if (!terrasByWhoAmI.ContainsKey(whoAmI))
                    return null;

                NPC npc = Main.npc[whoAmI];
                if (npc == null || !npc.active || npc.ModNPC is not TerraNPC terraNPC)
                    return null;

                return terraNPC;
            }
        }

        /// <summary>
        /// Removes a Terra by name, killing the NPC and removing from dictionaries.
        /// </summary>
        /// <param name="name">The name of the Terra to remove.</param>
        /// <returns>True if the Terra was found and removed, false otherwise.</returns>
        public bool RemoveTerra(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (lockObj)
            {
                if (!activeTerras.TryGetValue(name, out int whoAmI))
                    return false;

                // Remove from dictionaries first
                activeTerras.Remove(name);
                terrasByWhoAmI.Remove(whoAmI);

                // Kill the NPC if it exists
                if (whoAmI >= 0 && whoAmI < Main.maxNPCs)
                {
                    NPC npc = Main.npc[whoAmI];
                    if (npc != null && npc.active)
                    {
                        npc.life = 0;
                        npc.active = false;

                        // Sync to multiplayer
                        SyncDespawn(whoAmI);
                    }
                }

                TerraAIMod.Instance?.Logger.Info($"Removed Terra '{name}'");
                return true;
            }
        }

        /// <summary>
        /// Removes all active Terra instances.
        /// </summary>
        public void ClearAllTerras()
        {
            lock (lockObj)
            {
                if (activeTerras == null)
                    return;

                TerraAIMod.Instance?.Logger.Info($"Clearing {activeTerras.Count} Terra entities");

                // Kill all NPCs
                foreach (var kvp in activeTerras)
                {
                    int whoAmI = kvp.Value;
                    if (whoAmI >= 0 && whoAmI < Main.maxNPCs)
                    {
                        NPC npc = Main.npc[whoAmI];
                        if (npc != null && npc.active)
                        {
                            npc.life = 0;
                            npc.active = false;
                            SyncDespawn(whoAmI);
                        }
                    }
                }

                activeTerras.Clear();
                terrasByWhoAmI.Clear();
            }
        }

        /// <summary>
        /// Gets all active Terra NPC instances.
        /// </summary>
        /// <returns>An enumerable of all active TerraNPC instances.</returns>
        public IEnumerable<TerraNPC> GetAllTerras()
        {
            lock (lockObj)
            {
                var terras = new List<TerraNPC>();

                foreach (var kvp in activeTerras)
                {
                    int whoAmI = kvp.Value;
                    if (whoAmI >= 0 && whoAmI < Main.maxNPCs)
                    {
                        NPC npc = Main.npc[whoAmI];
                        if (npc != null && npc.active && npc.ModNPC is TerraNPC terraNPC)
                        {
                            terras.Add(terraNPC);
                        }
                    }
                }

                return terras;
            }
        }

        /// <summary>
        /// Gets the names of all active Terra instances.
        /// </summary>
        /// <returns>A list of Terra names.</returns>
        public List<string> GetTerraNames()
        {
            lock (lockObj)
            {
                return new List<string>(activeTerras.Keys);
            }
        }

        /// <summary>
        /// Performs cleanup of dead or removed Terra instances.
        /// Should be called periodically (e.g., in PostUpdateNPCs).
        /// </summary>
        public void Tick()
        {
            lock (lockObj)
            {
                if (activeTerras == null || activeTerras.Count == 0)
                    return;

                // Find dead or removed Terras
                var toRemove = new List<string>();

                foreach (var kvp in activeTerras)
                {
                    string name = kvp.Key;
                    int whoAmI = kvp.Value;

                    bool shouldRemove = false;

                    if (whoAmI < 0 || whoAmI >= Main.maxNPCs)
                    {
                        shouldRemove = true;
                    }
                    else
                    {
                        NPC npc = Main.npc[whoAmI];
                        if (npc == null || !npc.active || npc.life <= 0)
                        {
                            shouldRemove = true;
                        }
                        else if (npc.ModNPC is not TerraNPC)
                        {
                            // whoAmI was reused by a different NPC
                            shouldRemove = true;
                        }
                    }

                    if (shouldRemove)
                    {
                        toRemove.Add(name);
                    }
                }

                // Remove dead Terras
                foreach (string name in toRemove)
                {
                    if (activeTerras.TryGetValue(name, out int whoAmI))
                    {
                        activeTerras.Remove(name);
                        terrasByWhoAmI.Remove(whoAmI);
                        TerraAIMod.Instance?.Logger.Info($"Cleaned up Terra: {name}");
                    }
                }
            }
        }

        /// <summary>
        /// Called after NPCs are updated each frame. Performs tick cleanup.
        /// </summary>
        public override void PostUpdateNPCs()
        {
            Tick();
        }

        /// <summary>
        /// Syncs NPC spawn to all clients in multiplayer.
        /// </summary>
        /// <param name="npc">The NPC to sync.</param>
        private void SyncSpawn(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
        }

        /// <summary>
        /// Syncs NPC despawn to all clients in multiplayer.
        /// </summary>
        /// <param name="whoAmI">The NPC.whoAmI index to sync.</param>
        private void SyncDespawn(int whoAmI)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, whoAmI);
            }
        }

        /// <summary>
        /// Gets the maximum number of active Terras from config.
        /// </summary>
        /// <returns>The maximum number of allowed active Terras.</returns>
        private int GetMaxActiveTerras()
        {
            // Try to get from config if available
            // Default to 10 if config is not set up
            try
            {
                if (ModContent.GetInstance<TerraConfig>() is TerraConfig config)
                {
                    return config.MaxActiveTerras;
                }
            }
            catch
            {
                // Config not available, use default
            }

            return 10;
        }

        /// <summary>
        /// Checks if a Terra with the given name exists.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <returns>True if a Terra with that name exists, false otherwise.</returns>
        public bool HasTerra(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (lockObj)
            {
                return activeTerras.ContainsKey(name);
            }
        }

        /// <summary>
        /// Registers an existing Terra NPC (e.g., loaded from save or synced from server).
        /// </summary>
        /// <param name="terraNPC">The Terra NPC to register.</param>
        public void RegisterTerra(TerraNPC terraNPC)
        {
            if (terraNPC == null || string.IsNullOrWhiteSpace(terraNPC.TerraName))
                return;

            NPC npc = terraNPC.NPC;
            if (npc == null || !npc.active)
                return;

            lock (lockObj)
            {
                string name = terraNPC.TerraName;
                int whoAmI = npc.whoAmI;

                // Remove any stale entries with the same name or whoAmI
                if (activeTerras.TryGetValue(name, out int oldWhoAmI))
                {
                    terrasByWhoAmI.Remove(oldWhoAmI);
                }
                if (terrasByWhoAmI.TryGetValue(whoAmI, out string oldName))
                {
                    activeTerras.Remove(oldName);
                }

                activeTerras[name] = whoAmI;
                terrasByWhoAmI[whoAmI] = name;

                TerraAIMod.Instance?.Logger.Info($"Registered Terra '{name}' (whoAmI: {whoAmI})");
            }
        }
    }
}
