using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TerraAIMod.Action.Actions;
using TerraAIMod.AI;
using TerraAIMod.Config;
using TerraAIMod.NPCs;
using Terraria.ModLoader;

namespace TerraAIMod.Action
{
    /// <summary>
    /// Manages task queuing and action execution for a Terra NPC.
    /// Handles natural language command processing, task planning, and action lifecycle.
    /// </summary>
    public class ActionExecutor
    {
        #region Fields

        /// <summary>
        /// Reference to the Terra NPC this executor controls.
        /// </summary>
        private readonly TerraNPC terra;

        /// <summary>
        /// Queue of tasks waiting to be executed.
        /// </summary>
        private readonly Queue<Task> taskQueue;

        /// <summary>
        /// The currently executing action, or null if idle.
        /// </summary>
        private BaseAction currentAction;

        /// <summary>
        /// Description of the current goal being pursued.
        /// </summary>
        private string currentGoal;

        /// <summary>
        /// Number of game ticks since the last action was started.
        /// </summary>
        private int ticksSinceLastAction;

        /// <summary>
        /// Lazy-initialized task planner for AI-driven task decomposition.
        /// </summary>
        private SimpleTaskPlanner taskPlanner;

        /// <summary>
        /// Action to execute when idle (following the player).
        /// </summary>
        private BaseAction idleFollowAction;

        #endregion

        #region Properties

        /// <summary>
        /// Whether an action is currently executing or tasks are queued.
        /// </summary>
        public bool IsExecuting => currentAction != null || taskQueue.Count > 0;

        /// <summary>
        /// The current goal description, or null if no active goal.
        /// </summary>
        public string CurrentGoal => currentGoal;

