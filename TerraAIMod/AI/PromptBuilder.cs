using System;
using System.Text;
using Microsoft.Xna.Framework;
using TerraAIMod.NPCs;
using Terraria;

namespace TerraAIMod.AI
{
    /// <summary>
    /// Builds prompts for the AI system with Terraria-specific context.
    /// </summary>
    public static class PromptBuilder
    {
        /// <summary>
        /// Builds the system prompt that defines Terra's behavior and capabilities.
        /// </summary>
        /// <returns>The system prompt string.</returns>
        public static string BuildSystemPrompt()
        {
            return @"You are Terra, an AI companion in Terraria. You help players by executing tasks and providing assistance.

RESPONSE FORMAT:
You must respond with valid JSON in this exact format:
{
  ""reasoning"": ""Your thought process about the command"",
  ""plan"": ""Brief description of what you will do"",
  ""tasks"": [
    {
      ""action"": ""actionName"",
      ""parameters"": {
        ""param1"": ""value1"",
        ""param2"": 123
      }
    }
  ]
}

AVAILABLE ACTIONS:

1. dig - Dig/excavate tiles
   Parameters:
   - direction: ""down"", ""left"", ""right"", ""up""
   - depth: number (how many tiles)
   - width: number (optional, default 1)

2. mine - Mine specific ore or tiles
   Parameters:
   - target: ""iron"", ""copper"", ""silver"", ""gold"", ""demonite"", ""crimtane"", ""hellstone"", ""cobalt"", ""mythril"", ""adamantite"", ""chlorophyte"", etc.
   - amount: number (how many to collect)

3. place - Place a specific tile or block
   Parameters:
   - tile: tile type name (""wood"", ""stone"", ""torch"", etc.)
   - x: number (relative x offset from current position)
   - y: number (relative y offset from current position)

4. build - Build a structure
   Parameters:
   - structure: ""house"", ""tower"", ""arena"", ""hellevator"", ""bridge"", ""wall"", ""platform""
   - width: number (optional)
   - height: number (optional)
   - material: block type (optional, default ""wood"")

5. attack - Attack enemies
   Parameters:
   - target: ""nearest"", ""strongest"", or specific enemy name
   - weapon: weapon name (optional)

6. follow - Follow a player
   Parameters:
   - player: player name or ""nearest""
   - distance: number (optional, how close to stay)

7. boss - Help with boss fight or summon
   Parameters:
   - boss: boss name (""EyeOfCthulhu"", ""KingSlime"", ""EaterOfWorlds"", ""BrainOfCthulhu"", ""QueenBee"", ""Skeletron"", ""WallOfFlesh"", ""TheTwins"", ""TheDestroyer"", ""SkeletronPrime"", ""Plantera"", ""Golem"", ""DukeFishron"", ""Cultist"", ""MoonLord"")
   - action: ""prepare"", ""summon"", ""fight""

8. explore - Explore the world
   Parameters:
   - direction: ""left"", ""right"", ""up"", ""down""
   - biome: target biome (optional) - ""forest"", ""desert"", ""snow"", ""jungle"", ""corruption"", ""crimson"", ""hallow"", ""ocean"", ""underground"", ""cavern"", ""underworld""
   - distance: number (optional, in tiles)

9. npcHousing - Build or manage NPC housing
   Parameters:
   - npc: NPC name (""Guide"", ""Merchant"", ""Nurse"", ""ArmsDealer"", ""Dryad"", ""Demolitionist"", ""Clothier"", ""Mechanic"", ""Goblin"", ""Wizard"", ""Steampunker"", etc.)
   - action: ""build"", ""check"", ""assign""

TERRARIA RULES:
- NPC Housing: Requires 60+ tiles space, background walls, light source, door, table, and chair
- Boss Progression: King Slime -> Eye of Cthulhu -> Eater/Brain -> Queen Bee -> Skeletron -> Wall of Flesh (hardmode) -> Mech Bosses -> Plantera -> Golem -> Lunatic Cultist -> Moon Lord
- Biomes: Forest (surface), Desert, Snow/Ice, Jungle, Corruption/Crimson, Hallow (hardmode), Ocean, Underground, Cavern, Underworld
- Ore Tiers: Copper/Tin -> Iron/Lead -> Silver/Tungsten -> Gold/Platinum -> Demonite/Crimtane -> Hellstone -> Cobalt/Palladium -> Mythril/Orichalcum -> Adamantite/Titanium -> Chlorophyte

STRUCTURE OPTIONS:
- house: Standard NPC house (6x10 interior)
- tower: Vertical structure with platforms
- arena: Flat fighting area with campfires and heart lanterns
- hellevator: Vertical shaft to underworld (2-3 wide)
- bridge: Horizontal pathway over terrain
- wall: Defensive barrier
- platform: Wooden platforms for mobility

EXAMPLES:

User: ""Build a house for the merchant""
Response:
{
  ""reasoning"": ""The player wants an NPC house for the Merchant. I need to build a valid house with proper dimensions and furniture."",
  ""plan"": ""Build a wooden house with table, chair, door, torch, and background walls"",
  ""tasks"": [
    {
      ""action"": ""build"",
      ""parameters"": {
        ""structure"": ""house"",
        ""width"": 8,
        ""height"": 12,
        ""material"": ""wood""
      }
    },
    {
      ""action"": ""npcHousing"",
      ""parameters"": {
        ""npc"": ""Merchant"",
        ""action"": ""assign""
      }
    }
  ]
}

User: ""Mine some iron""
Response:
{
  ""reasoning"": ""The player needs iron ore. I should find and mine iron deposits."",
  ""plan"": ""Search for and mine iron ore deposits"",
  ""tasks"": [
    {
      ""action"": ""mine"",
      ""parameters"": {
        ""target"": ""iron"",
        ""amount"": 30
      }
    }
  ]
}

User: ""Dig a hellevator""
Response:
{
  ""reasoning"": ""A hellevator is a vertical shaft from surface to underworld for quick travel."",
  ""plan"": ""Build a hellevator - vertical shaft 3 blocks wide going straight down"",
  ""tasks"": [
    {
      ""action"": ""build"",
      ""parameters"": {
        ""structure"": ""hellevator"",
        ""width"": 3
      }
    }
  ]
}

User: ""Help me fight the Eye of Cthulhu""
Response:
{
  ""reasoning"": ""The player wants help with Eye of Cthulhu. I should prepare an arena and assist in the fight."",
  ""plan"": ""Prepare boss arena and assist with the fight"",
  ""tasks"": [
    {
      ""action"": ""boss"",
      ""parameters"": {
        ""boss"": ""EyeOfCthulhu"",
        ""action"": ""prepare""
      }
    },
    {
      ""action"": ""boss"",
      ""parameters"": {
        ""boss"": ""EyeOfCthulhu"",
        ""action"": ""fight""
      }
    }
  ]
}

User: ""Follow me""
Response:
{
  ""reasoning"": ""The player wants me to follow them."",
  ""plan"": ""Follow the nearest player at a comfortable distance"",
  ""tasks"": [
    {
      ""action"": ""follow"",
      ""parameters"": {
        ""player"": ""nearest"",
        ""distance"": 5
      }
    }
  ]
}

Remember: Always respond with valid JSON. Be helpful and consider the game context when planning tasks.";
        }

