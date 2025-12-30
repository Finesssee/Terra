using System;
using Microsoft.Xna.Framework;

namespace TerraAIMod.Pathfinding
{
    /// <summary>
    /// Types of movement that can be used to traverse between path nodes.
    /// </summary>
    public enum MovementType
    {
        /// <summary>Horizontal movement on ground.</summary>
        Walk,

        /// <summary>Gravity descent (falling down).</summary>
        Fall,

        /// <summary>Upward jump movement.</summary>
        Jump,

        /// <summary>Parabolic arc jump trajectory.</summary>
        JumpArc,

        /// <summary>Climbing on rope or vine.</summary>
        ClimbRope,

        /// <summary>Dropping through or climbing platforms.</summary>
        ClimbPlatform,

        /// <summary>Swimming in liquid (water, lava, honey).</summary>
        Swim,

        /// <summary>Grappling hook movement.</summary>
        Grapple,

        /// <summary>Teleportation (magic mirror, etc.).</summary>
        Teleport
    }

    /// <summary>
    /// A* pathfinding node for 2D platformer navigation.
    /// </summary>
    public class PathNode : IComparable<PathNode>
    {
        /// <summary>
        /// Tile X coordinate.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Tile Y coordinate.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Cost from the start node to this node.
        /// </summary>
        public float GCost { get; set; }

        /// <summary>
        /// Heuristic cost estimate from this node to the goal.
        /// </summary>
        public float HCost { get; set; }

        /// <summary>
        /// Total estimated cost (GCost + HCost).
        /// </summary>
        public float FCost => GCost + HCost;

        /// <summary>
        /// Parent node in the path (for path reconstruction).
        /// </summary>
        public PathNode Parent { get; set; }

        /// <summary>
        /// The type of movement used to reach this node from its parent.
        /// </summary>
        public MovementType MovementUsed { get; set; }

        /// <summary>
        /// Velocity vector for jump/grapple movements.
        /// </summary>
        public Vector2 Velocity { get; set; }

        /// <summary>
        /// Number of game ticks spent in the air (for jump/fall tracking).
        /// </summary>
        public int TicksInAir { get; set; }

        /// <summary>
        /// Creates a new path node at the specified tile coordinates.
        /// </summary>
        /// <param name="x">Tile X coordinate.</param>
        /// <param name="y">Tile Y coordinate.</param>
        public PathNode(int x, int y)
        {
            X = x;
            Y = y;
            GCost = 0f;
            HCost = 0f;
            Parent = null;
            MovementUsed = MovementType.Walk;
            Velocity = Vector2.Zero;
            TicksInAir = 0;
        }

        /// <summary>
        /// Compares this node with another by FCost for priority queue ordering.
        /// </summary>
        /// <param name="other">The other node to compare against.</param>
        /// <returns>Negative if this node has lower FCost, positive if higher, zero if equal.</returns>
        public int CompareTo(PathNode other)
        {
            if (other == null)
                return -1;

            return FCost.CompareTo(other.FCost);
        }

        /// <summary>
        /// Generates a hash code based on tile coordinates.
        /// </summary>
        /// <returns>Hash code computed as X * 10000 + Y.</returns>
        public override int GetHashCode()
        {
            return X * 10000 + Y;
        }

        /// <summary>
        /// Checks equality based on tile coordinates.
        /// </summary>
        /// <param name="obj">Object to compare against.</param>
        /// <returns>True if the object is a PathNode with the same X and Y coordinates.</returns>
        public override bool Equals(object obj)
        {
            if (obj is PathNode other)
            {
                return X == other.X && Y == other.Y;
            }
            return false;
        }

        /// <summary>
        /// Returns a string representation of this node.
        /// </summary>
        /// <returns>String showing coordinates, costs, and movement type.</returns>
        public override string ToString()
        {
            return $"PathNode({X}, {Y}) F={FCost:F2} G={GCost:F2} H={HCost:F2} Movement={MovementUsed}";
        }
    }
}