        /// <summary>
        /// Gets the task planner, initializing it lazily if needed.
        /// </summary>
        private SimpleTaskPlanner TaskPlanner
        {
            get
            {
                if (taskPlanner == null)
                {
                    taskPlanner = new SimpleTaskPlanner(terra);
                }
                return taskPlanner;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new ActionExecutor for the specified Terra NPC.
        /// </summary>
        /// <param name="terra">The Terra NPC this executor will control.</param>
        public ActionExecutor(TerraNPC terra)
        {
            this.terra = terra ?? throw new ArgumentNullException(nameof(terra));
            this.taskQueue = new Queue<Task>();
            this.currentAction = null;
            this.currentGoal = null;
            this.ticksSinceLastAction = 0;
            this.taskPlanner = null;
            this.idleFollowAction = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Processes a natural language command by planning tasks and queuing them for execution.
        /// </summary>
        /// <param name="command">The natural language command to process.</param>
        public async System.Threading.Tasks.Task ProcessNaturalLanguageCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Cancel any current action and clear the queue
            StopCurrentAction();

            try
            {
                // Plan tasks using the AI planner
                var response = await TaskPlanner.PlanTasksAsync(command);

                if (response != null)
                {
                    // Set the current goal from the plan
                    currentGoal = response.Plan;

                    // Update Terra's memory with the current goal
                    if (terra.Memory != null)
                    {
                        terra.Memory.CurrentGoal = currentGoal;
                    }

                    // Queue all tasks from the response
                    if (response.Tasks != null)
                    {
                        foreach (var task in response.Tasks)
                        {
                            taskQueue.Enqueue(task);
                        }
                    }

                    TerraAIMod.Instance?.Logger.Info($"Terra '{terra.TerraName}' planned {taskQueue.Count} tasks for goal: {currentGoal}");
                }
            }
            catch (Exception ex)
            {
                TerraAIMod.Instance?.Logger.Error($"Error processing command: {ex.Message}");
                terra.SendChatMessage($"I had trouble understanding that command: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a command asynchronously (alias for ProcessNaturalLanguageCommand).
        /// </summary>
        /// <param name="command">The command to process.</param>
        public System.Threading.Tasks.Task ProcessCommandAsync(string command)
        {
            return ProcessNaturalLanguageCommand(command);
        }

        /// <summary>
        /// Updates the action executor each game tick.
        /// Handles action completion, task dequeuing, and idle behavior.
        /// </summary>
        public void Tick()
        {
            // Check if current action is complete
            if (currentAction != null && currentAction.IsComplete)
            {
                // Log completion to memory
                LogActionCompletion(currentAction);
                currentAction = null;
            }

            // If we have an active action, tick it and return
            if (currentAction != null)
            {
                currentAction.Tick();
                return;
            }

            // Increment ticks since last action
            ticksSinceLastAction++;

            // Get the configured action delay
            int actionDelay = GetActionTickDelay();

            // Check if we should dequeue a new task
            if (ticksSinceLastAction >= actionDelay && taskQueue.Count > 0)
            {
                var task = taskQueue.Dequeue();
                ExecuteTask(task);
                ticksSinceLastAction = 0;
            }

            // If idle (no action, no queue, no goal), start idle follow behavior
            if (currentAction == null && taskQueue.Count == 0 && string.IsNullOrEmpty(currentGoal))
            {
                StartIdleFollowAction();
            }
        }

        /// <summary>
        /// Stops the current action and clears the task queue.
        /// </summary>
        public void StopCurrentAction()
        {
            // Cancel the current action if one exists
            if (currentAction != null)
            {
                currentAction.Cancel();
                currentAction = null;
            }

            // Clear the task queue
            taskQueue.Clear();

            // Clear the current goal
            currentGoal = null;

            // Update Terra's memory
            if (terra.Memory != null)
            {
                terra.Memory.CurrentGoal = null;
            }

            // Reset idle action if it was running
            idleFollowAction = null;

            ticksSinceLastAction = 0;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Executes a task by creating and starting the appropriate action.
        /// </summary>
        /// <param name="task">The task to execute.</param>
        private void ExecuteTask(Task task)
        {
            if (task == null)
                return;

            var action = CreateAction(task);

            if (action != null)
            {
                currentAction = action;
                currentAction.Start();

                TerraAIMod.Instance?.Logger.Info($"Terra '{terra.TerraName}' started action: {action.Description}");
            }
            else
            {
                TerraAIMod.Instance?.Logger.Warn($"Terra '{terra.TerraName}' could not create action for task: {task.Action}");
            }
        }

        /// <summary>
        /// Creates an action instance based on the task type.
        /// </summary>
        /// <param name="task">The task to create an action for.</param>
        /// <returns>The created action, or null if the task type is unknown.</returns>
        private BaseAction CreateAction(Task task)
        {
            if (task == null)
                return null;

            switch (task.Action?.ToLowerInvariant())
            {
                case "pathfind":
                    return new PathfindAction(terra, task);

                case "mine":
                    return new MineTileAction(terra, task);

                case "place":
                    return new PlaceTileAction(terra, task);

                case "build":
                    return new BuildStructureAction(terra, task);

                case "attack":
                    return new CombatAction(terra, task);

                case "follow":
                    return new FollowPlayerAction(terra, task);

                case "dig":
                    // Dig is an alias for mine with direction support
                    return new DigAction(terra, task);

                default:
                    TerraAIMod.Instance?.Logger.Warn($"Unknown action type: {task.Action}");
                    return null;
            }
        }

        /// <summary>
        /// Starts the idle follow action when Terra has nothing else to do.
        /// </summary>
        private void StartIdleFollowAction()
        {
            // Only start if not already following
            if (idleFollowAction != null && !idleFollowAction.IsComplete)
                return;

            // Create a follow task for idle behavior
            var followTask = new Task("follow", new Dictionary<string, object>
            {
                { "target", "player" },
                { "distance", 100f }
            });

            idleFollowAction = new FollowPlayerAction(terra, followTask);
            currentAction = idleFollowAction;
            currentAction.Start();
        }

        /// <summary>
        /// Logs action completion to Terra's memory.
        /// </summary>
        /// <param name="action">The completed action.</param>
        private void LogActionCompletion(BaseAction action)
        {
            if (action == null || terra.Memory == null)
                return;

            var result = action.Result;
            string logMessage = result != null
                ? $"Action '{action.Description}' {(result.Success ? "completed successfully" : "failed")}: {result.Message}"
                : $"Action '{action.Description}' completed";

            terra.Memory.AddAction(logMessage);

            // If this was the last task and the goal is complete, clear the goal
            if (taskQueue.Count == 0 && result != null && result.Success)
            {
                // Goal completed
                if (!string.IsNullOrEmpty(currentGoal))
                {
                    terra.Memory.AddAction($"Goal completed: {currentGoal}");
                    terra.SendChatMessage($"I've completed: {currentGoal}");
                    currentGoal = null;
                    terra.Memory.CurrentGoal = null;
                }
            }
            else if (result != null && !result.Success && result.RequiresReplanning)
            {
                // Action failed and requires replanning
                TerraAIMod.Instance?.Logger.Info($"Action failed, may require replanning: {result.Message}");
            }
        }

        /// <summary>
        /// Gets the configured action tick delay from the mod config.
        /// </summary>
        /// <returns>The number of ticks to wait between actions.</returns>
        private int GetActionTickDelay()
        {
            var config = ModContent.GetInstance<TerraConfig>();
            return config?.ActionTickDelay ?? 30;
        }

        #endregion
    }

    #region SimpleTaskPlanner Class

    /// <summary>
    /// Simple fallback task planner using keyword matching.
    /// Used when no AI LLM client is configured.
    /// </summary>
    public class SimpleTaskPlanner
    {
        private readonly TerraNPC terra;

        /// <summary>
        /// Creates a new SimpleTaskPlanner for the specified Terra NPC.
        /// </summary>
        /// <param name="terra">The Terra NPC to plan tasks for.</param>
        public SimpleTaskPlanner(TerraNPC terra)
        {
            this.terra = terra;
        }

        /// <summary>
        /// Plans tasks asynchronously based on a natural language command.
        /// </summary>
        /// <param name="command">The command to plan tasks for.</param>
        /// <returns>A TaskPlanResponse containing the plan and tasks.</returns>
        public async System.Threading.Tasks.Task<TaskPlanResponse> PlanTasksAsync(string command)
        {
            // TODO: Implement AI-driven task planning using ILLMClient
            // For now, return a simple response based on keyword matching

            var response = new TaskPlanResponse();
            var tasks = new List<Task>();

            string lowerCommand = command.ToLowerInvariant();

            if (lowerCommand.Contains("follow"))
            {
                response.Plan = "Following the player";
                tasks.Add(new Task("follow", new Dictionary<string, object>
                {
                    { "target", "player" }
                }));
            }
            else if (lowerCommand.Contains("mine") || lowerCommand.Contains("dig"))
            {
                response.Plan = "Mining blocks";
                tasks.Add(new Task("mine", new Dictionary<string, object>
                {
                    { "direction", "down" }
                }));
            }
            else if (lowerCommand.Contains("build"))
            {
                response.Plan = "Building a structure";
                tasks.Add(new Task("build", new Dictionary<string, object>
                {
                    { "type", "house" }
                }));
            }
            else if (lowerCommand.Contains("attack") || lowerCommand.Contains("fight"))
            {
                response.Plan = "Engaging in combat";
                tasks.Add(new Task("attack", new Dictionary<string, object>
                {
                    { "target", "nearest" }
                }));
            }
            else if (lowerCommand.Contains("go to") || lowerCommand.Contains("move to"))
            {
                response.Plan = "Moving to location";
                tasks.Add(new Task("pathfind", new Dictionary<string, object>()));
            }
            else
            {
                response.Plan = "Following the player";
                tasks.Add(new Task("follow", new Dictionary<string, object>
                {
                    { "target", "player" }
                }));
            }

            response.Tasks = tasks;
            return response;
        }
    }

    /// <summary>
    /// Response from the task planner containing the plan and tasks.
    /// </summary>
    public class TaskPlanResponse
    {
        /// <summary>
        /// Human-readable description of the planned goal.
        /// </summary>
        public string Plan { get; set; }

        /// <summary>
        /// List of tasks to execute to achieve the goal.
        /// </summary>
        public List<Task> Tasks { get; set; } = new List<Task>();
    }

    #endregion

}
