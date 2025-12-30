using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action that mines specific tile types using Terraria pickaxe tier mechanics.
    /// Navigates to target tiles and mines them based on pickaxe power requirements.
    /// </summary>
    public class MineTileAction : BaseAction
    {
        #region Constants

        /// <summary>
        /// Maximum ticks before timeout (12000 ticks = 200 seconds at 60 TPS).
        /// </summary>
        private const int MaxTicks = 12000;

        /// <summary>
        /// Distance in tiles to consider close enough for mining.
        /// </summary>
        private const int MiningRange = 4;

        /// <summary>
        /// Search radius in tiles for finding target tiles.
        /// </summary>
        private const int SearchRadius = 30;

        /// <summary>
        /// Tile size in pixels.
        /// </summary>
        private const float TileSize = 16f;

        /// <summary>
        /// Base mining damage per tick.
        /// </summary>
        private const int BaseMiningDamage = 10;

        #endregion

        #region Static Pickaxe Power Requirements

        /// <summary>
        /// Minimum pickaxe power required to mine specific tile types.
        /// </summary>
        private static readonly Dictionary<int, int> PickaxePowerRequirements = new Dictionary<int, int>
        {
            { TileID.Meteorite, 50 },
            { TileID.Demonite, 55 },
            { TileID.Crimtane, 55 },
            { TileID.Obsidian, 65 },
            { TileID.Hellstone, 65 },
            { TileID.Cobalt, 100 },
            { TileID.Palladium, 100 },
            { TileID.Mythril, 110 },
            { TileID.Orichalcum, 110 },
            { TileID.Adamantite, 150 },
            { TileID.Titanium, 150 },
            { TileID.Chlorophyte, 200 }
        };

        #endregion

        #region Fields

        /// <summary>
        /// The type of tile to mine.
        /// </summary>
        private int targetTileType;

        /// <summary>
        /// The quantity of tiles to mine.
        /// </summary>
        private int targetQuantity;

        /// <summary>
        /// The number of tiles successfully mined.
        /// </summary>
        private int minedCount;

        /// <summary>
        /// The current target tile position.
        /// </summary>
        private Point currentTarget;

        /// <summary>
        /// Number of ticks this action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Current mining progress on the target tile (0-100).
        /// </summary>
        private int miningProgress;

        /// <summary>
        /// The pathfinder used to compute paths to tiles.
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// The executor that controls Terra's movement along the path.
        /// </summary>
        private PathExecutor pathExecutor;

        /// <summary>
        /// Terra's current pickaxe power.
        /// </summary>
        private int pickaxePower;

        /// <summary>
        /// Whether we are currently navigating to a target.
        /// </summary>
        private bool isNavigating;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new MineTileAction for the specified Terra NPC and task.
        /// </summary>
        /// <param name="terra">The Terra NPC that will execute this action.</param>
        /// <param name="task">The task containing target tile information.</param>
        public MineTileAction(TerraNPC terra, Task task) : base(terra, task)
        {
            ticksRunning = 0;
            minedCount = 0;
            miningProgress = 0;
            currentTarget = Point.Zero;
            isNavigating = false;
            pickaxePower = 100; // Default pickaxe power (can be enhanced with equipment checks)
        }

        #endregion

        #region BaseAction Implementation

        /// <summary>
        /// A human-readable description of what this action does.
        /// </summary>
        public override string Description => $"Mining {GetTileName(targetTileType)} ({minedCount}/{targetQuantity})";

        /// <summary>
        /// Called when the action starts. Initializes mining parameters.
        /// </summary>
        protected override void OnStart()
        {
            // Get tile type from task parameters
            string tileName = task.GetParameter<string>("tile", "");
            targetTileType = ParseTileType(tileName);

            if (targetTileType < 0)
            {
                result = ActionResult.Fail($"Unknown tile type: {tileName}");
                return;
            }

            // Get target quantity
            targetQuantity = task.GetParameter<int>("quantity", 1);
            if (targetQuantity <= 0)
            {
                targetQuantity = 1;
            }

            // Get pickaxe power if specified
            pickaxePower = task.GetParameter<int>("pickaxePower", 100);

            // Initialize pathfinder and executor
            pathfinder = new TerrariaPathfinder(terra.NPC);
            pathExecutor = new PathExecutor(terra.NPC);

            // Find the first target
            if (!FindNextTarget())
            {
                result = ActionResult.Fail($"No {GetTileName(targetTileType)} tiles found within range");
                return;
            }
        }

        /// <summary>
        /// Called each game tick while the action is running.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            if (ticksRunning >= MaxTicks)
            {
                result = ActionResult.Fail($"Mining timed out after {MaxTicks} ticks. Mined {minedCount}/{targetQuantity}");
                return;
            }

            // Check if we've reached our target quantity
            if (minedCount >= targetQuantity)
            {
                result = ActionResult.Succeed($"Successfully mined {minedCount} {GetTileName(targetTileType)} tiles");
                return;
            }

            // Check if current target is still valid
            if (!IsValidTarget(currentTarget))
            {
                miningProgress = 0;
                if (!FindNextTarget())
                {
                    if (minedCount > 0)
                    {
                        result = ActionResult.Succeed($"Mined {minedCount}/{targetQuantity} tiles (no more targets found)");
                    }
                    else
                    {
                        result = ActionResult.Fail($"No {GetTileName(targetTileType)} tiles found");
                    }
                    return;
                }
            }

            // Calculate distance to target
            float distanceToTarget = GetDistanceToTarget();

            if (distanceToTarget > MiningRange)
            {
                // Navigate to target
                NavigateToTarget();
            }
            else
            {
                // Close enough to mine
                isNavigating = false;
                MineCurrentTarget();
            }
        }

        /// <summary>
        /// Called when the action is cancelled. Stops movement.
        /// </summary>
        protected override void OnCancel()
        {
            terra.NPC.velocity.X = 0;
            terra.NPC.velocity.Y = 0;
            pathExecutor?.ClearPath();
        }

        #endregion

        #region Mining Methods

        /// <summary>
        /// Attempts to mine the current target tile.
        /// </summary>
        private void MineCurrentTarget()
        {
            // Check if we have sufficient pickaxe power
            int requiredPower = GetRequiredPickaxePower(targetTileType);
            if (pickaxePower < requiredPower)
            {
                result = ActionResult.Fail($"Insufficient pickaxe power ({pickaxePower}) to mine {GetTileName(targetTileType)} (requires {requiredPower})");
                return;
            }

            // Face the target
            int direction = Math.Sign(currentTarget.X * TileSize - terra.NPC.Center.X);
            if (direction != 0)
            {
                terra.NPC.direction = direction;
            }

            // Calculate mining damage based on pickaxe power and tile hardness
            int tileHardness = GetTileHardness(targetTileType);
            int effectiveDamage = BaseMiningDamage + (pickaxePower - requiredPower) / 10;
            effectiveDamage = Math.Max(1, effectiveDamage);

            // Apply mining progress
            miningProgress += effectiveDamage;

            // Check if tile is fully mined
            if (miningProgress >= tileHardness)
            {
                // Kill the tile
                WorldGen.KillTile(currentTarget.X, currentTarget.Y, false, false, false);

                // Sync in multiplayer
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, currentTarget.X, currentTarget.Y);
                }

                minedCount++;
                miningProgress = 0;

                // Find next target if we haven't reached quota
                if (minedCount < targetQuantity)
                {
                    FindNextTarget();
                }
            }
        }

        /// <summary>
        /// Navigates Terra toward the current target tile.
        /// </summary>
        private void NavigateToTarget()
        {
            // Get tile position to stand at (adjacent to target)
            int standX = currentTarget.X;
            int standY = currentTarget.Y;

            // Try to find a standable position near the target
            if (!IsStandable(standX, standY))
            {
                // Check adjacent positions
                int[] offsets = { -1, 1, 0 };
                bool found = false;

                foreach (int dx in offsets)
                {
                    foreach (int dy in offsets)
                    {
                        if (IsStandable(currentTarget.X + dx, currentTarget.Y + dy))
                        {
                            standX = currentTarget.X + dx;
                            standY = currentTarget.Y + dy;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
            }

            // If not currently navigating or path is complete, compute new path
            if (!isNavigating || pathExecutor.IsComplete || pathExecutor.IsStuck)
            {
                int currentX = (int)(terra.NPC.Center.X / TileSize);
                int currentY = (int)(terra.NPC.Center.Y / TileSize);

                var path = pathfinder.FindPath(currentX, currentY, standX, standY);

                if (path != null)
                {
                    pathExecutor.SetPath(path);
                    isNavigating = true;
                }
                else
                {
                    // Can't path to target, try to find a different target
                    if (!FindNextTarget())
                    {
                        if (minedCount > 0)
                        {
                            result = ActionResult.Succeed($"Mined {minedCount}/{targetQuantity} tiles (can't reach remaining)");
                        }
                        else
                        {
                            result = ActionResult.Fail("Cannot reach any target tiles");
                        }
                    }
                    return;
                }
            }

            // Execute one tick of path following
            pathExecutor.Tick();
        }

        /// <summary>
        /// Finds the next target tile to mine within search radius.
        /// </summary>
        /// <returns>True if a target was found, false otherwise.</returns>
        private bool FindNextTarget()
        {
            int centerX = (int)(terra.NPC.Center.X / TileSize);
            int centerY = (int)(terra.NPC.Center.Y / TileSize);

            float nearestDistance = float.MaxValue;
            Point nearestTarget = Point.Zero;
            bool found = false;

            // Search in expanding squares for efficiency
            for (int radius = 1; radius <= SearchRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // Only check the outer ring of the current radius
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        int checkX = centerX + dx;
                        int checkY = centerY + dy;

                        if (IsValidTarget(new Point(checkX, checkY)))
                        {
                            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearestTarget = new Point(checkX, checkY);
                                found = true;
                            }
                        }
                    }
                }

                // If we found something in this ring, don't search further
                if (found) break;
            }

            if (found)
            {
                currentTarget = nearestTarget;
                miningProgress = 0;
                isNavigating = false;
                return true;
            }

            return false;
        }

        #endregion

        #region Tile Parsing and Lookup

        /// <summary>
        /// Parses a tile name string to a TileID constant.
        /// </summary>
        /// <param name="tileName">The name of the tile (e.g., "iron", "gold", "copper").</param>
        /// <returns>The TileID constant, or -1 if not found.</returns>
        private int ParseTileType(string tileName)
        {
            if (string.IsNullOrWhiteSpace(tileName))
                return -1;

            string lowerName = tileName.ToLowerInvariant().Trim();

            // Common ore mappings
            switch (lowerName)
            {
                // Pre-Hardmode ores
                case "copper":
                case "copper ore":
                    return TileID.Copper;
                case "tin":
                case "tin ore":
                    return TileID.Tin;
                case "iron":
                case "iron ore":
                    return TileID.Iron;
                case "lead":
                case "lead ore":
                    return TileID.Lead;
                case "silver":
                case "silver ore":
                    return TileID.Silver;
                case "tungsten":
                case "tungsten ore":
                    return TileID.Tungsten;
                case "gold":
                case "gold ore":
                    return TileID.Gold;
                case "platinum":
                case "platinum ore":
                    return TileID.Platinum;

                // Evil ores
                case "demonite":
                case "demonite ore":
                    return TileID.Demonite;
                case "crimtane":
                case "crimtane ore":
                    return TileID.Crimtane;

                // Special ores
                case "meteorite":
                case "meteor":
                    return TileID.Meteorite;
                case "obsidian":
                    return TileID.Obsidian;
                case "hellstone":
                    return TileID.Hellstone;

                // Hardmode ores
                case "cobalt":
                case "cobalt ore":
                    return TileID.Cobalt;
                case "palladium":
                case "palladium ore":
                    return TileID.Palladium;
                case "mythril":
                case "mythril ore":
                    return TileID.Mythril;
                case "orichalcum":
                case "orichalcum ore":
                    return TileID.Orichalcum;
                case "adamantite":
                case "adamantite ore":
                    return TileID.Adamantite;
                case "titanium":
                case "titanium ore":
                    return TileID.Titanium;

                // Post-Plantera ores
                case "chlorophyte":
                case "chlorophyte ore":
                    return TileID.Chlorophyte;

                // Common blocks
                case "dirt":
                    return TileID.Dirt;
                case "stone":
                    return TileID.Stone;
                case "sand":
                    return TileID.Sand;
                case "mud":
                    return TileID.Mud;
                case "clay":
                    return TileID.ClayBlock;
                case "snow":
                    return TileID.SnowBlock;
                case "ice":
                    return TileID.IceBlock;
                case "ash":
                    return TileID.Ash;

                // Gems
                case "amethyst":
                    return TileID.Amethyst;
                case "topaz":
                    return TileID.Topaz;
                case "sapphire":
                    return TileID.Sapphire;
                case "emerald":
                    return TileID.Emerald;
                case "ruby":
                    return TileID.Ruby;
                case "diamond":
                    return TileID.Diamond;

                default:
                    // Try to parse as a number (TileID directly)
                    if (int.TryParse(tileName, out int tileId))
                    {
                        return tileId;
                    }
                    return -1;
            }
        }

        /// <summary>
        /// Gets a human-readable name for a tile type.
        /// </summary>
        /// <param name="tileType">The tile type ID.</param>
        /// <returns>The tile name.</returns>
        private string GetTileName(int tileType)
        {
            switch (tileType)
            {
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
                case TileID.Obsidian: return "Obsidian";
                case TileID.Hellstone: return "Hellstone";
                case TileID.Cobalt: return "Cobalt Ore";
                case TileID.Palladium: return "Palladium Ore";
                case TileID.Mythril: return "Mythril Ore";
                case TileID.Orichalcum: return "Orichalcum Ore";
                case TileID.Adamantite: return "Adamantite Ore";
                case TileID.Titanium: return "Titanium Ore";
                case TileID.Chlorophyte: return "Chlorophyte Ore";
                case TileID.Dirt: return "Dirt";
                case TileID.Stone: return "Stone";
                case TileID.Sand: return "Sand";
                case TileID.Mud: return "Mud";
                default: return $"Tile #{tileType}";
            }
        }

        /// <summary>
        /// Gets the base hardness value for a tile type.
        /// Higher values require more mining damage to break.
        /// </summary>
        /// <param name="tileType">The tile type ID.</param>
        /// <returns>The hardness value.</returns>
        private int GetTileHardness(int tileType)
        {
            switch (tileType)
            {
                // Soft blocks
                case TileID.Dirt:
                case TileID.Sand:
                case TileID.Mud:
                case TileID.ClayBlock:
                case TileID.SnowBlock:
                case TileID.Ash:
                    return 25;

                // Standard blocks
                case TileID.Stone:
                case TileID.IceBlock:
                    return 50;

                // Pre-Hardmode ores
                case TileID.Copper:
                case TileID.Tin:
                    return 50;
                case TileID.Iron:
                case TileID.Lead:
                    return 55;
                case TileID.Silver:
                case TileID.Tungsten:
                    return 60;
                case TileID.Gold:
                case TileID.Platinum:
                    return 65;

                // Special ores
                case TileID.Demonite:
                case TileID.Crimtane:
                    return 70;
                case TileID.Meteorite:
                    return 75;
                case TileID.Obsidian:
                    return 80;
                case TileID.Hellstone:
                    return 85;

                // Hardmode ores
                case TileID.Cobalt:
                case TileID.Palladium:
                    return 100;
                case TileID.Mythril:
                case TileID.Orichalcum:
                    return 110;
                case TileID.Adamantite:
                case TileID.Titanium:
                    return 125;
                case TileID.Chlorophyte:
                    return 150;

                // Gems
                case TileID.Amethyst:
                case TileID.Topaz:
                case TileID.Sapphire:
                case TileID.Emerald:
                case TileID.Ruby:
                case TileID.Diamond:
                    return 60;

                default:
                    return 50;
            }
        }

        /// <summary>
        /// Gets the required pickaxe power to mine a tile type.
        /// </summary>
        /// <param name="tileType">The tile type ID.</param>
        /// <returns>The required pickaxe power.</returns>
        private int GetRequiredPickaxePower(int tileType)
        {
            if (PickaxePowerRequirements.TryGetValue(tileType, out int power))
            {
                return power;
            }

            // Most tiles require no special pickaxe power
            return 0;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if a point is a valid target tile.
        /// </summary>
        /// <param name="point">The tile position to check.</param>
        /// <returns>True if the tile is the target type and can be mined.</returns>
        private bool IsValidTarget(Point point)
        {
            if (point.X < 0 || point.X >= Main.maxTilesX ||
                point.Y < 0 || point.Y >= Main.maxTilesY)
            {
                return false;
            }

            Tile tile = Main.tile[point.X, point.Y];
            if (!tile.HasTile)
            {
                return false;
            }

            return tile.TileType == targetTileType;
        }

        /// <summary>
        /// Checks if a position is standable (air with solid below).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if the position is standable.</returns>
        private bool IsStandable(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            // Position must not be solid
            Tile currentTile = Main.tile[x, y];
            if (currentTile.HasTile && Main.tileSolid[currentTile.TileType])
                return false;

            // Position below must be solid
            if (y + 1 >= Main.maxTilesY)
                return false;

            Tile belowTile = Main.tile[x, y + 1];
            if (!belowTile.HasTile)
                return false;

            return Main.tileSolid[belowTile.TileType] || Main.tileSolidTop[belowTile.TileType];
        }

        /// <summary>
        /// Calculates the distance from Terra to the current target.
        /// </summary>
        /// <returns>Distance in tiles.</returns>
        private float GetDistanceToTarget()
        {
            float terraX = terra.NPC.Center.X / TileSize;
            float terraY = terra.NPC.Center.Y / TileSize;
            float dx = currentTarget.X - terraX;
            float dy = currentTarget.Y - terraY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        #endregion
    }
}
