using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;
using System.Collections.Generic;
using Terraria;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action that navigates Terra to a specific tile position using A* pathfinding.
    /// Uses TerrariaPathfinder to compute the path and PathExecutor to execute it.
    /// </summary>
    public class PathfindAction : BaseAction
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
        /// Number of ticks this action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Maximum ticks before timeout (600 ticks = 10 seconds at 60 TPS).
        /// </summary>
        private const int maxTicks = 600;

        /// <summary>
        /// The pathfinder used to compute the path.
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// The executor that controls Terra's movement along the path.
        /// </summary>
        private PathExecutor pathExecutor;

        /// <summary>
        /// Whether the path has been computed.
        /// </summary>
        private bool pathComputed;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new PathfindAction for the specified Terra NPC and task.
        /// </summary>
        /// <param name="terra">The Terra NPC that will execute this action.</param>
        /// <param name="task">The task containing target coordinates.</param>
        public PathfindAction(TerraNPC terra, Task task) : base(terra, task)
        {
            ticksRunning = 0;
            pathComputed = false;
        }

        #endregion

        #region BaseAction Implementation

        /// <summary>
        /// A human-readable description of what this action does.
        /// </summary>
        public override string Description => $"Pathfinding to ({targetX}, {targetY})";

        /// <summary>
        /// Called when the action starts. Initializes pathfinding components.
        /// </summary>
        protected override void OnStart()
        {
            // Get target coordinates from task parameters (tile coordinates)
            targetX = task.GetParameter<int>("x", 0);
            targetY = task.GetParameter<int>("y", 0);

            // Validate target is within world bounds
            if (targetX < 0 || targetX >= Main.maxTilesX ||
                targetY < 0 || targetY >= Main.maxTilesY)
            {
                result = ActionResult.Fail($"Target position ({targetX}, {targetY}) is outside world bounds");
                return;
            }

            // Initialize pathfinder with Terra's NPC reference
            pathfinder = new TerrariaPathfinder(terra.NPC);

            // Initialize path executor with Terra's NPC reference
            pathExecutor = new PathExecutor(terra.NPC);
        }

        /// <summary>
        /// Called each game tick while the action is running.
        /// Handles path computation and execution.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            if (ticksRunning >= maxTicks)
            {
                result = ActionResult.Fail($"Pathfinding timed out after {maxTicks} ticks");
                return;
            }

            // Compute path if not already done
            if (!pathComputed)
            {
                // Get Terra's current tile position from world coordinates
                int currentX = (int)(terra.NPC.Center.X / 16);
                int currentY = (int)(terra.NPC.Center.Y / 16);

                // Find path from current position to target
                List<PathNode> path = pathfinder.FindPath(currentX, currentY, targetX, targetY);

                if (path == null)
                {
                    result = ActionResult.Fail("No path found");
                    return;
                }

                // Set the path for execution
                pathExecutor.SetPath(path);
                pathComputed = true;
            }

            // Execute one tick of path following
            pathExecutor.Tick();

            // Check if path execution is complete
            if (pathExecutor.IsComplete)
            {
                result = ActionResult.Succeed($"Reached target position ({targetX}, {targetY})");
                return;
            }

            // Check if Terra got stuck
            if (pathExecutor.IsStuck)
            {
                result = ActionResult.Fail("Got stuck");
                return;
            }
        }

        /// <summary>
        /// Called when the action is cancelled. Stops Terra's movement.
        /// </summary>
        protected override void OnCancel()
        {
            // Stop Terra's movement by zeroing velocity
            terra.NPC.velocity.X = 0;
            terra.NPC.velocity.Y = 0;
        }

        #endregion
    }
}
