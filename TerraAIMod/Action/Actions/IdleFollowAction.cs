using System;
using Microsoft.Xna.Framework;
using Terraria;
using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Default idle behavior when Terra has no other tasks.
    /// Follows the nearest player at a comfortable distance.
    /// Uses lightweight simple movement rather than full A* pathfinding.
    /// </summary>
    public class IdleFollowAction : BaseAction
    {
        #region Constants

        /// <summary>
        /// Distance in tiles beyond which Terra should teleport to the player.
        /// </summary>
        private const float TeleportDistance = 50f;

        /// <summary>
        /// Distance in tiles at which Terra should start walking toward the player.
        /// </summary>
        private const float FollowDistance = 4f;

        /// <summary>
        /// Distance in tiles at which Terra should stop and idle near the player.
        /// </summary>
        private const float IdleDistance = 2.5f;

        /// <summary>
        /// Tile size in pixels.
        /// </summary>
        private const float TileSize = 16f;

        /// <summary>
        /// Walking speed for idle following.
        /// </summary>
        private const float WalkSpeed = 2f;

        /// <summary>
        /// Jump velocity when blocked.
        /// </summary>
        private const float JumpVelocity = -5.1f;

        #endregion

        #region Fields

        /// <summary>
        /// The player being followed.
        /// </summary>
        private Player targetPlayer;

        /// <summary>
        /// Number of ticks this action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// How often to search for a new nearest player (in ticks).
        /// </summary>
        private int searchInterval = 100;

        /// <summary>
        /// The tick count when we last searched for a player.
        /// </summary>
        private int lastSearchTick;

        /// <summary>
        /// Reference to pathfinder (kept for potential future use, but not used for simple movement).
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// Reference to path executor (kept for potential future use, but not used for simple movement).
        /// </summary>
        private PathExecutor pathExecutor;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new idle follow action.
        /// </summary>
        /// <param name="terra">The Terra NPC.</param>
        /// <param name="task">The task (can be null for background idle).</param>
        public IdleFollowAction(TerraNPC terra, Task task) : base(terra, task)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Human-readable description of this action.
        /// </summary>
        public override string Description => "Idle (following nearest player)";

        #endregion

        #region BaseAction Overrides

        /// <summary>
        /// Called when the action starts. Finds the nearest player and initializes pathfinding components.
        /// </summary>
        protected override void OnStart()
        {
            ticksRunning = 0;
            lastSearchTick = 0;

            // Find the nearest player
            targetPlayer = FindNearestPlayer();

            // Initialize pathfinder and executor for potential future use
            pathfinder = new TerrariaPathfinder(terra.NPC);
            pathExecutor = new PathExecutor(terra.NPC);
        }

        /// <summary>
        /// Called each game tick. Never completes on its own - runs until cancelled.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Periodically refresh the nearest player
            if (ticksRunning - lastSearchTick >= searchInterval)
            {
                lastSearchTick = ticksRunning;
                targetPlayer = FindNearestPlayer();
            }

            // If no target player, just idle in place
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead)
            {
                terra.NPC.velocity.X *= 0.9f; // Slow down
                return;
            }

            // Calculate distance to target player in tiles
            float distancePixels = Vector2.Distance(terra.NPC.Center, targetPlayer.Center);
            float distanceTiles = distancePixels / TileSize;

            // Update facing direction to look at player
            LookAtPlayer();

            // Determine behavior based on distance
            if (distanceTiles > TeleportDistance)
            {
                // Player moved too far - teleport near them
                TeleportNearPlayer();
            }
            else if (distanceTiles > FollowDistance)
            {
                // Walk toward player
                WalkTowardPlayer();
            }
            else if (distanceTiles < IdleDistance)
            {
                // Stop moving and face player
                StopMoving();
            }
            // Between IdleDistance and FollowDistance: do nothing special, just maintain position
        }

        /// <summary>
        /// Called when the action is cancelled. Stops all movement.
        /// </summary>
        protected override void OnCancel()
        {
            StopMoving();
        }

        #endregion

        #region Player Finding

        /// <summary>
        /// Finds the nearest active player.
        /// </summary>
        /// <returns>The nearest player, or null if none found.</returns>
        private Player FindNearestPlayer()
        {
            float nearestDistance = float.MaxValue;
            Player nearestPlayer = null;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    float distance = Vector2.Distance(terra.NPC.Center, player.Center);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPlayer = player;
                    }
                }
            }

            return nearestPlayer;
        }

        #endregion

        #region Movement Methods

        /// <summary>
        /// Teleports Terra near the target player when they've moved too far.
        /// </summary>
        private void TeleportNearPlayer()
        {
            if (targetPlayer == null)
                return;

            // Try to find ground near the player
            Vector2 teleportPos = FindGroundNear(targetPlayer.Center);

            // Teleport Terra to that position
            terra.NPC.position = teleportPos - new Vector2(terra.NPC.width / 2f, terra.NPC.height);
            terra.NPC.velocity = Vector2.Zero;

            // Create a dust effect for visual feedback
            for (int i = 0; i < 10; i++)
            {
                Dust.NewDust(terra.NPC.position, terra.NPC.width, terra.NPC.height, Terraria.ID.DustID.MagicMirror);
            }
        }

        /// <summary>
        /// Finds a valid ground position near the specified position.
        /// Searches downward for a solid tile.
        /// </summary>
        /// <param name="position">The position to search near.</param>
        /// <returns>A valid ground position in world coordinates.</returns>
        private Vector2 FindGroundNear(Vector2 position)
        {
            // Convert to tile coordinates
            int tileX = (int)(position.X / TileSize);
            int tileY = (int)(position.Y / TileSize);

            // Try positions around the target (left and right)
            int[] offsets = { -2, 2, -3, 3, -1, 1, 0 };

            foreach (int offsetX in offsets)
            {
                int checkX = tileX + offsetX;

                // Search downward for ground
                for (int checkY = tileY - 5; checkY < tileY + 20; checkY++)
                {
                    if (checkX < 0 || checkX >= Main.maxTilesX || checkY < 0 || checkY >= Main.maxTilesY)
                        continue;

                    // Check if this position is air with solid ground below
                    Tile tileHere = Main.tile[checkX, checkY];
                    Tile tileBelow = (checkY + 1 < Main.maxTilesY) ? Main.tile[checkX, checkY + 1] : default;
                    Tile tileAbove = (checkY - 1 >= 0) ? Main.tile[checkX, checkY - 1] : default;

                    bool isAir = !tileHere.HasTile || !Main.tileSolid[tileHere.TileType];
                    bool hasGround = (checkY + 1 < Main.maxTilesY) && tileBelow.HasTile && Main.tileSolid[tileBelow.TileType];
                    bool hasHeadroom = !tileAbove.HasTile || !Main.tileSolid[tileAbove.TileType];

                    if (isAir && hasGround && hasHeadroom)
                    {
                        // Found valid ground position
                        return new Vector2(checkX * TileSize + TileSize / 2f, checkY * TileSize);
                    }
                }
            }

            // Fallback: just return slightly above the target position
            return position - new Vector2(0, TileSize * 2);
        }

        /// <summary>
        /// Simple movement toward the player without full A* pathfinding.
        /// Sets velocity toward player and jumps if blocked.
        /// </summary>
        private void WalkTowardPlayer()
        {
            if (targetPlayer == null)
                return;

            // Determine direction to player
            float direction = Math.Sign(targetPlayer.Center.X - terra.NPC.Center.X);

            // Set horizontal velocity
            terra.NPC.velocity.X = direction * WalkSpeed;

            // Update facing direction
            terra.NPC.direction = (int)direction;
            if (terra.NPC.direction == 0)
            {
                terra.NPC.direction = 1;
            }
            terra.NPC.spriteDirection = terra.NPC.direction;

            // Check if blocked and need to jump
            if (IsBlocked() && IsOnGround())
            {
                terra.NPC.velocity.Y = JumpVelocity;
            }
        }

        /// <summary>
        /// Checks if there's a solid tile blocking Terra's path ahead.
        /// </summary>
        /// <returns>True if Terra is blocked by a solid tile.</returns>
        private bool IsBlocked()
        {
            // Check the tile in front of Terra at feet level
            int direction = terra.NPC.direction;
            int tileX = (int)((terra.NPC.Center.X + direction * (terra.NPC.width / 2f + 8)) / TileSize);
            int tileY = (int)((terra.NPC.position.Y + terra.NPC.height - 1) / TileSize);

            if (tileX < 0 || tileX >= Main.maxTilesX || tileY < 0 || tileY >= Main.maxTilesY)
                return true;

            Tile tile = Main.tile[tileX, tileY];
            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
            {
                return true;
            }

            // Also check the tile at body level
            int bodyTileY = (int)(terra.NPC.Center.Y / TileSize);
            if (bodyTileY >= 0 && bodyTileY < Main.maxTilesY)
            {
                Tile bodyTile = Main.tile[tileX, bodyTileY];
                if (bodyTile.HasTile && Main.tileSolid[bodyTile.TileType] && !Main.tileSolidTop[bodyTile.TileType])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if Terra is standing on solid ground.
        /// </summary>
        /// <returns>True if on ground.</returns>
        private bool IsOnGround()
        {
            // Check tiles beneath Terra's feet
            int leftTileX = (int)(terra.NPC.position.X / TileSize);
            int rightTileX = (int)((terra.NPC.position.X + terra.NPC.width) / TileSize);
            int bottomTileY = (int)((terra.NPC.position.Y + terra.NPC.height + 1) / TileSize);

            for (int x = leftTileX; x <= rightTileX; x++)
            {
                if (x < 0 || x >= Main.maxTilesX || bottomTileY < 0 || bottomTileY >= Main.maxTilesY)
                    continue;

                Tile tile = Main.tile[x, bottomTileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    return true;
                }
            }

            // Also consider being on ground if Y velocity is near zero
            return Math.Abs(terra.NPC.velocity.Y) < 0.01f && terra.NPC.velocity.Y >= 0;
        }

        /// <summary>
        /// Stops Terra's horizontal movement.
        /// </summary>
        private void StopMoving()
        {
            terra.NPC.velocity.X *= 0.8f; // Gradual slowdown for smoother stopping

            // Stop completely if very slow
            if (Math.Abs(terra.NPC.velocity.X) < 0.1f)
            {
                terra.NPC.velocity.X = 0;
            }
        }

        /// <summary>
        /// Updates Terra's direction to face the target player.
        /// </summary>
        private void LookAtPlayer()
        {
            if (targetPlayer == null)
                return;

            // Face the player
            if (targetPlayer.Center.X > terra.NPC.Center.X)
            {
                terra.NPC.direction = 1;
            }
            else
            {
                terra.NPC.direction = -1;
            }

            terra.NPC.spriteDirection = terra.NPC.direction;
        }

        #endregion
    }
}
