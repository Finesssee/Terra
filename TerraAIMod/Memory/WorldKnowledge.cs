using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerraAIMod.NPCs;

namespace TerraAIMod.Memory
{
    /// <summary>
    /// Provides world state awareness for Terra, including biome detection,
    /// nearby entities, and environmental information.
    /// </summary>
    public class WorldKnowledge
    {
        #region Fields

        /// <summary>
        /// Reference to Terra's NPC instance.
        /// </summary>
        private NPC terra;

        /// <summary>
        /// The radius (in tiles) to scan for nearby entities and tiles.
        /// </summary>
        private int scanRadius = 50;

        /// <summary>
        /// Dictionary mapping tile types to their counts in the scan area.
        /// </summary>
        private Dictionary<int, int> nearbyTiles;

        /// <summary>
        /// List of friendly NPCs (town NPCs) nearby.
        /// </summary>
        private List<NPC> nearbyNPCs;

        /// <summary>
        /// List of hostile enemies nearby.
        /// </summary>
        private List<NPC> nearbyEnemies;

        /// <summary>
        /// List of players nearby.
        /// </summary>
        private List<Player> nearbyPlayers;

        /// <summary>
        /// The current biome Terra is in.
        /// </summary>
        private string currentBiome;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new WorldKnowledge instance and performs an initial scan.
        /// </summary>
        /// <param name="terraNPC">The TerraNPC to track world knowledge for.</param>
        public WorldKnowledge(TerraNPC terraNPC)
        {
            terra = terraNPC?.NPC;
            nearbyTiles = new Dictionary<int, int>();
            nearbyNPCs = new List<NPC>();
            nearbyEnemies = new List<NPC>();
            nearbyPlayers = new List<Player>();
            currentBiome = "Forest";

            Scan();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Performs a full scan of the surrounding area.
        /// </summary>
        public void Scan()
        {
            if (terra == null || !terra.active)
                return;

            ScanBiome();
            ScanTiles();
            ScanEntities();
            ScanPlayers();
        }

        /// <summary>
        /// Gets the current biome name.
        /// </summary>
        /// <returns>The name of the current biome.</returns>
        public string GetCurrentBiome()
        {
            return currentBiome;
        }

        /// <summary>
        /// Gets a description of Terra's current depth.
        /// </summary>
        /// <returns>A string describing the depth level.</returns>
        public string GetDepthDescription()
        {
            if (terra == null)
                return "Unknown";

            float tileY = terra.position.Y / 16f;

            // Space level (above surface)
            if (tileY < Main.worldSurface * 0.35f)
                return "Space";

            // Surface level
            if (tileY < Main.worldSurface)
                return "Surface";

            // Underground (above rock layer)
            if (tileY < Main.rockLayer)
                return "Underground";

            // Cavern (rock layer)
            if (tileY < Main.maxTilesY - 200)
                return "Cavern";

            // Underworld
            return "Underworld";
        }

        /// <summary>
        /// Gets the current time of day as a descriptive string.
        /// </summary>
        /// <returns>A string describing the time of day.</returns>
        public string GetTimeOfDay()
        {
            if (Main.dayTime)
            {
                // Day time: 0 = 4:30 AM, 54000 = 7:30 PM
                double time = Main.time;

                if (time < 13500) // 4:30 AM - 7:30 AM
                    return "Morning";
                else if (time < 27000) // 7:30 AM - 10:30 AM
                    return "Late Morning";
                else if (time < 32400) // 10:30 AM - 12:00 PM
                    return "Noon";
                else if (time < 45900) // 12:00 PM - 4:30 PM
                    return "Afternoon";
                else // 4:30 PM - 7:30 PM
                    return "Evening";
            }
            else
            {
                // Night time: 0 = 7:30 PM, 32400 = 4:30 AM
                return "Night";
            }
        }

        /// <summary>
        /// Gets the names of nearby players.
        /// </summary>
        /// <returns>A list of nearby player names.</returns>
        public List<string> GetNearbyPlayerNames()
        {
            return nearbyPlayers
                .Where(p => p != null && p.active)
                .Select(p => p.name)
                .ToList();
        }

        /// <summary>
        /// Gets a summary of nearby friendly NPCs, grouped by type.
        /// </summary>
        /// <returns>A formatted summary string of nearby NPCs (top 5).</returns>
        public string GetNearbyNPCsSummary()
        {
            if (nearbyNPCs.Count == 0)
                return "None";

            var grouped = nearbyNPCs
                .Where(n => n != null && n.active)
                .GroupBy(n => n.GivenOrTypeName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5);

            var summaries = grouped.Select(g =>
                g.Count > 1 ? $"{g.Name} x{g.Count}" : g.Name);

            return string.Join(", ", summaries);
        }

        /// <summary>
        /// Gets a summary of nearby enemies, grouped by type.
        /// </summary>
        /// <returns>A formatted summary string of nearby enemies (top 5).</returns>
        public string GetNearbyEnemiesSummary()
        {
            if (nearbyEnemies.Count == 0)
                return "None";

            var grouped = nearbyEnemies
                .Where(n => n != null && n.active)
                .GroupBy(n => n.GivenOrTypeName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5);

            var summaries = grouped.Select(g =>
                g.Count > 1 ? $"{g.Name} x{g.Count}" : g.Name);

            return string.Join(", ", summaries);
        }

        /// <summary>
        /// Gets a summary of nearby tiles, showing the top 5 most common types.
        /// </summary>
        /// <returns>A formatted summary string of nearby tile types.</returns>
        public string GetNearbyTilesSummary()
        {
            if (nearbyTiles.Count == 0)
                return "None";

            var topTiles = nearbyTiles
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => $"{GetTileName(kvp.Key)}: {kvp.Value}");

            return string.Join(", ", topTiles);
        }

