using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action that places a single tile or wall at a specified position.
    /// Will pathfind to within range if too far away.
    /// </summary>
    public class PlaceTileAction : BaseAction
    {
        #region Fields

        /// <summary>
        /// Target X coordinate in tile units.
        /// </summary>
        private int targetX;

        /// <summary>
        /// Target Y coordinate in tile units.
        /// </summary>
        private int targetY;

        /// <summary>
        /// The tile type ID to place.
        /// </summary>
        private int tileType;

        /// <summary>
        /// The wall type ID to place.
        /// </summary>
        private int wallType;

        /// <summary>
        /// Whether we are placing a wall instead of a tile.
        /// </summary>
        private bool isWall;

        /// <summary>
        /// Number of ticks this action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Maximum ticks before timeout (200 ticks = ~3.3 seconds at 60 TPS).
        /// </summary>
        private const int MaxTicks = 200;

        /// <summary>
        /// The pathfinder used to compute the path.
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// The executor that controls Terra's movement along the path.
        /// </summary>
        private PathExecutor pathExecutor;

        /// <summary>
        /// Whether the tile has been placed.
        /// </summary>
        private bool placed;

        /// <summary>
        /// The name of the tile being placed (for description).
        /// </summary>
        private string tileName;

        /// <summary>
        /// Maximum tile placement range in tiles.
        /// </summary>
        private const float PlacementRange = 5f;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new PlaceTileAction for the specified Terra NPC and task.
        /// </summary>
        /// <param name="terra">The Terra NPC that will execute this action.</param>
        /// <param name="task">The task containing target coordinates and tile type.</param>
        public PlaceTileAction(TerraNPC terra, Task task) : base(terra, task)
        {
            ticksRunning = 0;
            placed = false;
        }

        #endregion

        #region BaseAction Implementation

        /// <summary>
        /// A human-readable description of what this action does.
        /// </summary>
        public override string Description => $"Placing {tileName} at ({targetX}, {targetY})";

        /// <summary>
        /// Called when the action starts. Parses parameters and initializes pathfinding.
        /// </summary>
        protected override void OnStart()
        {
            // Get target coordinates from task parameters
            targetX = task.GetParameter<int>("x", 0);
            targetY = task.GetParameter<int>("y", 0);

            // Get tile name and parse to type ID
            tileName = task.GetParameter<string>("tile", "dirt");

            // Check if placing a wall
            isWall = task.HasParameter("wall");
            if (isWall)
            {
                tileName = task.GetParameter<string>("wall", "stone");
                wallType = ParseWallType(tileName);
            }
            else
            {
                tileType = ParseTileType(tileName);
            }

            // Validate target is within world bounds
            if (targetX < 0 || targetX >= Main.maxTilesX ||
                targetY < 0 || targetY >= Main.maxTilesY)
            {
                result = ActionResult.Fail($"Target position ({targetX}, {targetY}) is outside world bounds");
                return;
            }

            // Initialize pathfinder and executor
            pathfinder = new TerrariaPathfinder(terra.NPC);
            pathExecutor = new PathExecutor(terra.NPC);
        }

        /// <summary>
        /// Called each game tick while the action is running.
        /// Handles pathfinding to position and tile placement.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            if (ticksRunning >= MaxTicks)
            {
                result = ActionResult.Fail($"Tile placement timed out after {MaxTicks} ticks");
                return;
            }

            // If already placed, complete the action
            if (placed)
            {
                result = ActionResult.Succeed($"Placed {tileName} at ({targetX}, {targetY})");
                return;
            }

            // Calculate distance to target in tiles
            float terraX = terra.NPC.Center.X / 16f;
            float terraY = terra.NPC.Center.Y / 16f;
            float distanceX = targetX - terraX;
            float distanceY = targetY - terraY;
            float distance = System.Math.Abs(distanceX) + System.Math.Abs(distanceY);

            // If too far away, pathfind to position
            if (distance > PlacementRange)
            {
                // Compute path if executor doesn't have one
                if (pathExecutor.IsComplete || pathExecutor.IsStuck)
                {
                    int currentTileX = (int)terraX;
                    int currentTileY = (int)terraY;

                    // Find a path to near the target
                    List<PathNode> path = pathfinder.FindPath(currentTileX, currentTileY, targetX, targetY);
                    if (path == null)
                    {
                        // Try to get close to the target instead
                        int nearX = targetX + (distanceX > 0 ? -3 : 3);
                        int nearY = targetY;
                        path = pathfinder.FindPath(currentTileX, currentTileY, nearX, nearY);
                    }

                    if (path != null)
                    {
                        pathExecutor.SetPath(path);
                    }
                }

                // Execute path following
                pathExecutor.Tick();
            }
            else
            {
                // In range - attempt to place the tile
                PlaceTile();
            }
        }

        /// <summary>
        /// Called when the action is cancelled. Stops Terra's movement.
        /// </summary>
        protected override void OnCancel()
        {
            terra.NPC.velocity.X = 0;
            terra.NPC.velocity.Y = 0;
            pathExecutor?.ClearPath();
        }

        #endregion

        #region Tile Placement

        /// <summary>
        /// Attempts to place a tile or wall at the target position.
        /// </summary>
        private void PlaceTile()
        {
            // Validate position is in world bounds
            if (targetX < 0 || targetX >= Main.maxTilesX ||
                targetY < 0 || targetY >= Main.maxTilesY)
            {
                result = ActionResult.Fail($"Position ({targetX}, {targetY}) is outside world bounds");
                return;
            }

            Tile tile = Main.tile[targetX, targetY];

            if (isWall)
            {
                // Place wall
                WorldGen.PlaceWall(targetX, targetY, wallType);
                placed = true;

                // Sync with multiplayer
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    NetMessage.SendTileSquare(-1, targetX, targetY, 1);
                }

                result = ActionResult.Succeed($"Placed {tileName} wall at ({targetX}, {targetY})");
            }
            else
            {
                // Check if the position is air or replaceable
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    result = ActionResult.Fail($"Cannot place tile at ({targetX}, {targetY}) - position is occupied");
                    return;
                }

                // Place tile
                bool success = WorldGen.PlaceTile(targetX, targetY, tileType);

                if (success)
                {
                    placed = true;

                    // Sync with multiplayer
                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        NetMessage.SendTileSquare(-1, targetX, targetY, 1);
                    }

                    result = ActionResult.Succeed($"Placed {tileName} at ({targetX}, {targetY})");
                }
                else
                {
                    result = ActionResult.Fail($"Failed to place {tileName} at ({targetX}, {targetY})");
                }
            }
        }

        #endregion

        #region Type Parsing

        /// <summary>
        /// Parses a tile name string to its corresponding TileID.
        /// </summary>
        /// <param name="name">The name of the tile type.</param>
        /// <returns>The TileID value for the tile type.</returns>
        private int ParseTileType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return TileID.Dirt;

            switch (name.ToLower())
            {
                case "wood":
                case "woodblock":
                    return TileID.WoodBlock;
                case "stone":
                case "stoneblock":
                    return TileID.Stone;
                case "dirt":
                case "dirtblock":
                    return TileID.Dirt;
                case "platform":
                case "platforms":
                case "woodplatform":
                    return TileID.Platforms;
                case "torch":
                case "torches":
                    return TileID.Torches;
                case "sand":
                    return TileID.Sand;
                case "mud":
                    return TileID.Mud;
                case "clay":
                case "clayblock":
                    return TileID.ClayBlock;
                case "glass":
                    return TileID.Glass;
                case "obsidian":
                    return TileID.Obsidian;
                case "ash":
                case "ashblock":
                    return TileID.Ash;
                case "snow":
                case "snowblock":
                    return TileID.SnowBlock;
                case "ice":
                case "iceblock":
                    return TileID.IceBlock;
                case "graybrick":
                case "greybrick":
                case "stonebrick":
                    return TileID.GrayBrick;
                case "redbrick":
                    return TileID.RedBrick;
                case "copper":
                case "copperore":
                    return TileID.Copper;
                case "iron":
                case "ironore":
                    return TileID.Iron;
                case "silver":
                case "silverore":
                    return TileID.Silver;
                case "gold":
                case "goldore":
                    return TileID.Gold;
                case "ebonstone":
                    return TileID.Ebonstone;
                case "crimstone":
                    return TileID.Crimstone;
                case "pearlstone":
                    return TileID.Pearlstone;
                case "grass":
                    return TileID.Grass;
                case "jungle":
                case "junglegrass":
                    return TileID.JungleGrass;
                case "mushroom":
                case "mushroomgrass":
                    return TileID.MushroomGrass;
                case "chest":
                    return TileID.Containers;
                case "workbench":
                    return TileID.WorkBenches;
                case "furnace":
                    return TileID.Furnaces;
                case "anvil":
                    return TileID.Anvils;
                case "door":
                case "closeddoor":
                    return TileID.ClosedDoor;
                case "table":
                    return TileID.Tables;
                case "chair":
                    return TileID.Chairs;
                case "bed":
                    return TileID.Beds;
                case "rope":
                    return TileID.Rope;
                case "chain":
                    return TileID.Chain;
                default:
                    // Try to parse as numeric ID
                    if (int.TryParse(name, out int tileId))
                    {
                        return tileId;
                    }
                    return TileID.Dirt;
            }
        }

        /// <summary>
        /// Parses a wall name string to its corresponding WallID.
        /// </summary>
        /// <param name="name">The name of the wall type.</param>
        /// <returns>The WallID value for the wall type.</returns>
        private int ParseWallType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return WallID.Stone;

            switch (name.ToLower())
            {
                case "stone":
                case "stonewall":
                    return WallID.Stone;
                case "wood":
                case "woodwall":
                    return WallID.Wood;
                case "dirt":
                case "dirtwall":
                    return WallID.Dirt;
                case "graybrick":
                case "greybrick":
                case "graybrickwall":
                    return WallID.GrayBrick;
                case "redbrick":
                case "redbrickwall":
                    return WallID.RedBrick;
                case "bluebrick":
                    return WallID.BlueDungeon;
                case "pinkbrick":
                    return WallID.PinkDungeon;
                case "greenbrick":
                    return WallID.GreenDungeon;
                case "obsidian":
                case "obsidianbrick":
                    return WallID.ObsidianBrick;
                case "glass":
                    return WallID.Glass;
                case "pearlstone":
                case "pearlstonebrick":
                    return WallID.PearlstoneBrick;
                case "copper":
                case "copperbrick":
                    return WallID.CopperBrick;
                case "silver":
                case "silverbrick":
                    return WallID.SilverBrick;
                case "gold":
                case "goldbrick":
                    return WallID.GoldBrick;
                case "mud":
                case "mudwall":
                    return WallID.MudUnsafe;
                case "ebonstone":
                case "ebonstonebrick":
                    return WallID.EbonstoneBrick;
                case "hellstone":
                case "hellstonebrick":
                    return WallID.HellstoneBrick;
                case "planked":
                case "plankedwall":
                    return WallID.Planked;
                case "snow":
                case "snowwall":
                    return WallID.SnowWallUnsafe;
                case "ice":
                case "icewall":
                    return WallID.IceUnsafe;
                case "sandstone":
                case "sandstonewall":
                    return WallID.Sandstone;
                case "livingwood":
                    return WallID.LivingWood;
                case "grass":
                case "grasswall":
                    return WallID.GrassUnsafe;
                case "jungle":
                case "junglewall":
                    return WallID.JungleUnsafe;
                case "flower":
                case "flowerwall":
                    return WallID.FlowerUnsafe;
                case "mushroom":
                case "mushroomwall":
                    return WallID.MushroomUnsafe;
                default:
                    // Try to parse as numeric ID
                    if (int.TryParse(name, out int wallId))
                    {
                        return wallId;
                    }
                    return WallID.Stone;
            }
        }

        #endregion
    }
}
