using System;
using Microsoft.Xna.Framework;
using TerraAIMod.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action for digging in a direction - creates hellevators (down) or tunnels (left/right/up).
    /// For hellevator (down), creates a 2-wide shaft.
    /// For horizontal tunnels, creates a 3-tall passage.
    /// </summary>
    public class DigAction : BaseAction
    {
        #region Constants

        /// <summary>
        /// Default distance for hellevator (vertical down) digging.
        /// </summary>
        private const int DefaultHellevatorDistance = 100;

        /// <summary>
        /// Default distance for horizontal or upward tunnels.
        /// </summary>
        private const int DefaultHorizontalDistance = 50;

        /// <summary>
        /// Ticks multiplier for timeout calculation (distance * this value).
        /// </summary>
        private const int TimeoutTicksPerBlock = 120;

        /// <summary>
        /// Interval for placing torches (every N tiles).
        /// </summary>
        private const int TorchInterval = 10;

        /// <summary>
        /// Mining speed - ticks required to mine a tile (simulates pickaxe power).
        /// </summary>
        private const int MiningTicksPerTile = 10;

        /// <summary>
        /// Movement speed toward target.
        /// </summary>
        private const float MoveSpeed = 3f;

        #endregion

        #region Fields

        /// <summary>
        /// The direction to dig: "down", "up", "left", "right".
        /// </summary>
        private string direction;

        /// <summary>
        /// Total distance to dig in tiles.
        /// </summary>
        private int distance;

        /// <summary>
        /// Number of tiles successfully dug.
        /// </summary>
        private int dugCount;

        /// <summary>
        /// Number of ticks the action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Current target tile position to mine.
        /// </summary>
        private Point currentTarget;

        /// <summary>
        /// Progress toward mining the current tile (0 to MiningTicksPerTile).
        /// </summary>
        private int miningProgress;

        /// <summary>
        /// Starting position when the dig action began (in tile coordinates).
        /// </summary>
        private Point startPosition;

        /// <summary>
        /// Tracks tiles dug since last torch placement.
        /// </summary>
        private int tilesSinceTorch;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new DigAction for the specified Terra NPC and task.
        /// </summary>
        /// <param name="terra">The Terra NPC executing this action.</param>
        /// <param name="task">The task containing dig parameters.</param>
        public DigAction(TerraNPC terra, Task task) : base(terra, task)
        {
            dugCount = 0;
            ticksRunning = 0;
            miningProgress = 0;
            tilesSinceTorch = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Human-readable description of the dig action.
        /// </summary>
        public override string Description => $"Digging {direction} ({dugCount}/{distance})";

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called when the dig action starts.
        /// Initializes direction, distance, and sets the first target.
        /// </summary>
        protected override void OnStart()
        {
            // Get direction from parameters, default to "down" (hellevator)
            direction = task.GetParameter<string>("direction", "down").ToLowerInvariant();

            // Validate direction
            if (direction != "down" && direction != "up" && direction != "left" && direction != "right")
            {
                direction = "down";
            }

            // Get distance from parameters, default based on direction
            int defaultDistance = (direction == "down") ? DefaultHellevatorDistance : DefaultHorizontalDistance;
            distance = task.GetParameter<int>("distance", defaultDistance);

            // Ensure distance is reasonable
            if (distance <= 0)
            {
                distance = defaultDistance;
            }

            // Record starting position in tile coordinates
            int tileX = (int)(terra.NPC.Center.X / 16f);
            int tileY = (int)(terra.NPC.Center.Y / 16f);
            startPosition = new Point(tileX, tileY);

            // Set the first target tile based on direction
            currentTarget = GetFirstTarget();

            TerraAIMod.Instance?.Logger.Info($"Terra starting dig action: direction={direction}, distance={distance}, start=({startPosition.X},{startPosition.Y})");
        }

        /// <summary>
        /// Called each game tick while the dig action is running.
        /// Handles timeout, completion, mining, and movement.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            int timeoutTicks = distance * TimeoutTicksPerBlock;
            if (ticksRunning > timeoutTicks)
            {
                result = ActionResult.Fail($"Dig action timed out after {ticksRunning} ticks (dug {dugCount}/{distance} tiles)");
                return;
            }

            // Check for completion
            if (dugCount >= distance)
            {
                result = ActionResult.Succeed($"Successfully dug {dugCount} tiles {direction}");
                return;
            }

            // Validate current target is within world bounds
            if (!IsValidTilePosition(currentTarget))
            {
                result = ActionResult.Fail($"Dig action stopped: reached world boundary at ({currentTarget.X},{currentTarget.Y})");
                return;
            }

            // Get the tile at current target
            Tile tile = Main.tile[currentTarget.X, currentTarget.Y];

            // If the target tile is air (or already broken), advance to next target
            if (!tile.HasTile || !IsSolidTile(tile))
            {
                AdvanceTarget();
                dugCount++; // Count air tiles as "dug" for progress
                tilesSinceTorch++;
                return;
            }

            // Move Terra toward the current target
            MoveTowardTarget();

            // Check if Terra is close enough to mine
            float distanceToTarget = GetDistanceToTarget();
            if (distanceToTarget < 64f) // Within 4 tiles
            {
                // Mine the current tile
                MineCurrentTile();
            }
        }

        /// <summary>
        /// Called when the dig action is cancelled.
        /// </summary>
        protected override void OnCancel()
        {
            TerraAIMod.Instance?.Logger.Info($"Dig action cancelled after digging {dugCount}/{distance} tiles {direction}");
        }

        #endregion

        #region Mining Methods

        /// <summary>
        /// Applies mining progress to the current target tile.
        /// When progress completes, breaks the tile and advances.
        /// </summary>
        private void MineCurrentTile()
        {
            miningProgress++;

            // Create mining dust/particles for visual feedback
            if (miningProgress % 3 == 0)
            {
                Tile tile = Main.tile[currentTarget.X, currentTarget.Y];
                if (tile.HasTile)
                {
                    WorldGen.KillTile_MakeTileDust(currentTarget.X, currentTarget.Y, tile);
                }
            }

            // Check if mining is complete for this tile
            if (miningProgress >= MiningTicksPerTile)
            {
                // Break the tile
                WorldGen.KillTile(currentTarget.X, currentTarget.Y, false, false, false);

                // Sync in multiplayer
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, currentTarget.X, currentTarget.Y);
                }

                // Increment dug count
                dugCount++;
                tilesSinceTorch++;

                // Mine additional tiles for width/height based on direction
                MineAdjacentTiles();

                // Check if we should place a torch
                if (direction == "down" && tilesSinceTorch >= TorchInterval)
                {
                    PlaceTorch();
                    tilesSinceTorch = 0;
                }

                // Reset mining progress and advance to next target
                miningProgress = 0;
                AdvanceTarget();
            }
        }

        /// <summary>
        /// Mines adjacent tiles to create proper tunnel width/height.
        /// Hellevator (down): 2-wide shaft
        /// Horizontal/Up: 3-tall tunnel
        /// </summary>
        private void MineAdjacentTiles()
        {
            if (direction == "down" || direction == "up")
            {
                // Mine tile to the right for 2-wide shaft
                int adjacentX = currentTarget.X + 1;
                if (IsValidTilePosition(new Point(adjacentX, currentTarget.Y)))
                {
                    Tile adjacentTile = Main.tile[adjacentX, currentTarget.Y];
                    if (adjacentTile.HasTile && IsSolidTile(adjacentTile))
                    {
                        WorldGen.KillTile(adjacentX, currentTarget.Y, false, false, false);

                        if (Main.netMode != NetmodeID.SinglePlayer)
                        {
                            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, adjacentX, currentTarget.Y);
                        }
                    }
                }
            }
            else // left or right - 3-tall tunnel
            {
                // Mine tile above
                int aboveY = currentTarget.Y - 1;
                if (IsValidTilePosition(new Point(currentTarget.X, aboveY)))
                {
                    Tile aboveTile = Main.tile[currentTarget.X, aboveY];
                    if (aboveTile.HasTile && IsSolidTile(aboveTile))
                    {
                        WorldGen.KillTile(currentTarget.X, aboveY, false, false, false);

                        if (Main.netMode != NetmodeID.SinglePlayer)
                        {
                            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, currentTarget.X, aboveY);
                        }
                    }
                }

                // Mine tile below
                int belowY = currentTarget.Y + 1;
                if (IsValidTilePosition(new Point(currentTarget.X, belowY)))
                {
                    Tile belowTile = Main.tile[currentTarget.X, belowY];
                    if (belowTile.HasTile && IsSolidTile(belowTile))
                    {
                        WorldGen.KillTile(currentTarget.X, belowY, false, false, false);

                        if (Main.netMode != NetmodeID.SinglePlayer)
                        {
                            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, currentTarget.X, belowY);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Places a torch on the wall for light in hellevators.
        /// </summary>
        private void PlaceTorch()
        {
            // Place torch on the left wall of the shaft
            int torchX = currentTarget.X - 1;
            int torchY = currentTarget.Y;

            if (!IsValidTilePosition(new Point(torchX, torchY)))
                return;

            Tile wallTile = Main.tile[torchX, torchY];

            // Only place if there's a wall to attach to and no tile blocking
            if (wallTile.WallType > WallID.None && !wallTile.HasTile)
            {
                // Place a torch
                WorldGen.PlaceTile(torchX, torchY, TileID.Torches, false, false, -1, 0);

                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 1, torchX, torchY, TileID.Torches);
                }
            }
            else
            {
                // Try placing in the shaft itself on the wall
                if (!Main.tile[currentTarget.X, torchY].HasTile && Main.tile[currentTarget.X, torchY].WallType > WallID.None)
                {
                    WorldGen.PlaceTile(currentTarget.X, torchY, TileID.Torches, false, false, -1, 0);

                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 1, currentTarget.X, torchY, TileID.Torches);
                    }
                }
            }
        }

        #endregion

        #region Target Management

        /// <summary>
        /// Gets the first target tile based on direction from Terra's position.
        /// </summary>
        /// <returns>The first target tile position.</returns>
        private Point GetFirstTarget()
        {
            switch (direction)
            {
                case "down":
                    return new Point(startPosition.X, startPosition.Y + 2); // Start below feet
                case "up":
                    return new Point(startPosition.X, startPosition.Y - 2); // Start above head
                case "left":
                    return new Point(startPosition.X - 1, startPosition.Y);
                case "right":
                    return new Point(startPosition.X + 1, startPosition.Y);
                default:
                    return new Point(startPosition.X, startPosition.Y + 2);
            }
        }

        /// <summary>
        /// Advances the current target to the next tile in the dig direction.
        /// </summary>
        private void AdvanceTarget()
        {
            switch (direction)
            {
                case "down":
                    currentTarget.Y++;
                    break;
                case "up":
                    currentTarget.Y--;
                    break;
                case "left":
                    currentTarget.X--;
                    break;
                case "right":
                    currentTarget.X++;
                    break;
            }
        }

        #endregion

        #region Movement Methods

        /// <summary>
        /// Moves Terra toward the current target tile.
        /// Simple movement - walk/fall toward target.
        /// </summary>
        private void MoveTowardTarget()
        {
            // Calculate target world position (center of target tile)
            Vector2 targetWorldPos = new Vector2(
                currentTarget.X * 16f + 8f,
                currentTarget.Y * 16f + 8f
            );

            Vector2 terraPos = terra.NPC.Center;
            Vector2 diff = targetWorldPos - terraPos;

            // Horizontal movement
            if (Math.Abs(diff.X) > 8f)
            {
                terra.NPC.velocity.X = diff.X > 0 ? MoveSpeed : -MoveSpeed;
            }
            else
            {
                terra.NPC.velocity.X = 0;
            }

            // Vertical movement for "up" direction - need to jump
            if (direction == "up" && diff.Y < -16f)
            {
                // Check if on ground and need to jump
                if (IsOnGround())
                {
                    terra.NPC.velocity.Y = -8f; // Jump
                }
            }

            // For "down" direction, just let gravity do its work
            // Terra will fall down the hellevator naturally
        }

        /// <summary>
        /// Gets the distance from Terra to the current target tile.
        /// </summary>
        /// <returns>Distance in world units (pixels).</returns>
        private float GetDistanceToTarget()
        {
            Vector2 targetWorldPos = new Vector2(
                currentTarget.X * 16f + 8f,
                currentTarget.Y * 16f + 8f
            );

            return Vector2.Distance(terra.NPC.Center, targetWorldPos);
        }

        /// <summary>
        /// Checks if Terra is standing on solid ground.
        /// </summary>
        /// <returns>True if on ground.</returns>
        private bool IsOnGround()
        {
            int leftTileX = (int)(terra.NPC.position.X / 16f);
            int rightTileX = (int)((terra.NPC.position.X + terra.NPC.width) / 16f);
            int bottomTileY = (int)((terra.NPC.position.Y + terra.NPC.height + 1) / 16f);

            for (int x = leftTileX; x <= rightTileX; x++)
            {
                if (!IsValidTilePosition(new Point(x, bottomTileY)))
                    continue;

                Tile tile = Main.tile[x, bottomTileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if a tile position is within world bounds.
        /// </summary>
        /// <param name="pos">The tile position to check.</param>
        /// <returns>True if valid.</returns>
        private bool IsValidTilePosition(Point pos)
        {
            return pos.X >= 0 && pos.X < Main.maxTilesX &&
                   pos.Y >= 0 && pos.Y < Main.maxTilesY;
        }

        /// <summary>
        /// Checks if a tile is solid and mineable.
        /// </summary>
        /// <param name="tile">The tile to check.</param>
        /// <returns>True if solid and can be mined.</returns>
        private bool IsSolidTile(Tile tile)
        {
            if (!tile.HasTile)
                return false;

            // Check if it's a solid tile
            return Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
        }

        #endregion
    }
}