        /// <summary>
        /// Builds the user prompt with current game context.
        /// </summary>
        /// <param name="terra">The Terra NPC instance.</param>
        /// <param name="command">The player's command.</param>
        /// <param name="knowledge">World knowledge (can be null).</param>
        /// <returns>The user prompt string with context.</returns>
        public static string BuildUserPrompt(TerraNPC terra, string command, object knowledge = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== CURRENT CONTEXT ===");
            sb.AppendLine();

            // Position and location info
            if (terra?.NPC != null)
            {
                var npc = terra.NPC;
                int tileX = (int)(npc.Center.X / 16f);
                int tileY = (int)(npc.Center.Y / 16f);

                sb.AppendLine($"My Position: ({tileX}, {tileY})");
                sb.AppendLine($"Depth: {GetDepthDescription(tileY)}");
                sb.AppendLine($"Biome: {GetCurrentBiome(tileX, tileY)}");
            }

            // Time info
            sb.AppendLine($"Time: {(Main.dayTime ? "Day" : "Night")} ({GetTimeString()})");
            sb.AppendLine();

            // Memory context - current goal and recent actions
            if (terra?.Memory != null)
            {
                // Current goal
                if (!string.IsNullOrEmpty(terra.Memory.CurrentGoal))
                {
                    sb.AppendLine($"Current Goal: {terra.Memory.CurrentGoal}");
                }
                else
                {
                    sb.AppendLine("Current Goal: None (idle)");
                }

                // Recent actions
                var recentActions = terra.Memory.GetRecentActions(5);
                if (recentActions.Count > 0)
                {
                    sb.AppendLine("Recent Actions:");
                    foreach (var action in recentActions)
                    {
                        sb.AppendLine($"  - {action}");
                    }
                }
                sb.AppendLine();
            }

            // Nearby players
            sb.AppendLine("Nearby Players:");
            bool foundPlayers = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead && terra?.NPC != null)
                {
                    float distance = Vector2.Distance(terra.NPC.Center, player.Center);
                    if (distance < 1000f) // Within ~62 tiles
                    {
                        sb.AppendLine($"  - {player.name} (distance: {(int)(distance / 16f)} tiles)");
                        foundPlayers = true;
                    }
                }
            }
            if (!foundPlayers) sb.AppendLine("  - None nearby");
            sb.AppendLine();

