using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace TerraAIMod.Pathfinding
{
    /// <summary>
    /// A* pathfinding algorithm for 2D platformer navigation with gravity physics.
    /// Supports walking, jumping, falling, climbing, swimming, and grappling.
    /// </summary>
    public class TerrariaPathfinder
    {
        #region Constants

        /// <summary>Gravity acceleration per tick.</summary>
        public const float Gravity = 0.4f;

        /// <summary>Maximum fall speed (terminal velocity).</summary>
        public const float MaxFallSpeed = 10f;

        /// <summary>Initial upward velocity when jumping.</summary>
        public const float JumpSpeed = 5.1f;

        /// <summary>Horizontal movement speed.</summary>
        public const float MoveSpeed = 3f;

        /// <summary>Maximum jump height in tiles.</summary>
        public const int MaxJumpHeight = 6;

        /// <summary>Maximum nodes to search before giving up.</summary>
        public const int MaxSearchNodes = 10000;

        /// <summary>Grapple hook range in tiles.</summary>
        private const int GrappleRange = 25;

        #endregion

        #region Fields

        /// <summary>Reference to the Terra NPC for equipment checks.</summary>
        private readonly NPC terra;

        /// <summary>Set of already evaluated nodes.</summary>
        private HashSet<(int, int)> closedSet;

        /// <summary>Priority queue of nodes to evaluate.</summary>
        private PriorityQueue<PathNode, float> openSet;

        /// <summary>Dictionary of all created nodes for fast lookup.</summary>
        private Dictionary<(int, int), PathNode> allNodes;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new pathfinder for the specified NPC.
        /// </summary>
        /// <param name="terraNpc">The NPC to pathfind for.</param>
        public TerrariaPathfinder(NPC terraNpc)
        {
            terra = terraNpc;
        }

        #endregion

        #region Main Pathfinding

        /// <summary>
        /// Finds a path from the start position to the goal using A* algorithm.
        /// </summary>
        /// <param name="startX">Starting tile X coordinate.</param>
        /// <param name="startY">Starting tile Y coordinate.</param>
        /// <param name="goalX">Goal tile X coordinate.</param>
        /// <param name="goalY">Goal tile Y coordinate.</param>
        /// <returns>List of PathNodes from start to goal, or null if no path found.</returns>
        public List<PathNode> FindPath(int startX, int startY, int goalX, int goalY)
        {
            // Initialize data structures
            closedSet = new HashSet<(int, int)>();
            openSet = new PriorityQueue<PathNode, float>();
            allNodes = new Dictionary<(int, int), PathNode>();

            // Create start node
            PathNode startNode = GetOrCreateNode(startX, startY);
            startNode.GCost = 0;
            startNode.HCost = Heuristic(startX, startY, goalX, goalY);

            openSet.Enqueue(startNode, startNode.FCost);

            int nodesSearched = 0;

            while (openSet.Count > 0 && nodesSearched < MaxSearchNodes)
            {
                PathNode current = openSet.Dequeue();
                nodesSearched++;

                // Skip if already processed
                if (closedSet.Contains((current.X, current.Y)))
                    continue;

                // Check if we reached the goal
                if (current.X == goalX && current.Y == goalY)
                {
                    return ReconstructPath(current);
                }

                // Mark as processed
                closedSet.Add((current.X, current.Y));

                // Explore neighbors
                foreach (PathNode neighbor in GetValidNeighbors(current, goalX, goalY))
                {
                    if (closedSet.Contains((neighbor.X, neighbor.Y)))
                        continue;

                    float tentativeGCost = current.GCost + GetMovementCost(current, neighbor);

                    if (tentativeGCost < neighbor.GCost)
                    {
                        neighbor.Parent = current;
                        neighbor.GCost = tentativeGCost;
                        neighbor.HCost = Heuristic(neighbor.X, neighbor.Y, goalX, goalY);

                        openSet.Enqueue(neighbor, neighbor.FCost);
                    }
                }
            }

            // No path found
            return null;
        }

        /// <summary>
        /// Calculates the heuristic cost estimate from a position to the goal.
        /// Uses Manhattan distance with vertical penalty for upward movement.
        /// </summary>
        /// <param name="x1">Current X coordinate.</param>
        /// <param name="y1">Current Y coordinate.</param>
        /// <param name="x2">Goal X coordinate.</param>
        /// <param name="y2">Goal Y coordinate.</param>
        /// <returns>Estimated cost to reach the goal.</returns>
        private float Heuristic(int x1, int y1, int x2, int y2)
        {
            float dx = Math.Abs(x2 - x1);
            float dy = y2 - y1; // Positive = down, negative = up

            // Manhattan distance as base
            float distance = dx + Math.Abs(dy);

            // Add penalty for upward movement (harder to go up than down)
            if (dy < 0)
            {
                distance += Math.Abs(dy) * 0.5f;
            }

            return distance;
        }

        #endregion

        #region Neighbor Generation

        /// <summary>
        /// Generates all valid neighbor nodes from the current position.
        /// </summary>
        /// <param name="current">The current path node.</param>
        /// <param name="goalX">Goal X coordinate for direction preference.</param>
        /// <param name="goalY">Goal Y coordinate for direction preference.</param>
        /// <returns>Enumerable of valid neighbor nodes.</returns>
        private IEnumerable<PathNode> GetValidNeighbors(PathNode current, int goalX, int goalY)
        {
            int x = current.X;
            int y = current.Y;

            bool onGround = IsStandable(x, y);
            bool inLiquid = IsLiquid(x, y);
            bool onClimbable = IsClimbable(x, y);

            // 1. Walking left/right on solid ground
            if (onGround)
            {
                // Walk left
                if (CanWalkTo(x - 1, y))
                {
                    yield return CreateNeighbor(x - 1, y, MovementType.Walk);
                }

                // Walk right
                if (CanWalkTo(x + 1, y))
                {
                    yield return CreateNeighbor(x + 1, y, MovementType.Walk);
                }
            }

            // 2. Falling if no ground below
            if (!onGround && !onClimbable && !inLiquid)
            {
                // Fall straight down
                if (!IsSolid(x, y + 1) && CanLandAt(x, y + 1))
                {
                    yield return CreateNeighbor(x, y + 1, MovementType.Fall);
                }

                // Fall diagonally
                if (!IsSolid(x - 1, y + 1) && CanLandAt(x - 1, y + 1))
                {
                    yield return CreateNeighbor(x - 1, y + 1, MovementType.Fall);
                }
                if (!IsSolid(x + 1, y + 1) && CanLandAt(x + 1, y + 1))
                {
                    yield return CreateNeighbor(x + 1, y + 1, MovementType.Fall);
                }
            }

            // 3. Jumping (straight up + arcs) if on ground
            if (onGround)
            {
                // Straight up jump
                for (int jumpHeight = 1; jumpHeight <= MaxJumpHeight; jumpHeight++)
                {
                    int jumpY = y - jumpHeight;

                    // Check if path is clear
                    if (IsSolid(x, jumpY) || IsSolid(x, jumpY - 1)) // Head clearance
                        break;

                    // Can we land here or is it a waypoint?
                    if (CanLandAt(x, jumpY))
                    {
                        yield return CreateNeighbor(x, jumpY, MovementType.Jump);
                    }
                }

                // Jump arcs (parabolic trajectories)
                foreach (PathNode arcNode in GetJumpArcNeighbors(x, y))
                {
                    yield return arcNode;
                }
            }

            // 4. Platform drop-through
            if (IsPlatform(x, y + 1) && !IsSolid(x, y + 2))
            {
                yield return CreateNeighbor(x, y + 1, MovementType.ClimbPlatform);
            }

            // 5. Rope/vine climbing
            if (onClimbable)
            {
                // Climb up
                if (IsClimbable(x, y - 1) || CanLandAt(x, y - 1))
                {
                    yield return CreateNeighbor(x, y - 1, MovementType.ClimbRope);
                }

                // Climb down
                if (IsClimbable(x, y + 1) || CanLandAt(x, y + 1))
                {
                    yield return CreateNeighbor(x, y + 1, MovementType.ClimbRope);
                }

                // Jump off rope
                if (!IsSolid(x - 1, y))
                {
                    yield return CreateNeighbor(x - 1, y, MovementType.Fall);
                }
                if (!IsSolid(x + 1, y))
                {
                    yield return CreateNeighbor(x + 1, y, MovementType.Fall);
                }
            }

            // 6. Swimming in liquid
            if (inLiquid)
            {
                // Swim in all directions
                int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
                int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

                for (int i = 0; i < 8; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];

                    if (!IsSolid(nx, ny) && (IsLiquid(nx, ny) || CanLandAt(nx, ny)))
                    {
                        yield return CreateNeighbor(nx, ny, MovementType.Swim);
                    }
                }
            }

            // 7. Grapple targets (if equipped)
            if (HasGrappleEquipped())
            {
                foreach (PathNode grappleTarget in GetGrappleTargets(x, y))
                {
                    yield return grappleTarget;
                }
            }
        }

        /// <summary>
        /// Generates jump arc neighbors using parabolic trajectories.
        /// </summary>
        /// <param name="startX">Starting X position.</param>
        /// <param name="startY">Starting Y position.</param>
        /// <returns>Enumerable of reachable positions via jump arcs.</returns>
        private IEnumerable<PathNode> GetJumpArcNeighbors(int startX, int startY)
        {
            // Simulate jump arcs to the left and right
            int[] directions = { -1, 1 };

            foreach (int dir in directions)
            {
                float vx = MoveSpeed * dir;
                float vy = -JumpSpeed;
                float x = startX;
                float y = startY;

                HashSet<(int, int)> visited = new HashSet<(int, int)>();

                // Simulate trajectory
                for (int tick = 0; tick < 60; tick++) // Max 1 second of simulation
                {
                    x += vx / 16f; // Convert to tiles per tick
                    vy += Gravity;
                    if (vy > MaxFallSpeed) vy = MaxFallSpeed;
                    y += vy / 16f;

                    int tileX = (int)x;
                    int tileY = (int)y;

                    // Stop if we hit something
                    if (IsSolid(tileX, tileY))
                        break;

                    // Only yield unique landing positions
                    if (!visited.Contains((tileX, tileY)) && CanLandAt(tileX, tileY))
                    {
                        visited.Add((tileX, tileY));

                        // Check if path is clear to this point
                        if (IsPathClear(startX, startY, tileX, tileY))
                        {
                            yield return CreateNeighbor(tileX, tileY, MovementType.JumpArc);
                        }
                    }

                    // Stop if we're falling and past starting height
                    if (vy > 0 && tileY > startY + MaxJumpHeight)
                        break;
                }
            }
        }

        /// <summary>
        /// Creates a neighbor PathNode with the specified movement type.
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <param name="movement">Movement type used to reach this node.</param>
        /// <returns>The created or retrieved PathNode.</returns>
        private PathNode CreateNeighbor(int x, int y, MovementType movement)
        {
            PathNode node = GetOrCreateNode(x, y);
            node.MovementUsed = movement;
            return node;
        }

        #endregion

        #region Movement Cost

        /// <summary>
        /// Calculates the movement cost between two nodes based on movement type.
        /// </summary>
        /// <param name="from">The source node.</param>
        /// <param name="to">The destination node.</param>
        /// <returns>The cost of the movement.</returns>
        private float GetMovementCost(PathNode from, PathNode to)
        {
            float baseCost = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);

            switch (to.MovementUsed)
            {
                case MovementType.Walk:
                    return baseCost * 1.0f;

                case MovementType.Fall:
                    return baseCost * 0.5f; // Falling is easy

                case MovementType.Jump:
                    return baseCost * 1.5f; // Jumping costs more

                case MovementType.JumpArc:
                    return baseCost * 2.0f; // Arc jumps are complex

                case MovementType.ClimbRope:
                    return baseCost * 1.2f;

                case MovementType.ClimbPlatform:
                    return baseCost * 1.0f;

                case MovementType.Swim:
                    return baseCost * 1.5f; // Swimming is slower

                case MovementType.Grapple:
                    return baseCost * 0.8f; // Grappling is efficient

                case MovementType.Teleport:
                    return 0.1f; // Teleport is almost free

                default:
                    return baseCost;
            }
        }

        #endregion

        #region Tile Helpers

        /// <summary>
        /// Checks if the tile at the specified position is solid.
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if the tile is solid.</returns>
        private bool IsSolid(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return true; // Out of bounds is solid

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return false;

            return Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// Checks if the tile at the specified position is a platform (solid top only).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if the tile is a platform.</returns>
        private bool IsPlatform(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return false;

            return Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// Checks if the tile at the specified position is climbable (rope, vine, etc.).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if the tile can be climbed.</returns>
        private bool IsClimbable(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return false;

            int type = tile.TileType;

            // Check for rope types
            if (type == TileID.Rope ||
                type == TileID.SilkRope ||
                type == TileID.VineRope ||
                type == TileID.WebRope)
            {
                return true;
            }

            // Check for vines
            if (type == TileID.Vines ||
                type == TileID.JungleVines ||
                type == TileID.HallowedVines ||
                type == TileID.CrimsonVines)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the tile at the specified position contains liquid.
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if the tile has liquid.</returns>
        private bool IsLiquid(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[x, y];
            return tile.LiquidAmount > 0;
        }

        /// <summary>
        /// Checks if the position is standable (no solid here AND solid/platform below).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if an entity can stand at this position.</returns>
        private bool IsStandable(int x, int y)
        {
            // Position must not be solid
            if (IsSolid(x, y))
                return false;

            // Must have solid ground or platform below
            return IsSolid(x, y + 1) || IsPlatform(x, y + 1);
        }

        /// <summary>
        /// Checks if the position can be walked to (standable with head clearance).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if an entity can walk to this position.</returns>
        private bool CanWalkTo(int x, int y)
        {
            // Must be standable
            if (!IsStandable(x, y))
                return false;

            // Must have head clearance (tile above must not be solid)
            return !IsSolid(x, y - 1);
        }

        /// <summary>
        /// Checks if the position can be landed at (standable or platform or climbable).
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>True if an entity can land at this position.</returns>
        private bool CanLandAt(int x, int y)
        {
            if (IsSolid(x, y))
                return false;

            return IsStandable(x, y) || IsPlatform(x, y + 1) || IsClimbable(x, y);
        }

        /// <summary>
        /// Checks if the path between two points is clear using Bresenham line algorithm.
        /// </summary>
        /// <param name="x1">Start X coordinate.</param>
        /// <param name="y1">Start Y coordinate.</param>
        /// <param name="x2">End X coordinate.</param>
        /// <param name="y2">End Y coordinate.</param>
        /// <returns>True if there are no solid tiles blocking the path.</returns>
        private bool IsPathClear(int x1, int y1, int x2, int y2)
        {
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            int x = x1;
            int y = y1;

            while (true)
            {
                // Check current position (skip start and end)
                if ((x != x1 || y != y1) && (x != x2 || y != y2))
                {
                    if (IsSolid(x, y))
                        return false;
                }

                if (x == x2 && y == y2)
                    break;

                int e2 = 2 * err;

                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            return true;
        }

        /// <summary>
        /// Scans for valid grapple anchor points within range.
        /// </summary>
        /// <param name="x">Current X position.</param>
        /// <param name="y">Current Y position.</param>
        /// <returns>Enumerable of reachable grapple target nodes.</returns>
        private IEnumerable<PathNode> GetGrappleTargets(int x, int y)
        {
            // Scan in a circle for solid tiles that can be grappled
            for (int offsetX = -GrappleRange; offsetX <= GrappleRange; offsetX++)
            {
                for (int offsetY = -GrappleRange; offsetY <= GrappleRange; offsetY++)
                {
                    int targetX = x + offsetX;
                    int targetY = y + offsetY;

                    // Check distance
                    float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    if (distance > GrappleRange || distance < 3)
                        continue;

                    // Grapple needs a solid anchor point
                    if (!IsSolid(targetX, targetY))
                        continue;

                    // Find the landing position next to the anchor
                    // Check positions around the solid tile
                    int[] checkX = { 0, 0, -1, 1 };
                    int[] checkY = { -1, 1, 0, 0 };

                    for (int i = 0; i < 4; i++)
                    {
                        int landX = targetX + checkX[i];
                        int landY = targetY + checkY[i];

                        if (!IsSolid(landX, landY) && IsPathClear(x, y, landX, landY))
                        {
                            // Prefer landing spots where we can stand
                            if (CanLandAt(landX, landY))
                            {
                                yield return CreateNeighbor(landX, landY, MovementType.Grapple);
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the NPC has a grappling hook equipped.
        /// </summary>
        /// <returns>True if a grapple is available.</returns>
        private bool HasGrappleEquipped()
        {
            // For NPCs, we might check if this is enabled via AI configuration
            // For now, return false as NPCs typically don't have grapples
            return false;
        }

        #endregion

        #region Node Management

        /// <summary>
        /// Gets an existing node from the dictionary or creates a new one.
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        /// <returns>The existing or newly created PathNode.</returns>
        private PathNode GetOrCreateNode(int x, int y)
        {
            var key = (x, y);

            if (allNodes.TryGetValue(key, out PathNode existing))
            {
                return existing;
            }

            PathNode node = new PathNode(x, y)
            {
                GCost = float.MaxValue,
                HCost = 0
            };

            allNodes[key] = node;
            return node;
        }

        /// <summary>
        /// Reconstructs the path from the end node back to the start.
        /// </summary>
        /// <param name="endNode">The final node in the path.</param>
        /// <returns>List of PathNodes from start to goal.</returns>
        private List<PathNode> ReconstructPath(PathNode endNode)
        {
            List<PathNode> path = new List<PathNode>();
            PathNode current = endNode;

            while (current != null)
            {
                path.Add(current);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        #endregion
    }
}
