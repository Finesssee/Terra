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
        /// Number of stuck recovery attempts before abandoning path.
        /// </summary>
        private const int MaxStuckRecoveryAttempts = 3;

        /// <summary>
        /// Distance in tiles to consider a node reached (walking).
        /// </summary>
        private const float NodeReachedDistanceWalk = 1.0f;

        /// <summary>
        /// Distance in tiles to consider a node reached (jumping/falling).
        /// </summary>
        private const float NodeReachedDistanceAir = 2.0f;

        /// <summary>
        /// Distance in tiles to consider a node reached (vertical movement).
        /// </summary>
        private const float NodeReachedDistanceVertical = 1.5f;

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

        /// <summary>
        /// Gravity acceleration per tick (matching TerrariaPathfinder).
        /// </summary>
        private const float Gravity = 0.4f;

        /// <summary>
        /// Maximum fall speed (terminal velocity).
        /// </summary>
        private const float MaxFallSpeed = 10f;

        /// <summary>
        /// Minimum distance moved per stuck check period to be considered not stuck.
        /// </summary>
        private const float MinMovementThreshold = 0.5f;

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

        /// <summary>
        /// Number of stuck recovery attempts made for current node.
        /// </summary>
        private int stuckRecoveryAttempts;

        /// <summary>
        /// Tracks if we're currently in a jump (initiated but haven't landed yet).
        /// </summary>
        private bool isJumping;

        /// <summary>
        /// Tracks ticks spent waiting for ground to initiate a jump.
        /// </summary>
        private int jumpWaitTicks;

        /// <summary>
        /// Maximum ticks to wait for ground before attempting recovery.
        /// </summary>
        private const int MaxJumpWaitTicks = 30;

        /// <summary>
        /// Tracks accumulated movement for stuck detection (checked periodically).
        /// </summary>
        private float accumulatedMovement;

        /// <summary>
        /// Ticks since last stuck check.
        /// </summary>
        private int ticksSinceStuckCheck;

        /// <summary>
        /// Interval for stuck checking (in ticks).
        /// </summary>
        private const int StuckCheckInterval = 10;

        // ==================== Properties ====================

        /// <summary>
        /// Returns true if the path is complete or no path is set.
        /// </summary>
        public bool IsComplete => currentPath == null || currentNodeIndex >= currentPath.Count;

        /// <summary>
        /// Returns true if the NPC is currently stuck and recovery has failed.
        /// </summary>
        public bool IsStuck => stuckTicks >= StuckThreshold && stuckRecoveryAttempts >= MaxStuckRecoveryAttempts;

        /// <summary>
        /// Returns true if the NPC is temporarily stuck but recovery is being attempted.
        /// </summary>
        public bool IsRecoveringFromStuck => stuckTicks >= StuckThreshold && stuckRecoveryAttempts < MaxStuckRecoveryAttempts;

        /// <summary>
        /// Gets the current target node, or null if path is complete.
        /// </summary>
        public PathNode CurrentNode => IsComplete ? null : currentPath[currentNodeIndex];

        /// <summary>
        /// Gets the remaining nodes in the path.
        /// </summary>
        public int RemainingNodes => currentPath == null ? 0 : Math.Max(0, currentPath.Count - currentNodeIndex);

        /// <summary>
        /// Gets the current progress percentage (0-100).
        /// </summary>
        public float Progress => currentPath == null || currentPath.Count == 0
            ? 100f
            : (float)currentNodeIndex / currentPath.Count * 100f;

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
            stuckRecoveryAttempts = 0;
            isJumping = false;
            jumpWaitTicks = 0;
            accumulatedMovement = 0f;
            ticksSinceStuckCheck = 0;
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
            stuckRecoveryAttempts = 0;
            isJumping = false;
            jumpWaitTicks = 0;
            accumulatedMovement = 0f;
            ticksSinceStuckCheck = 0;
        }

        /// <summary>
        /// Clears the current path and stops execution.
        /// </summary>
        public void ClearPath()
        {
            currentPath = null;
            currentNodeIndex = 0;
            stuckTicks = 0;
            stuckRecoveryAttempts = 0;
            isJumping = false;
            jumpWaitTicks = 0;
            accumulatedMovement = 0f;
            ticksSinceStuckCheck = 0;
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

            // Track movement for stuck detection
            float movementThisTick = Vector2.Distance(terra.position, lastPosition);
            accumulatedMovement += movementThisTick;
            ticksSinceStuckCheck++;

            // Periodic stuck check
            if (ticksSinceStuckCheck >= StuckCheckInterval)
            {
                if (accumulatedMovement < MinMovementThreshold)
                {
                    stuckTicks += StuckCheckInterval;

                    // Attempt stuck recovery
                    if (stuckTicks >= StuckThreshold)
                    {
                        if (!AttemptStuckRecovery())
                        {
                            // Recovery failed, path is blocked
                            return false;
                        }
                    }
                }
                else
                {
                    // Moving fine, reset stuck counter
                    stuckTicks = 0;
                    stuckRecoveryAttempts = 0;
                }

                // Reset periodic tracking
                accumulatedMovement = 0f;
                ticksSinceStuckCheck = 0;
            }

            // Update last position for next tick
            lastPosition = terra.position;

            // Get current target node
            PathNode targetNode = currentPath[currentNodeIndex];
            Vector2 targetWorldPos = GetNodeWorldPosition(targetNode);

            // Check if we've reached the current node
            if (IsNodeReached(targetNode, targetWorldPos))
            {
                // Successfully reached node, reset jump state and recovery attempts
                isJumping = false;
                jumpWaitTicks = 0;
                stuckRecoveryAttempts = 0;

                currentNodeIndex++;
                if (IsComplete)
                {
                    // Stop movement on completion
                    terra.velocity.X = 0;
                    return true;
                }

                // Get the next target node
                targetNode = currentPath[currentNodeIndex];
                targetWorldPos = GetNodeWorldPosition(targetNode);
            }

            // Execute movement based on the movement type
            ExecuteMovement(targetNode, targetWorldPos);

            // Apply physics constraints
            ApplyPhysicsConstraints();

            return false;
        }

        /// <summary>
        /// Attempts to recover from being stuck.
        /// </summary>
        /// <returns>True if recovery should continue, false if path should be abandoned.</returns>
        private bool AttemptStuckRecovery()
        {
            stuckRecoveryAttempts++;

            if (stuckRecoveryAttempts > MaxStuckRecoveryAttempts)
            {
                return false; // Too many attempts, give up
            }

            // Recovery strategy 1: Try jumping
            if (stuckRecoveryAttempts == 1)
            {
                if (IsOnGround())
                {
                    terra.velocity.Y = JumpVelocity;
                }
                stuckTicks = 0;
                return true;
            }

            // Recovery strategy 2: Try jumping with horizontal movement
            if (stuckRecoveryAttempts == 2)
            {
                if (IsOnGround())
                {
                    terra.velocity.Y = JumpVelocity;
                }

                // Try moving in the opposite direction briefly
                PathNode targetNode = currentPath[currentNodeIndex];
                float targetX = GetNodeWorldPosition(targetNode).X;
                float direction = Math.Sign(targetX - terra.Center.X);
                terra.velocity.X = -direction * WalkSpeed; // Move away first
                stuckTicks = 0;
                return true;
            }

            // Recovery strategy 3: Skip to next node if possible
            if (stuckRecoveryAttempts == 3)
            {
                if (currentNodeIndex + 1 < currentPath.Count)
                {
                    // Try skipping to next node
                    PathNode nextNode = currentPath[currentNodeIndex + 1];
                    Vector2 nextNodePos = GetNodeWorldPosition(nextNode);
                    float distanceToNext = Vector2.Distance(terra.Center, nextNodePos) / TileSize;

                    // Only skip if next node is reasonably close
                    if (distanceToNext < 5f)
                    {
                        currentNodeIndex++;
                        stuckTicks = 0;
                        return true;
                    }
                }
                return false; // Cannot skip, give up
            }

            return false;
        }

        /// <summary>
        /// Applies physics constraints to the NPC velocity.
        /// </summary>
        private void ApplyPhysicsConstraints()
        {
            // Cap fall speed
            if (terra.velocity.Y > MaxFallSpeed)
            {
                terra.velocity.Y = MaxFallSpeed;
            }

            // Cap horizontal speed
            float maxHorizontalSpeed = WalkSpeed * 1.5f;
            if (Math.Abs(terra.velocity.X) > maxHorizontalSpeed)
            {
                terra.velocity.X = Math.Sign(terra.velocity.X) * maxHorizontalSpeed;
            }
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
                    ExecuteJump(node, targetPos);
                    break;
                case MovementType.JumpArc:
                    ExecuteJumpArc(node, targetPos);
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
            float horizontalDiff = target.X - terra.Center.X;
            float direction = Math.Sign(horizontalDiff);

            // Apply smooth acceleration instead of instant velocity
            float targetVelocity = direction * WalkSpeed;
            float acceleration = 0.5f;

            if (Math.Abs(horizontalDiff) < TileSize * 0.5f)
            {
                // Very close, slow down
                terra.velocity.X = horizontalDiff / TileSize * WalkSpeed;
            }
            else
            {
                // Accelerate toward target velocity
                terra.velocity.X = MoveToward(terra.velocity.X, targetVelocity, acceleration);
            }

            // Update NPC facing direction
            UpdateFacingDirection(direction);

            // Small step-up: if blocked horizontally but can step up one tile, do so
            if (Math.Abs(terra.velocity.X) < 0.1f && Math.Abs(horizontalDiff) > TileSize)
            {
                // Check if there's a step we can climb
                int tileX = (int)(terra.Center.X / TileSize) + (int)direction;
                int tileY = (int)((terra.position.Y + terra.height) / TileSize);

                if (IsTileSolid(tileX, tileY) && !IsTileSolid(tileX, tileY - 1) && !IsTileSolid(tileX, tileY - 2))
                {
                    // Can step up, apply small jump
                    if (IsOnGround())
                    {
                        terra.velocity.Y = JumpVelocity * 0.4f;
                    }
                }
            }
        }

        /// <summary>
        /// Executes a straight vertical jump toward the target.
        /// </summary>
        /// <param name="node">The target path node.</param>
        /// <param name="target">Target world position.</param>
        private void ExecuteJump(PathNode node, Vector2 target)
        {
            float horizontalDiff = target.X - terra.Center.X;
            float direction = Math.Sign(horizontalDiff);

            // Move horizontally toward target
            if (Math.Abs(horizontalDiff) > TileSize * 0.5f)
            {
                terra.velocity.X = direction * WalkSpeed;
            }
            else
            {
                terra.velocity.X = horizontalDiff / TileSize * WalkSpeed;
            }

            // Update NPC facing direction
            UpdateFacingDirection(direction);

            // Jump if on ground
            if (IsOnGround())
            {
                // Calculate required jump velocity based on height difference
                float heightDiff = terra.Center.Y - target.Y; // Positive = need to go up

                if (heightDiff > 0)
                {
                    // Need to jump up - calculate required velocity
                    // Using kinematic equation: v^2 = 2 * g * h
                    // Adding extra margin for safety
                    float requiredHeight = heightDiff / TileSize + 1f;
                    float requiredVelocity = -(float)Math.Sqrt(2f * Gravity * requiredHeight * TileSize);

                    // Clamp to reasonable jump velocity
                    requiredVelocity = Math.Max(requiredVelocity, JumpVelocity * 1.2f);
                    requiredVelocity = Math.Min(requiredVelocity, JumpVelocity * 0.5f);

                    terra.velocity.Y = requiredVelocity;
                    isJumping = true;
                }
            }
            else if (isJumping)
            {
                // In air, apply air control
                float airControl = 0.3f;
                if (Math.Abs(horizontalDiff) > TileSize)
                {
                    terra.velocity.X = MoveToward(terra.velocity.X, direction * WalkSpeed, airControl);
                }
            }
        }

        /// <summary>
        /// Executes a parabolic arc jump using the node's stored velocity.
        /// </summary>
        /// <param name="node">The target path node with velocity information.</param>
        /// <param name="target">Target world position.</param>
        private void ExecuteJumpArc(PathNode node, Vector2 target)
        {
            float horizontalDiff = target.X - terra.Center.X;
            float direction = Math.Sign(horizontalDiff);

            // Update NPC facing direction
            UpdateFacingDirection(direction);

            if (IsOnGround() && !isJumping)
            {
                // Initiate jump with arc velocity
                if (node.Velocity != Vector2.Zero)
                {
                    // Use the pre-calculated velocity from pathfinding
                    terra.velocity.X = node.Velocity.X;
                    terra.velocity.Y = node.Velocity.Y;
                }
                else
                {
                    // Calculate arc jump velocity
                    float jumpDirection = Math.Sign(target.X - terra.Center.X);
                    terra.velocity.X = jumpDirection * WalkSpeed;
                    terra.velocity.Y = JumpVelocity;
                }
                isJumping = true;
                jumpWaitTicks = 0;
            }
            else if (!IsOnGround())
            {
                // In the air - apply air control to steer toward target
                float airControl = 0.15f;
                float targetVelocityX = direction * WalkSpeed;
                terra.velocity.X = MoveToward(terra.velocity.X, targetVelocityX, airControl);
            }
            else
            {
                // Waiting on ground for jump - increment wait counter
                jumpWaitTicks++;
                if (jumpWaitTicks > MaxJumpWaitTicks)
                {
                    // Force jump attempt
                    terra.velocity.Y = JumpVelocity;
                    terra.velocity.X = direction * WalkSpeed;
                    isJumping = true;
                }
            }
        }

        /// <summary>
        /// Executes falling movement with horizontal control.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteFall(Vector2 target)
        {
            float horizontalDiff = target.X - terra.Center.X;
            float direction = Math.Sign(horizontalDiff);

            // Air control during fall
            float airControl = 0.2f;
            float targetVelocityX = direction * WalkSpeed * 0.7f;
            terra.velocity.X = MoveToward(terra.velocity.X, targetVelocityX, airControl);

            // Update NPC facing direction
            UpdateFacingDirection(direction);

            // Let gravity do its work (no Y velocity manipulation)
        }

        /// <summary>
        /// Executes climbing movement on ropes or vines.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteClimb(Vector2 target)
        {
            // When climbing, negate gravity by setting the NPC state appropriately
            // In Terraria, NPCs don't have built-in climbing, so we simulate it

            float verticalDiff = target.Y - terra.Center.Y;
            float horizontalDiff = target.X - terra.Center.X;

            // Move vertically toward target (negate gravity while climbing)
            float verticalDirection = Math.Sign(verticalDiff);
            terra.velocity.Y = verticalDirection * ClimbSpeed;

            // Allow slight horizontal adjustment to stay on rope
            float horizontalDirection = Math.Sign(horizontalDiff);
            terra.velocity.X = horizontalDirection * ClimbSpeed * 0.3f;

            // Update NPC facing direction based on horizontal movement
            if (Math.Abs(horizontalDiff) > TileSize * 0.5f)
            {
                UpdateFacingDirection(horizontalDirection);
            }
        }

        /// <summary>
        /// Executes dropping through a platform.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecutePlatformDrop(Vector2 target)
        {
            float horizontalDiff = target.X - terra.Center.X;
            float direction = Math.Sign(horizontalDiff);

            // Apply small downward velocity to initiate platform drop
            // In Terraria, this is typically done by holding down + jump
            if (IsOnPlatform())
            {
                terra.velocity.Y = PlatformDropVelocity;

                // For NPCs, we might need to temporarily disable platform collision
                // This is often handled by the NPC AI type, but we apply velocity regardless
            }

            // Control horizontal movement
            float airControl = 0.3f;
            float targetVelocityX = direction * WalkSpeed * 0.5f;
            terra.velocity.X = MoveToward(terra.velocity.X, targetVelocityX, airControl);

            // Update NPC facing direction
            UpdateFacingDirection(direction);
        }

        /// <summary>
        /// Executes swimming movement in liquids.
        /// </summary>
        /// <param name="target">Target world position.</param>
        private void ExecuteSwim(Vector2 target)
        {
            // Calculate direction to target
            Vector2 toTarget = target - terra.Center;
            float distance = toTarget.Length();

            if (distance > 1f)
            {
                toTarget.Normalize();

                // In liquids, apply smooth swimming motion
                float swimAcceleration = 0.3f;
                Vector2 targetVelocity = toTarget * SwimSpeed;

                terra.velocity.X = MoveToward(terra.velocity.X, targetVelocity.X, swimAcceleration);
                terra.velocity.Y = MoveToward(terra.velocity.Y, targetVelocity.Y, swimAcceleration);
            }
            else
            {
                // Very close to target, slow down
                terra.velocity *= 0.9f;
            }

            // Update NPC facing direction based on horizontal movement
            if (Math.Abs(toTarget.X) > 0.1f)
            {
                UpdateFacingDirection(Math.Sign(toTarget.X));
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

            if (distance > 1f)
            {
                toTarget.Normalize();

                // Grapple pulls faster than swimming, with acceleration
                float grappleSpeed = 6f;
                float grappleAcceleration = 0.5f;
                Vector2 targetVelocity = toTarget * grappleSpeed;

                terra.velocity.X = MoveToward(terra.velocity.X, targetVelocity.X, grappleAcceleration);
                terra.velocity.Y = MoveToward(terra.velocity.Y, targetVelocity.Y, grappleAcceleration);

                // Override gravity while grappling
                if (terra.velocity.Y > 0 && toTarget.Y < 0)
                {
                    terra.velocity.Y = Math.Min(terra.velocity.Y, grappleSpeed * 0.5f);
                }
            }

            // Update NPC facing direction
            if (Math.Abs(toTarget.X) > 0.1f)
            {
                UpdateFacingDirection(Math.Sign(toTarget.X));
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
            // Instant teleport to target, adjusting for NPC size
            terra.position = target - new Vector2(terra.width / 2f, terra.height);
            terra.velocity = Vector2.Zero;

            // Create visual effect (dust) if desired
            // Dust.NewDust(terra.position, terra.width, terra.height, DustID.MagicMirror, 0, 0, 0, default, 1f);
        }

        // ==================== Helper Methods ====================

        /// <summary>
        /// Moves a value toward a target by a maximum delta.
        /// </summary>
        /// <param name="current">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <param name="maxDelta">Maximum change per call.</param>
        /// <returns>The new value moved toward target.</returns>
        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }
            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>
        /// Updates the NPC's facing direction.
        /// </summary>
        /// <param name="direction">Direction to face (-1 for left, 1 for right).</param>
        private void UpdateFacingDirection(float direction)
        {
            if (direction != 0)
            {
                terra.direction = (int)direction;
            }
            else
            {
                terra.direction = 1; // Default to facing right
            }
        }

        /// <summary>
        /// Checks if the NPC is standing on solid ground.
        /// </summary>
        /// <returns>True if on ground, false otherwise.</returns>
        private bool IsOnGround()
        {
            // Check multiple points along the bottom of the NPC
            int leftTileX = (int)(terra.position.X / TileSize);
            int rightTileX = (int)((terra.position.X + terra.width) / TileSize);
            int bottomTileY = (int)((terra.position.Y + terra.height + 1) / TileSize);

            // Check if any tile below is solid
            for (int x = leftTileX; x <= rightTileX; x++)
            {
                if (IsTileSolid(x, bottomTileY) || IsTilePlatform(x, bottomTileY))
                {
                    // Also verify NPC has minimal vertical velocity
                    if (Math.Abs(terra.velocity.Y) < 0.5f)
                    {
                        return true;
                    }
                }
            }

            // Fallback: check if velocity indicates grounded state
            return Math.Abs(terra.velocity.Y) < 0.01f && terra.velocity.Y >= 0 && terra.oldVelocity.Y >= 0;
        }

        /// <summary>
        /// Checks if the NPC is standing on a platform (can drop through).
        /// </summary>
        /// <returns>True if on a platform, false otherwise.</returns>
        private bool IsOnPlatform()
        {
            int centerTileX = (int)(terra.Center.X / TileSize);
            int bottomTileY = (int)((terra.position.Y + terra.height + 1) / TileSize);

            return IsTilePlatform(centerTileX, bottomTileY);
        }

        /// <summary>
        /// Checks if a tile is solid (blocks movement).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if solid.</returns>
        private bool IsTileSolid(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return true; // Out of bounds is considered solid

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return false;

            return Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// Checks if a tile is a platform (solid top only).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if platform.</returns>
        private bool IsTilePlatform(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return false;

            return Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// Converts a path node's tile coordinates to world position.
        /// </summary>
        /// <param name="node">The path node.</param>
        /// <returns>World position in pixels (center of tile).</returns>
        private Vector2 GetNodeWorldPosition(PathNode node)
        {
            return new Vector2(node.X * TileSize + TileSize / 2f, node.Y * TileSize + TileSize / 2f);
        }

        /// <summary>
        /// Checks if the NPC has reached the target node.
        /// Uses different thresholds based on movement type.
        /// </summary>
        /// <param name="node">The target path node.</param>
        /// <param name="targetWorldPos">Target world position.</param>
        /// <returns>True if within appropriate distance of target.</returns>
        private bool IsNodeReached(PathNode node, Vector2 targetWorldPos)
        {
            float distanceInTiles = Vector2.Distance(terra.Center, targetWorldPos) / TileSize;

            // Use different thresholds based on movement type
            float threshold;
            switch (node.MovementUsed)
            {
                case MovementType.Walk:
                    threshold = NodeReachedDistanceWalk;
                    break;

                case MovementType.Jump:
                case MovementType.JumpArc:
                case MovementType.Fall:
                    // For air movements, be more lenient and also check if we've passed the target
                    threshold = NodeReachedDistanceAir;

                    // Additional check: if we're falling and have passed the target Y, consider reached
                    if (node.MovementUsed == MovementType.Fall)
                    {
                        float verticalDiff = targetWorldPos.Y - terra.Center.Y;
                        if (verticalDiff < -TileSize && terra.velocity.Y > 0)
                        {
                            // We've fallen past the target, move to next node
                            return true;
                        }
                    }
                    break;

                case MovementType.ClimbRope:
                case MovementType.ClimbPlatform:
                    threshold = NodeReachedDistanceVertical;
                    break;

                case MovementType.Teleport:
                    threshold = 0.5f; // Very precise for teleport
                    break;

                default:
                    threshold = NodeReachedDistanceWalk;
                    break;
            }

            // Standard distance check
            if (distanceInTiles <= threshold)
            {
                return true;
            }

            // For jumping/falling, also check if we've landed near the target X
            if (node.MovementUsed == MovementType.Jump || node.MovementUsed == MovementType.JumpArc)
            {
                float horizontalDistTiles = Math.Abs(terra.Center.X - targetWorldPos.X) / TileSize;
                float verticalDistTiles = Math.Abs(terra.Center.Y - targetWorldPos.Y) / TileSize;

                // If we're close horizontally and have landed, consider it reached
                if (horizontalDistTiles < 1.5f && verticalDistTiles < 2f && IsOnGround())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