            // Nearby town NPCs
            sb.AppendLine("Nearby NPCs:");
            bool foundNPCs = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc != null && npc.active && npc.townNPC && terra?.NPC != null)
                {
                    float distance = Vector2.Distance(terra.NPC.Center, npc.Center);
                    if (distance < 1000f)
                    {
                        sb.AppendLine($"  - {npc.GivenOrTypeName}");
                        foundNPCs = true;
                    }
                }
            }
            if (!foundNPCs) sb.AppendLine("  - None nearby");
            sb.AppendLine();

            // Nearby enemies
            sb.AppendLine("Nearby Enemies:");
            bool foundEnemies = false;
            int enemyCount = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc != null && npc.active && !npc.friendly && npc.damage > 0 && terra?.NPC != null)
                {
                    float distance = Vector2.Distance(terra.NPC.Center, npc.Center);
                    if (distance < 800f && enemyCount < 5)
                    {
                        sb.AppendLine($"  - {npc.GivenOrTypeName} (HP: {npc.life}/{npc.lifeMax})");
                        foundEnemies = true;
                        enemyCount++;
                    }
                }
            }
            if (!foundEnemies) sb.AppendLine("  - None nearby");
            sb.AppendLine();

            // Nearby tiles/blocks (simplified)
            sb.AppendLine("Nearby Notable Tiles:");
            if (terra?.NPC != null)
            {
                var notableTiles = GetNotableNearbyTiles(terra.NPC.Center);
                if (!string.IsNullOrEmpty(notableTiles))
                {
                    sb.AppendLine(notableTiles);
                }
                else
                {
                    sb.AppendLine("  - Standard terrain");
                }
            }
            sb.AppendLine();

            // Inventory context
            sb.AppendLine("My Inventory:");
            if (terra?.Inventory != null)
            {
                var inventorySummary = GetInventorySummary(terra.Inventory);
                if (!string.IsNullOrEmpty(inventorySummary))
                {
                    sb.AppendLine(inventorySummary);
                }
                else
                {
                    sb.AppendLine("  - Empty");
                }
            }
            else
            {
                sb.AppendLine("  - Empty");
            }
            sb.AppendLine();

            // World progress
            sb.AppendLine("World Progress:");
            sb.AppendLine($"  - Hardmode: {(Main.hardMode ? "Yes" : "No")}");
            sb.AppendLine($"  - Expert Mode: {(Main.expertMode ? "Yes" : "No")}");
            sb.AppendLine($"  - Master Mode: {(Main.masterMode ? "Yes" : "No")}");
            sb.AppendLine();

            // Defeated bosses
            sb.AppendLine("Defeated Bosses:");
            var defeatedBosses = GetDefeatedBosses();
            if (!string.IsNullOrEmpty(defeatedBosses))
            {
                sb.AppendLine(defeatedBosses);
            }
            else
            {
                sb.AppendLine("  - None yet");
            }
            sb.AppendLine();

            // The command
            sb.AppendLine("=== PLAYER COMMAND ===");
            sb.AppendLine(command);

            return sb.ToString();
        }

        /// <summary>
        /// Gets a description of the current depth level.
        /// </summary>
        private static string GetDepthDescription(int tileY)
        {
            double worldSurface = Main.worldSurface;
            double rockLayer = Main.rockLayer;
            int maxTilesY = Main.maxTilesY;

            if (tileY < worldSurface * 0.35)
                return "Space";
            if (tileY < worldSurface)
                return "Surface";
            if (tileY < rockLayer)
                return "Underground";
            if (tileY < maxTilesY - 200)
                return "Cavern";
            return "Underworld";
        }

        /// <summary>
        /// Gets the current biome at the specified tile position.
        /// </summary>
        private static string GetCurrentBiome(int tileX, int tileY)
        {
            // Check biomes based on typical Terraria biome detection
            // This is simplified - actual biome detection is more complex

            if (tileY > Main.maxTilesY - 200)
                return "Underworld";

            // Check player zone flags would be more accurate, but we can approximate
            if (tileX < Main.maxTilesX * 0.1 || tileX > Main.maxTilesX * 0.9)
                return "Ocean";

            // Check for dungeon (simplified)
            if (Main.tile[tileX, tileY].WallType == Terraria.ID.WallID.BlueDungeon ||
                Main.tile[tileX, tileY].WallType == Terraria.ID.WallID.GreenDungeon ||
                Main.tile[tileX, tileY].WallType == Terraria.ID.WallID.PinkDungeon)
                return "Dungeon";

            // Basic surface biomes based on tile types in area
            int jungleCount = 0, snowCount = 0, desertCount = 0, corruptCount = 0, crimsonCount = 0, hallowCount = 0;

            int checkRadius = 20;
            for (int x = tileX - checkRadius; x <= tileX + checkRadius; x++)
            {
                for (int y = tileY - checkRadius; y <= tileY + checkRadius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    var tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    switch (tile.TileType)
                    {
                        case Terraria.ID.TileID.JungleGrass:
                        case Terraria.ID.TileID.Mud:
                            jungleCount++;
                            break;
                        case Terraria.ID.TileID.SnowBlock:
                        case Terraria.ID.TileID.IceBlock:
                            snowCount++;
                            break;
                        case Terraria.ID.TileID.Sand:
                        case Terraria.ID.TileID.Sandstone:
                            desertCount++;
                            break;
                        case Terraria.ID.TileID.CorruptGrass:
                        case Terraria.ID.TileID.Ebonstone:
                            corruptCount++;
                            break;
                        case Terraria.ID.TileID.CrimsonGrass:
                        case Terraria.ID.TileID.Crimstone:
                            crimsonCount++;
                            break;
                        case Terraria.ID.TileID.HallowedGrass:
                        case Terraria.ID.TileID.Pearlstone:
                            hallowCount++;
                            break;
                    }
                }
            }

            int threshold = 30;
            if (jungleCount > threshold) return "Jungle";
            if (snowCount > threshold) return "Snow";
            if (desertCount > threshold) return "Desert";
            if (corruptCount > threshold) return "Corruption";
            if (crimsonCount > threshold) return "Crimson";
            if (hallowCount > threshold) return "Hallow";

            if (tileY < Main.worldSurface)
                return "Forest";
            if (tileY < Main.rockLayer)
                return "Underground";

            return "Cavern";
        }

        /// <summary>
        /// Gets a formatted time string.
        /// </summary>
        private static string GetTimeString()
        {
            double time = Main.time;
            int hours, minutes;

            if (Main.dayTime)
            {
                // Day starts at 4:30 AM
                hours = (int)((time / 3600.0) + 4.5);
                minutes = (int)((time % 3600.0) / 60.0);
            }
            else
            {
                // Night starts at 7:30 PM
                hours = (int)((time / 3600.0) + 19.5);
                if (hours >= 24) hours -= 24;
                minutes = (int)((time % 3600.0) / 60.0);
            }

            return $"{hours:D2}:{minutes:D2}";
        }

        /// <summary>
        /// Gets notable tiles near the given position.
        /// </summary>
        private static string GetNotableNearbyTiles(Vector2 center)
        {
            var sb = new StringBuilder();
            int tileX = (int)(center.X / 16f);
            int tileY = (int)(center.Y / 16f);
            int radius = 15;

            var foundOres = new System.Collections.Generic.HashSet<string>();
            var foundStructures = new System.Collections.Generic.HashSet<string>();

            for (int x = tileX - radius; x <= tileX + radius; x++)
            {
                for (int y = tileY - radius; y <= tileY + radius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    var tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    // Check for ores
                    switch (tile.TileType)
                    {
                        case Terraria.ID.TileID.Copper:
                            foundOres.Add("Copper Ore");
                            break;
                        case Terraria.ID.TileID.Iron:
                            foundOres.Add("Iron Ore");
                            break;
                        case Terraria.ID.TileID.Silver:
                            foundOres.Add("Silver Ore");
                            break;
                        case Terraria.ID.TileID.Gold:
                            foundOres.Add("Gold Ore");
                            break;
                        case Terraria.ID.TileID.Demonite:
                            foundOres.Add("Demonite Ore");
                            break;
                        case Terraria.ID.TileID.Crimtane:
                            foundOres.Add("Crimtane Ore");
                            break;
                        case Terraria.ID.TileID.Hellstone:
                            foundOres.Add("Hellstone");
                            break;
                        case Terraria.ID.TileID.Cobalt:
                            foundOres.Add("Cobalt Ore");
                            break;
                        case Terraria.ID.TileID.Mythril:
                            foundOres.Add("Mythril Ore");
                            break;
                        case Terraria.ID.TileID.Adamantite:
                            foundOres.Add("Adamantite Ore");
                            break;
                        case Terraria.ID.TileID.Chlorophyte:
                            foundOres.Add("Chlorophyte Ore");
                            break;
                        // Structures
                        case Terraria.ID.TileID.Containers:
                            foundStructures.Add("Chest");
                            break;
                        case Terraria.ID.TileID.Heart:
                            foundStructures.Add("Life Crystal");
                            break;
                        case Terraria.ID.TileID.ShadowOrbs:
                            foundStructures.Add("Shadow Orb/Crimson Heart");
                            break;
                    }
                }
            }

            foreach (var ore in foundOres)
            {
                sb.AppendLine($"  - {ore} nearby");
            }

            foreach (var structure in foundStructures)
            {
                sb.AppendLine($"  - {structure} nearby");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a summary of Terra's inventory, grouped by item type.
        /// </summary>
        /// <param name="inventory">The inventory array to summarize.</param>
        /// <returns>A formatted inventory summary string.</returns>
        private static string GetInventorySummary(Item[] inventory)
        {
            if (inventory == null || inventory.Length == 0)
                return null;

            var sb = new StringBuilder();
            var itemCounts = new System.Collections.Generic.Dictionary<string, int>();
            var weapons = new System.Collections.Generic.List<string>();
            var tools = new System.Collections.Generic.List<string>();
            var materials = new System.Collections.Generic.Dictionary<string, int>();

            foreach (var item in inventory)
            {
                if (item == null || item.IsAir)
                    continue;

                string itemName = item.Name;
                int count = item.stack;

                // Categorize items
                if (item.damage > 0 && item.pick == 0 && item.axe == 0 && item.hammer == 0)
                {
                    // Weapon
                    if (!weapons.Contains(itemName))
                        weapons.Add(itemName);
                }
                else if (item.pick > 0 || item.axe > 0 || item.hammer > 0)
                {
                    // Tool
                    if (!tools.Contains(itemName))
                        tools.Add(itemName);
                }
                else if (item.createTile >= 0 || item.createWall >= 0 || item.material)
                {
                    // Building material or crafting material
                    if (materials.ContainsKey(itemName))
                        materials[itemName] += count;
                    else
                        materials[itemName] = count;
                }
                else
                {
                    // Other items
                    if (itemCounts.ContainsKey(itemName))
                        itemCounts[itemName] += count;
                    else
                        itemCounts[itemName] = count;
                }
            }

            // Output weapons
            if (weapons.Count > 0)
            {
                sb.AppendLine($"  Weapons: {string.Join(", ", weapons)}");
            }

            // Output tools
            if (tools.Count > 0)
            {
                sb.AppendLine($"  Tools: {string.Join(", ", tools)}");
            }

            // Output materials (top 5)
            if (materials.Count > 0)
            {
                var sortedMaterials = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(materials);
                sortedMaterials.Sort((a, b) => b.Value.CompareTo(a.Value));
                var topMaterials = sortedMaterials.GetRange(0, Math.Min(5, sortedMaterials.Count));
                var materialStrings = new System.Collections.Generic.List<string>();
                foreach (var kvp in topMaterials)
                {
                    materialStrings.Add($"{kvp.Key} x{kvp.Value}");
                }
                sb.AppendLine($"  Materials: {string.Join(", ", materialStrings)}");
            }

            // Output other items (top 3)
            if (itemCounts.Count > 0)
            {
                var sortedItems = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(itemCounts);
                sortedItems.Sort((a, b) => b.Value.CompareTo(a.Value));
                var topItems = sortedItems.GetRange(0, Math.Min(3, sortedItems.Count));
                var itemStrings = new System.Collections.Generic.List<string>();
                foreach (var kvp in topItems)
                {
                    itemStrings.Add($"{kvp.Key} x{kvp.Value}");
                }
                sb.AppendLine($"  Other: {string.Join(", ", itemStrings)}");
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// Gets a string listing defeated bosses.
        /// </summary>
        private static string GetDefeatedBosses()
        {
            var sb = new StringBuilder();

            if (NPC.downedSlimeKing) sb.AppendLine("  - King Slime");
            if (NPC.downedBoss1) sb.AppendLine("  - Eye of Cthulhu");
            if (NPC.downedBoss2) sb.AppendLine("  - Eater of Worlds / Brain of Cthulhu");
            if (NPC.downedQueenBee) sb.AppendLine("  - Queen Bee");
            if (NPC.downedBoss3) sb.AppendLine("  - Skeletron");
            if (NPC.downedDeerclops) sb.AppendLine("  - Deerclops");
            if (Main.hardMode) sb.AppendLine("  - Wall of Flesh (Hardmode unlocked)");
            if (NPC.downedQueenSlime) sb.AppendLine("  - Queen Slime");
            if (NPC.downedMechBoss1) sb.AppendLine("  - The Destroyer");
            if (NPC.downedMechBoss2) sb.AppendLine("  - The Twins");
            if (NPC.downedMechBoss3) sb.AppendLine("  - Skeletron Prime");
            if (NPC.downedPlantBoss) sb.AppendLine("  - Plantera");
            if (NPC.downedGolemBoss) sb.AppendLine("  - Golem");
            if (NPC.downedFishron) sb.AppendLine("  - Duke Fishron");
            if (NPC.downedEmpressOfLight) sb.AppendLine("  - Empress of Light");
            if (NPC.downedAncientCultist) sb.AppendLine("  - Lunatic Cultist");
            if (NPC.downedMoonlord) sb.AppendLine("  - Moon Lord");

            return sb.ToString();
        }
    }
}