        /// <summary>
        /// Gets the current world progression stage.
        /// </summary>
        /// <returns>A string describing the world's progression state.</returns>
        public string GetWorldProgress()
        {
            if (!Main.hardMode)
            {
                // Pre-Hardmode progression
                if (NPC.downedBoss3) // Skeletron
                    return "Pre-Hardmode (Late)";
                else if (NPC.downedBoss2) // Evil boss
                    return "Pre-Hardmode (Mid)";
                else if (NPC.downedBoss1) // Eye of Cthulhu
                    return "Pre-Hardmode (Early)";
                else
                    return "Pre-Hardmode (Start)";
            }
            else
            {
                // Hardmode progression
                if (NPC.downedMoonlord)
                    return "Hardmode (Post-Moon Lord)";
                else if (NPC.downedAncientCultist)
                    return "Hardmode (Post-Cultist)";
                else if (NPC.downedGolemBoss)
                    return "Hardmode (Post-Golem)";
                else if (NPC.downedPlantBoss)
                    return "Hardmode (Post-Plantera)";
                else if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
                    return "Hardmode (Post-Mechs)";
                else if (NPC.downedMechBossAny)
                    return "Hardmode (Mid)";
                else
                    return "Hardmode (Early)";
            }
        }

        /// <summary>
        /// Gets a list of all defeated bosses.
        /// </summary>
        /// <returns>A list of defeated boss names.</returns>
        public List<string> GetDefeatedBosses()
        {
            var defeated = new List<string>();

            // Pre-Hardmode bosses
            if (NPC.downedSlimeKing)
                defeated.Add("King Slime");
            if (NPC.downedBoss1)
                defeated.Add("Eye of Cthulhu");
            if (NPC.downedBoss2)
                defeated.Add(WorldGen.crimson ? "Brain of Cthulhu" : "Eater of Worlds");
            if (NPC.downedQueenBee)
                defeated.Add("Queen Bee");
            if (NPC.downedBoss3)
                defeated.Add("Skeletron");
            if (NPC.downedDeerclops)
                defeated.Add("Deerclops");
            if (Main.hardMode)
                defeated.Add("Wall of Flesh");

            // Hardmode bosses
            if (NPC.downedQueenSlime)
                defeated.Add("Queen Slime");
            if (NPC.downedMechBoss1)
                defeated.Add("The Destroyer");
            if (NPC.downedMechBoss2)
                defeated.Add("The Twins");
            if (NPC.downedMechBoss3)
                defeated.Add("Skeletron Prime");
            if (NPC.downedPlantBoss)
                defeated.Add("Plantera");
            if (NPC.downedGolemBoss)
                defeated.Add("Golem");
            if (NPC.downedFishron)
                defeated.Add("Duke Fishron");
            if (NPC.downedEmpressOfLight)
                defeated.Add("Empress of Light");
            if (NPC.downedAncientCultist)
                defeated.Add("Lunatic Cultist");
            if (NPC.downedMoonlord)
                defeated.Add("Moon Lord");

            // Events
            if (NPC.downedGoblins)
                defeated.Add("Goblin Army");
            if (NPC.downedPirates)
                defeated.Add("Pirate Invasion");
            if (NPC.downedMartians)
                defeated.Add("Martian Madness");
            if (NPC.downedHalloweenKing)
                defeated.Add("Pumpking");
            if (NPC.downedHalloweenTree)
                defeated.Add("Mourning Wood");
            if (NPC.downedChristmasIceQueen)
                defeated.Add("Ice Queen");
            if (NPC.downedChristmasSantank)
                defeated.Add("Santa-NK1");
            if (NPC.downedChristmasTree)
                defeated.Add("Everscream");

            return defeated;
        }

