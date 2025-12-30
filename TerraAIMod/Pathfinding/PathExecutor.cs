using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace TerraAIMod.Pathfinding
{
    /// <summary>
    /// Executes computed A* paths by controlling NPC movement.
    /// Handles different movement types like walking, jumping, climbing, swimming, etc.
    /// </summary>
    public class PathExecutor
    {
        // ==================== Constants ====================

        /// <summary>
        /// Number of ticks before considering the NPC stuck.
        /// </summary>
        private const int StuckThreshold = 60;

        /// <summary>
        /// Distance in tiles to consider a node reached.
        /// </summary>
        private const float NodeReachedDistance = 1.5f;

        /// <summary>
        /// Tile size in pixels.
        /// </summary>
        private const float TileSize = 16f;

        /// <summary>
        /// Jump velocity for standard jumps.
        /// </summary>
        private const float JumpVelocity = -5.1f;

        /// <summary>
        /// Horizontal movement speed.
        /// </summary>
        private const float WalkSpeed = 3f;

        /// <summary>
        /// Climbing speed on ropes.
        /// </summary>
        private const float ClimbSpeed = 3f;

        /// <summary>
        /// Swimming speed in liquids.
        /// </summary>
        private const float SwimSpeed = 2f;

        /// <summary>
        /// Velocity for dropping through platforms.
        /// </summary>
        private const float PlatformDropVelocity = 1f;

        // ==================== Fields ====================

        /// <summary>
        /// Reference to the Terra NPC being controlled.
        /// </summary>
        private readonly NPC terra;

        /// <summary>
        /// The current path being executed.
        /// </summary>
        private List<PathNode> currentPath;

        /// <summary>
        /// Index of the current node in the path.
        /// </summary>
        private int currentNodeIndex;

        /// <summary>
        /// Number of ticks the NPC has been stuck.
        /// </summary>
        private int stuckTicks;

        /// <summary>
        /// Last recorded position for stuck detection.
        /// </summary>
        private Vector2 lastPosition;

        // ==================== Properties ====================

        /// <summary>
        /// Returns true if the path is complete or no path is set.
        /// </summary>
        public bool IsComplete => currentPath == null || currentNodeIndex >= currentPath.Count;

        /// <summary>
        /// Returns true if the NPC is currently stuck.
        /// </summary>
        public bool IsStuck => stuckTicks >= StuckThreshold;

        /// <summary>
        /// Gets the current target node, or null if path is complete.
        /// </summary>
        public PathNode CurrentNode => IsComplete ? null : currentPath[currentNodeIndex];

        /// <summary>
        /// Gets the remaining nodes in the path.
        /// </summary>
        public int RemainingNodes => currentPath == null ? 0 : Math.Max(0, currentPath.Count - currentNodeIndex);

        // ==================== Constructor ====================

        /// <summary>
        /// Creates a new PathExecutor for the specified NPC.
        /// </summary>
        /// <param name="npc">The NPC to control.</param>
        public PathExecutor(NPC npc)
        {
            terra = npc ?? throw new ArgumentNullException(nameof(npc));
            currentPath = null;
            currentNodeIndex = 0;
            stuckTicks = 0;
            lastPosition = Vector2.Zero;
        }

        // ==================== Public Methods ====================

        /// <summary>
        /// Sets a new path to execute, resetting the index and stuck counter.
        /// </summary>
        /// <param name="path">The path to execute.</param>
        public void SetPath(List<PathNode> path)
        {
            currentPath = path;
            currentNodeIndex = 0;
            stuckTicks = 0;
            lastPosition = terra.position;
        }

        /// <summary>
        /// Clears the current path and stops execution.
        /// </summary>
        public void ClearPath()
        {
            currentPath = null;
            currentNodeIndex = 0;
            stuckTicks = 0;
        }

        /// <summary>
        /// Executes one tick of path following.
        /// </summary>
        /// <returns>True if path is complete, false if still moving or stuck.</returns>
        public bool Tick()
        {
            // Check if path is complete
            if (IsComplete)
            {
                return true;
            }

            // Detect stuck condition
            if (DetectStuck())
            {
                stuckTicks++;
                if (IsStuck)
                {
                    // NPC has been stuck too long
                    return false;
                }
            }
            else
            {
                stuckTicks = 0;
            }

            // Update last position for stuck detection
            lastPosition = terra.position;

            // Get current target node
            PathNode targetNode = currentPath[currentNodeIndex];
            Vector2 targetWorldPos = GetNodeWorldPosition(targetNode);

            // Check if we've reached the current node
            if (IsNodeReached(targetWorldPos))
            {
                currentNodeIndex++;
                if (IsComplete)
                {
                    return true;
                }
                // Get the next target node
                targetNode = currentPath[currentNodeIndex];
                targetWorldPos = GetNodeWorldPosition(targetNode);
            }

            // Execute movement based on the movement type
            ExecuteMovement(targetNode, targetWorldPos);

            return false;
        }

        // ==================== Movement Execution Methods ====================

        /// <summary>
        /// Executes the appropriate movement based on the node's movement type.
        /// </summary>
        /// <param name="node">The target path node.</param>
        /// <param name="targetPos">The target world position.</param>
        private void ExecuteMovement(PathNode node, Vector2 targetPos)
        {
            switch (node.MovementUsed)
            {
                case MovementType.Walk:
                    ExecuteWalk(targetPos);
                    break;
                case MovementType.Jump:
                case MovementType.JumpArc:
                    ExecuteJump(targetPos);
                    break;
                case MovementType.Fall:
                    ExecuteFall(targetPos);
                    break;
                case MovementType.ClimbRope:
                    ExecuteClimb(targetPos);
                    break;
                case MovementType.ClimbPlatform:
                    ExecutePlatformDrop(targetPos);
                    break;
                case MovementType.Swim:
                    ExecuteSwim(targetPos);
                    break;
                case MovementType.Grapple:
                    ExecuteGrapple(targetPos);
                    break;
                case MovementType.Teleport:
                    ExecuteTeleport(targetPos);
                    break;
                default:
                    ExecuteWalk(targetPos);
                    break;
            }
        }

        /// <summary>
        /// Executes walking movement toward the target.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteWalk(Vector2 target)
        {
            float direction = Math.Sign(target.X - terra.Center.X);
            terra.velocity.X = direction * WalkSpeed;

            // Update NPC facing direction
            terra.direction = (int)direction;
            if (terra.direction == 0)
            {
                terra.direction = 1;
            }
        }

        /// <summary>
        /// Executes a jump toward the target.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteJump(Vector2 target)
        {
            // Move horizontally toward target
            float direction = Math.Sign(target.X - terra.Center.X);
            terra.velocity.X = direction * WalkSpeed;

            // Update NPC facing direction
            terra.direction = (int)direction;
            if (terra.direction == 0)
            {
                terra.direction = 1;
            }

            // Jump if on ground
            if (IsOnGround())
            {
                terra.velocity.Y = JumpVelocity;
            }
        }

        /// <summary>
        /// Executes falling movement with horizontal control.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteFall(Vector2 target)
        {
            // Let gravity work, just control horizontal movement
            float direction = Math.Sign(target.X - terra.Center.X);
            terra.velocity.X = direction * WalkSpeed * 0.5f;

            // Update NPC facing direction
            terra.direction = (int)direction;
            if (terra.direction == 0)
            {
                terra.direction = 1;
            }
        }

        /// <summary>
        /// Executes climbing movement on ropes or vines.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteClimb(Vector2 target)
        {
            // Negate gravity effect while climbing
            terra.velocity.Y = 0;

            // Move vertically toward target
            float verticalDirection = Math.Sign(target.Y - terra.Center.Y);
            terra.velocity.Y = verticalDirection * ClimbSpeed;

            // Allow slight horizontal adjustment
            float horizontalDirection = Math.Sign(target.X - terra.Center.X);
            terra.velocity.X = horizontalDirection * ClimbSpeed * 0.3f;

            // Update NPC facing direction based on horizontal movement
            if (horizontalDirection != 0)
            {
                terra.direction = (int)horizontalDirection;
            }
        }

        /// <summary>
        /// Executes dropping through a platform.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecutePlatformDrop(Vector2 target)
        {
            // Apply small downward velocity to drop through platform
            terra.velocity.Y = PlatformDropVelocity;

            // Control horizontal movement
            float direction = Math.Sign(target.X - terra.Center.X);
            terra.velocity.X = direction * WalkSpeed * 0.5f;

            // Update NPC facing direction
            if (direction != 0)
            {
                terra.direction = (int)direction;
            }
        }

        /// <summary>
        /// Executes swimming movement in liquids.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteSwim(Vector2 target)
        {
            // Calculate direction to target
            Vector2 toTarget = target - terra.Center;
            if (toTarget.Length() > 0)
            {
                toTarget.Normalize();
            }

            // Move toward target at swim speed
            terra.velocity = toTarget * SwimSpeed;

            // Update NPC facing direction based on horizontal movement
            if (toTarget.X != 0)
            {
                terra.direction = Math.Sign(toTarget.X);
            }
        }

        /// <summary>
        /// Executes grappling hook movement toward the target.
        /// This is a simplified implementation - full grapple would require projectile spawning.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteGrapple(Vector2 target)
        {
            // Simplified grapple: move directly toward target
            // A full implementation would spawn a grapple projectile
            Vector2 toTarget = target - terra.Center;
            float distance = toTarget.Length();

            if (distance > 0)
            {
                toTarget.Normalize();
                // Grapple pulls faster than swimming
                float grappleSpeed = 6f;
                terra.velocity = toTarget * Math.Min(grappleSpeed, distance / 10f);
            }

            // Update NPC facing direction
            if (toTarget.X != 0)
            {
                terra.direction = Math.Sign(toTarget.X);
            }

            // TODO: Full grapple implementation would:
            // 1. Spawn a grappling hook projectile toward the target
            // 2. When it attaches, pull the NPC toward the attachment point
            // 3. Release when close enough to the target
        }

        /// <summary>
        /// Executes teleportation to the target.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteTeleport(Vector2 target)
        {
            // Instant teleport to target
            terra.position = target - new Vector2(terra.width / 2f, terra.height);
            terra.velocity = Vector2.Zero;
        }

        // ==================== Helper Methods ====================

        /// <summary>
        /// Checks if the NPC is standing on solid ground.
        /// </summary>
        /// <returns>True if on ground, false otherwise.</returns>
        private bool IsOnGround()
        {
            // Get tile coordinates below the NPC's feet
            int tileX = (int)(terra.Center.X / TileSize);
            int tileY = (int)((terra.position.Y + terra.height + 1) / TileSize);

            // Check if the tile below is solid
            if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY)
            {
                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    return true;
                }
            }

            // Also check velocity - if Y velocity is 0 or very small, likely on ground
            return Math.Abs(terra.velocity.Y) < 0.01f && terra.velocity.Y >= 0;
        }

        /// <summary>
        /// Converts a path node's tile coordinates to world position.
        /// </summary>
        /// <param name="node">The path node.</param>
        /// <returns>World position in pixels.</returns>
        private Vector2 GetNodeWorldPosition(PathNode node)
        {
            return new Vector2(node.X * TileSize + TileSize / 2f, node.Y * TileSize + TileSize / 2f);
        }

        /// <summary>
        /// Checks if the NPC has reached the target position.
        /// </summary>
        /// <param name="targetWorldPos">Target world position.</param>
        /// <returns>True if within NodeReachedDistance tiles of target.</returns>
        private bool IsNodeReached(Vector2 targetWorldPos)
        {
            float distanceInTiles = Vector2.Distance(terra.Center, targetWorldPos) / TileSize;
            return distanceInTiles <= NodeReachedDistance;
        }

        /// <summary>
        /// Detects if the NPC is stuck by checking if position hasn't changed.
        /// </summary>
        /// <returns>True if position hasn't changed significantly.</returns>
        private bool DetectStuck()
        {
            float distanceMoved = Vector2.Distance(terra.position, lastPosition);
            // Consider stuck if moved less than 0.1 pixels
            return distanceMoved < 0.1f;
        }
    }
}
