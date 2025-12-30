using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action that makes Terra follow a specific player.
    /// Supports following by name or finding the nearest player.
    /// </summary>
    public class FollowPlayerAction : BaseAction
    {
        #region Constants

        /// <summary>
        /// Tile size in pixels.
        /// </summary>
        private const float TileSize = 16f;

        /// <summary>
        /// Distance in tiles at which Terra will teleport to the player instead of walking.
        /// </summary>
        private const float TeleportDistanceTiles = 50f;

        /// <summary>
        /// Distance in tiles at which Terra will stop and consider the follow complete/wait.
        /// </summary>
        private const float StopDistanceTiles = 2f;

        /// <summary>
        /// Distance in tiles at which Terra will start pathfinding to the player.
        /// </summary>
        private const float PathfindDistanceTiles = 4f;

        /// <summary>
        /// Number of ticks between path recalculations.
        /// </summary>
        private const int RepathIntervalTicks = 60;

        #endregion

        #region Fields

        /// <summary>
        /// The name of the player to follow.
        /// </summary>
        private string targetPlayerName;

        /// <summary>
        /// Reference to the target player.
        /// </summary>
        private Player targetPlayer;

        /// <summary>
        /// Number of ticks this action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Maximum duration in ticks before the action times out.
        /// </summary>
        private int maxDuration = 6000;

        /// <summary>
        /// Pathfinder for calculating paths to the player.
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// Path executor for following calculated paths.
        /// </summary>
        private PathExecutor pathExecutor;

        /// <summary>
        /// Tick count when the last path was calculated.
        /// </summary>
        private int lastPathTime;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new FollowPlayerAction for the specified Terra and task.
        /// </summary>
        /// <param name="terra">The Terra NPC that will follow the player.</param>
        /// <param name="task">The task containing follow parameters.</param>
        public FollowPlayerAction(TerraNPC terra, Task task) : base(terra, task)
        {
        }

        #endregion

        #region BaseAction Overrides

        /// <summary>
        /// Gets a description of this action.
        /// </summary>
        public override string Description => $"Following {targetPlayerName ?? "player"}";

        /// <summary>
        /// Called when the action starts. Initializes pathfinding and finds the target player.
        /// </summary>
        protected override void OnStart()
        {
            // Get player name from task parameters, default to "nearest"
            targetPlayerName = task.GetParameter<string>("playerName", "nearest");
            if (string.IsNullOrEmpty(targetPlayerName))
            {
                targetPlayerName = "nearest";
            }

            // Find the target player
            targetPlayer = FindPlayer(targetPlayerName);

            if (targetPlayer == null)
            {
                result = ActionResult.Fail($"Could not find player '{targetPlayerName}'");
                return;
            }

            // Update the target player name with the actual player's name
            targetPlayerName = targetPlayer.name;

            // Initialize pathfinding
            pathfinder = new TerrariaPathfinder(terra.NPC);
            pathExecutor = new PathExecutor(terra.NPC);

            // Set Terra's target player index
            terra.TargetPlayerIndex = targetPlayer.whoAmI;

            ticksRunning = 0;
            lastPathTime = -RepathIntervalTicks; // Force immediate pathfinding
        }

        /// <summary>
        /// Called each game tick. Updates pathfinding and movement toward the player.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            if (ticksRunning >= maxDuration)
            {
                result = ActionResult.Succeed($"Finished following {targetPlayerName} (timeout)");
                return;
            }

            // Validate player is still active
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead)
            {
                // Try to find the player again
                targetPlayer = FindPlayer(targetPlayerName);

                if (targetPlayer == null)
                {
                    result = ActionResult.Fail($"Player '{targetPlayerName}' is no longer available");
                    return;
                }

                // Update Terra's target index
                terra.TargetPlayerIndex = targetPlayer.whoAmI;
            }

            // Calculate distance to player in tiles
            float distancePixels = Vector2.Distance(terra.NPC.Center, targetPlayer.Center);
            float distanceTiles = distancePixels / TileSize;

            // If too far, teleport near player
            if (distanceTiles > TeleportDistanceTiles)
            {
                TeleportNearPlayer();
                return;
            }

            // If close enough, stop and wait
            if (distanceTiles < StopDistanceTiles)
            {
                // Stop movement
                terra.NPC.velocity.X = 0;
                pathExecutor.ClearPath();

                // Continue following (don't complete the action)
                // The action will keep running and resume following if the player moves away
                return;
            }

            // If within pathfinding range, calculate and follow path
            if (distanceTiles > PathfindDistanceTiles)
            {
                // Check if we need to recalculate the path
                if (ticksRunning - lastPathTime >= RepathIntervalTicks || pathExecutor.IsComplete || pathExecutor.IsStuck)
                {
                    CalculatePathToPlayer();
                    lastPathTime = ticksRunning;
                }
            }
            else
            {
                // Close enough to just walk toward player without pathfinding
                float direction = Math.Sign(targetPlayer.Center.X - terra.NPC.Center.X);
                terra.NPC.velocity.X = direction * 3f;
                terra.NPC.direction = (int)direction;
                if (terra.NPC.direction == 0)
                {
                    terra.NPC.direction = 1;
                }
                terra.NPC.spriteDirection = terra.NPC.direction;

                // Check if blocked and need to jump
                if (IsBlockedAhead() && IsOnGround())
                {
                    terra.NPC.velocity.Y = -5.1f; // Jump velocity
                }
            }

            // Execute the current path
            pathExecutor.Tick();
        }

        /// <summary>
        /// Called when the action is cancelled. Clears the target and stops movement.
        /// </summary>
        protected override void OnCancel()
        {
            // Clear target player
            terra.TargetPlayerIndex = -1;

            // Stop movement
            terra.NPC.velocity.X = 0;

            // Clear pathfinding
            pathExecutor?.ClearPath();
        }

        #endregion

        #region Player Finding Methods

        /// <summary>
        /// Finds a player by name or returns the nearest player if name is "nearest".
        /// </summary>
        /// <param name="name">The player name to search for, or "nearest" for the closest player.</param>
        /// <returns>The found player, or null if not found.</returns>
        private Player FindPlayer(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Equals("nearest", StringComparison.OrdinalIgnoreCase))
            {
                return FindNearestPlayer();
            }

            // Search for player by name (case-insensitive)
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    if (player.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return player;
                    }
                }
            }

            // If exact match not found, try partial match
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    if (player.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return player;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the nearest active player to Terra.
        /// </summary>
        /// <returns>The nearest player, or null if no players are available.</returns>
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

        #region Teleportation Methods

        /// <summary>
        /// Teleports Terra to a safe spot near the target player.
        /// </summary>
        private void TeleportNearPlayer()
        {
            if (targetPlayer == null)
                return;

            // Find a safe spot near the player
            Vector2 safeSpot = FindSafeSpot(targetPlayer.Center);

            // Teleport Terra to the safe spot
            terra.NPC.position = safeSpot - new Vector2(terra.NPC.width / 2f, terra.NPC.height);
            terra.NPC.velocity = Vector2.Zero;

            // Clear the current path since we teleported
            pathExecutor.ClearPath();
            lastPathTime = ticksRunning - RepathIntervalTicks; // Force immediate pathfinding
        }

        /// <summary>
        /// Finds a safe (standable) spot near the specified position.
        /// Searches in a spiral pattern around the target position.
        /// </summary>
        /// <param name="near">The target position to search near.</param>
        /// <returns>A safe world position, or the original position if none found.</returns>
        private Vector2 FindSafeSpot(Vector2 near)
        {
            int centerTileX = (int)(near.X / TileSize);
            int centerTileY = (int)(near.Y / TileSize);

            // Search in expanding squares around the target
            for (int radius = 1; radius <= 10; radius++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    for (int offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        // Only check the perimeter of the current square
                        if (Math.Abs(offsetX) != radius && Math.Abs(offsetY) != radius)
                            continue;

                        int tileX = centerTileX + offsetX;
                        int tileY = centerTileY + offsetY;

                        if (IsStandable(tileX, tileY))
                        {
                            // Return world position (center of tile)
                            return new Vector2(
                                tileX * TileSize + TileSize / 2f,
                                tileY * TileSize + TileSize / 2f
                            );
                        }
                    }
                }
            }

            // Fallback: check directly at player position
            if (IsStandable(centerTileX, centerTileY))
            {
                return near;
            }

            // Check directly above player
            for (int y = centerTileY - 1; y >= centerTileY - 5; y--)
            {
                if (IsStandable(centerTileX, y))
                {
                    return new Vector2(
                        centerTileX * TileSize + TileSize / 2f,
                        y * TileSize + TileSize / 2f
                    );
                }
            }

            // No safe spot found, return original position
            return near;
        }

        /// <summary>
        /// Checks if a tile position is standable (has solid ground below and is not blocked).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if the position is standable.</returns>
        private bool IsStandable(int x, int y)
        {
            // Check bounds
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            // The tile and the one above must not be solid (for player height)
            Tile currentTile = Main.tile[x, y];
            Tile aboveTile = Main.tile[x, y - 1];

            if (currentTile.HasTile && Main.tileSolid[currentTile.TileType] && !Main.tileSolidTop[currentTile.TileType])
                return false;

            if (aboveTile.HasTile && Main.tileSolid[aboveTile.TileType] && !Main.tileSolidTop[aboveTile.TileType])
                return false;

            // Check for solid ground below
            if (y + 1 >= Main.maxTilesY)
                return false;

            Tile belowTile = Main.tile[x, y + 1];
            if (!belowTile.HasTile)
                return false;

            return Main.tileSolid[belowTile.TileType];
        }

        #endregion

        #region Pathfinding Methods

        /// <summary>
        /// Calculates a new path from Terra's current position to the target player.
        /// </summary>
        private void CalculatePathToPlayer()
        {
            if (targetPlayer == null || pathfinder == null)
                return;

            // Get tile coordinates
            int startX = (int)(terra.NPC.Center.X / TileSize);
            int startY = (int)(terra.NPC.Center.Y / TileSize);
            int goalX = (int)(targetPlayer.Center.X / TileSize);
            int goalY = (int)(targetPlayer.Center.Y / TileSize);

            // Calculate path
            List<PathNode> path = pathfinder.FindPath(startX, startY, goalX, goalY);

            if (path != null && path.Count > 0)
            {
                pathExecutor.SetPath(path);
            }
            else
            {
                // No path found, try to move directly toward player
                float direction = Math.Sign(targetPlayer.Center.X - terra.NPC.Center.X);
                terra.NPC.velocity.X = direction * 3f;
            }
        }

        #endregion

        #region Movement Helper Methods

        /// <summary>
        /// Checks if there's a solid tile blocking Terra's path ahead.
        /// </summary>
        /// <returns>True if Terra is blocked by a solid tile.</returns>
        private bool IsBlockedAhead()
        {
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

        #endregion
    }
}