        #endregion

        #region Private Scan Methods

        /// <summary>
        /// Determines the current biome based on tile composition.
        /// </summary>
        private void ScanBiome()
        {
            if (terra == null)
                return;

            int tileX = (int)(terra.Center.X / 16f);
            int tileY = (int)(terra.Center.Y / 16f);

            // Count biome-specific tiles
            int jungleTiles = 0;
            int snowTiles = 0;
            int desertTiles = 0;
            int corruptTiles = 0;
            int crimsonTiles = 0;
            int hallowTiles = 0;
            int mushroomTiles = 0;

            int checkRadius = 42; // Standard biome check radius

            for (int x = tileX - checkRadius; x <= tileX + checkRadius; x++)
            {
                for (int y = tileY - checkRadius; y <= tileY + checkRadius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    int type = tile.TileType;

                    // Jungle tiles
                    if (type == TileID.JungleGrass || type == TileID.JunglePlants ||
                        type == TileID.JunglePlants2 || type == TileID.PlantDetritus ||
                        type == TileID.JungleVines || type == TileID.Hive ||
                        type == TileID.LihzahrdBrick)
                        jungleTiles++;

                    // Snow tiles
                    else if (type == TileID.SnowBlock || type == TileID.IceBlock ||
                             type == TileID.CorruptIce || type == TileID.FleshIce ||
                             type == TileID.HallowedIce)
                        snowTiles++;

                    // Desert tiles
                    else if (type == TileID.Sand || type == TileID.Sandstone ||
                             type == TileID.HardenedSand || type == TileID.DesertFossil)
                        desertTiles++;

                    // Corruption tiles
                    else if (type == TileID.CorruptGrass || type == TileID.Ebonstone ||
                             type == TileID.CorruptSandstone || type == TileID.CorruptHardenedSand ||
                             type == TileID.Ebonsand || type == TileID.CorruptThorns)
                        corruptTiles++;

                    // Crimson tiles
                    else if (type == TileID.CrimsonGrass || type == TileID.Crimstone ||
                             type == TileID.CrimsonSandstone || type == TileID.CrimsonHardenedSand ||
                             type == TileID.Crimsand || type == TileID.CrimsonThorns)
                        crimsonTiles++;

                    // Hallow tiles
                    else if (type == TileID.HallowedGrass || type == TileID.Pearlstone ||
                             type == TileID.HallowSandstone || type == TileID.HallowHardenedSand ||
                             type == TileID.Pearlsand)
                        hallowTiles++;

                    // Mushroom tiles
                    else if (type == TileID.MushroomGrass || type == TileID.MushroomPlants ||
                             type == TileID.MushroomTrees || type == TileID.MushroomVines)
                        mushroomTiles++;
                }
            }

            // Determine biome based on tile counts (thresholds based on Terraria's biome detection)
            // Check position-based biomes first
            if (tileY > Main.maxTilesY - 200)
            {
                currentBiome = "Underworld";
            }
            else if (tileY < Main.worldSurface * 0.35f)
            {
                currentBiome = "Space";
            }
            else if (tileX < 380 || tileX > Main.maxTilesX - 380)
            {
                // Near world edge at surface level
                if (tileY < Main.worldSurface)
                    currentBiome = "Ocean";
                else
                    currentBiome = "Underground";
            }
            // Check tile-based biomes
            else if (corruptTiles >= 200)
            {
                currentBiome = "Corruption";
            }
            else if (crimsonTiles >= 200)
            {
                currentBiome = "Crimson";
            }
            else if (hallowTiles >= 100)
            {
                currentBiome = "Hallow";
            }
            else if (jungleTiles >= 80)
            {
                currentBiome = "Jungle";
            }
            else if (snowTiles >= 300)
            {
                currentBiome = "Snow";
            }
            else if (desertTiles >= 1000)
            {
                currentBiome = "Desert";
            }
            else if (mushroomTiles >= 100)
            {
                currentBiome = "Mushroom";
            }
            else
            {
                currentBiome = "Forest";
            }
        }

        /// <summary>
        /// Scans and counts tile types in the surrounding area.
        /// </summary>
        private void ScanTiles()
        {
            nearbyTiles.Clear();

            if (terra == null)
                return;

            int centerX = (int)(terra.Center.X / 16f);
            int centerY = (int)(terra.Center.Y / 16f);

            for (int x = centerX - scanRadius; x <= centerX + scanRadius; x++)
            {
                for (int y = centerY - scanRadius; y <= centerY + scanRadius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    int type = tile.TileType;

                    if (nearbyTiles.ContainsKey(type))
                        nearbyTiles[type]++;
                    else
                        nearbyTiles[type] = 1;
                }
            }
        }

        /// <summary>
        /// Scans for nearby NPCs and categorizes them as friendly or hostile.
        /// </summary>
        private void ScanEntities()
        {
            nearbyNPCs.Clear();
            nearbyEnemies.Clear();

            if (terra == null)
                return;

            float scanDistance = scanRadius * 16f; // Convert tiles to pixels

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || npc.whoAmI == terra.whoAmI)
                    continue;

                float distance = Vector2.Distance(terra.Center, npc.Center);
                if (distance > scanDistance)
                    continue;

                if (npc.friendly || npc.townNPC || NPCID.Sets.ActsLikeTownNPC[npc.type])
                {
                    nearbyNPCs.Add(npc);
                }
                else if (!npc.friendly && npc.damage > 0)
                {
                    nearbyEnemies.Add(npc);
                }
            }
        }

        /// <summary>
        /// Scans for nearby players.
        /// </summary>
        private void ScanPlayers()
        {
            nearbyPlayers.Clear();

            if (terra == null)
                return;

            float scanDistance = scanRadius * 16f; // Convert tiles to pixels

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player == null || !player.active || player.dead)
                    continue;

                float distance = Vector2.Distance(terra.Center, player.Center);
                if (distance <= scanDistance)
                {
                    nearbyPlayers.Add(player);
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a human-readable name for a tile type.
        /// </summary>
        /// <param name="tileType">The tile type ID.</param>
        /// <returns>A string name for the tile type.</returns>
        private string GetTileName(int tileType)
        {
            // Common tiles
            switch (tileType)
            {
                case TileID.Dirt: return "Dirt";
                case TileID.Stone: return "Stone";
                case TileID.Grass: return "Grass";
                case TileID.Sand: return "Sand";
                case TileID.SnowBlock: return "Snow";
                case TileID.IceBlock: return "Ice";
                case TileID.Mud: return "Mud";
                case TileID.JungleGrass: return "Jungle Grass";
                case TileID.CorruptGrass: return "Corrupt Grass";
                case TileID.CrimsonGrass: return "Crimson Grass";
                case TileID.HallowedGrass: return "Hallowed Grass";
                case TileID.MushroomGrass: return "Mushroom Grass";
                case TileID.Ash: return "Ash";
                case TileID.Hellstone: return "Hellstone";
                case TileID.Obsidian: return "Obsidian";
                case TileID.Ebonstone: return "Ebonstone";
                case TileID.Crimstone: return "Crimstone";
                case TileID.Pearlstone: return "Pearlstone";
                case TileID.ClayBlock: return "Clay";
                case TileID.Silt: return "Silt";
                case TileID.Slush: return "Slush";

                // Ores
                case TileID.Copper: return "Copper Ore";
                case TileID.Tin: return "Tin Ore";
                case TileID.Iron: return "Iron Ore";
                case TileID.Lead: return "Lead Ore";
                case TileID.Silver: return "Silver Ore";
                case TileID.Tungsten: return "Tungsten Ore";
                case TileID.Gold: return "Gold Ore";
                case TileID.Platinum: return "Platinum Ore";
                case TileID.Demonite: return "Demonite Ore";
                case TileID.Crimtane: return "Crimtane Ore";
                case TileID.Meteorite: return "Meteorite";
                case TileID.Cobalt: return "Cobalt Ore";
                case TileID.Palladium: return "Palladium Ore";
                case TileID.Mythril: return "Mythril Ore";
                case TileID.Orichalcum: return "Orichalcum Ore";
                case TileID.Adamantite: return "Adamantite Ore";
                case TileID.Titanium: return "Titanium Ore";
                case TileID.Chlorophyte: return "Chlorophyte Ore";
                case TileID.LunarOre: return "Luminite";

                // Wood types
                case TileID.WoodBlock: return "Wood";
                case TileID.Ebonwood: return "Ebonwood";
                case TileID.Shadewood: return "Shadewood";
                case TileID.Pearlwood: return "Pearlwood";
                case TileID.RichMahogany: return "Rich Mahogany";
                case TileID.BorealWood: return "Boreal Wood";
                case TileID.PalmWood: return "Palm Wood";

                // Building blocks
                case TileID.GrayBrick: return "Gray Brick";
                case TileID.RedBrick: return "Red Brick";
                case TileID.HellstoneBrick: return "Hellstone Brick";
                case TileID.Sandstone: return "Sandstone";
                case TileID.Marble: return "Marble";
                case TileID.Granite: return "Granite";

                // Interactive
                case TileID.Torches: return "Torch";
                case TileID.Containers: return "Chest";
                case TileID.WorkBenches: return "Workbench";
                case TileID.Furnaces: return "Furnace";
                case TileID.Anvils: return "Anvil";
                case TileID.ClosedDoor:
                case TileID.OpenDoor: return "Door";
                case TileID.Tables: return "Table";
                case TileID.Chairs: return "Chair";
                case TileID.Beds: return "Bed";
                case TileID.Platforms: return "Platform";

                // Nature
                case TileID.Trees: return "Tree";
                case TileID.Plants: return "Plants";
                case TileID.CorruptPlants: return "Corrupt Plants";
                case TileID.CrimsonPlants: return "Crimson Plants";
                case TileID.HallowedPlants: return "Hallowed Plants";
                case TileID.JunglePlants: return "Jungle Plants";

                default:
                    return $"Tile #{tileType}";
            }
        }

        #endregion
    }
}
