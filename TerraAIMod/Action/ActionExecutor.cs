using System;
using System.Collections.Concurrent;
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
    /// Thread-safe: Commands are queued from background threads and processed on the main thread.
    /// </summary>
    public class ActionExecutor
    {
        #region Fields

        /// <summary>
        /// Reference to the Terra NPC this executor controls.
        /// </summary>
        private readonly TerraNPC terra;

        /// <summary>
        /// Queue of tasks waiting to be executed. Only accessed from main thread.
        /// </summary>
        private readonly Queue<Task> taskQueue;

        /// <summary>
        /// Thread-safe queue for pending command results from background async processing.
        /// Results are queued from background threads and dequeued on main thread.
        /// </summary>
        private readonly ConcurrentQueue<PendingCommandResult> pendingCommandResults;

        /// <summary>
        /// Thread-safe queue for pending chat messages to be sent on main thread.
        /// </summary>
        private readonly ConcurrentQueue<string> pendingChatMessages;

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
        /// Lazy-initialized AI task planner for AI-driven task decomposition.
        /// </summary>
        private TaskPlanner aiTaskPlanner;

        /// <summary>
        /// Lazy-initialized simple fallback task planner when no API key is configured.
        /// </summary>
        private SimpleTaskPlanner simpleTaskPlanner;

        /// <summary>
        /// Action to execute when idle (following the player).
        /// </summary>
        private BaseAction idleFollowAction;

        /// <summary>
        /// Flag indicating that a stop was requested from background thread.
        /// </summary>
        private volatile bool stopRequested;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents the result of an async command processing operation.
        /// </summary>
        private class PendingCommandResult
        {
            public string Plan { get; set; }
            public List<Task> Tasks { get; set; }
            public string ErrorMessage { get; set; }
            public bool IsError { get; set; }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Whether an action is currently executing or tasks are queued.
        /// </summary>
        public bool IsExecuting => currentAction != null || taskQueue.Count > 0 || !pendingCommandResults.IsEmpty;

        /// <summary>
        /// The current goal description, or null if no active goal.
        /// </summary>
        public string CurrentGoal => currentGoal;

        /// <summary>
        /// Gets the AI task planner, initializing it lazily if needed.
        /// </summary>
        private TaskPlanner AITaskPlanner
        {
            get
            {
                if (aiTaskPlanner == null)
                {
                    aiTaskPlanner = new TaskPlanner();
                }
                return aiTaskPlanner;
            }
        }

        /// <summary>
        /// Gets the simple task planner, initializing it lazily if needed.
        /// </summary>
        private SimpleTaskPlanner SimpleTaskPlannerInstance
        {
            get
            {
                if (simpleTaskPlanner == null)
                {
                    simpleTaskPlanner = new SimpleTaskPlanner(terra);
                }
                return simpleTaskPlanner;
            }
        }

        /// <summary>
        /// Checks if an API key is configured for the current AI provider.
        /// </summary>
        /// <returns>True if an API key is configured, false otherwise.</returns>
        private bool HasApiKeyConfigured()
        {
            var config = ModContent.GetInstance<TerraConfig>();
            if (config == null)
                return false;

            string provider = config.AIProvider?.ToLowerInvariant() ?? "groq";

            return provider switch
            {
                "openai" => !string.IsNullOrWhiteSpace(config.OpenAIApiKey),
                "gemini" => !string.IsNullOrWhiteSpace(config.GeminiApiKey),
                "groq" => !string.IsNullOrWhiteSpace(config.GroqApiKey),
                _ => !string.IsNullOrWhiteSpace(config.GroqApiKey)
            };
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
            this.pendingCommandResults = new ConcurrentQueue<PendingCommandResult>();
            this.pendingChatMessages = new ConcurrentQueue<string>();
            this.currentAction = null;
            this.currentGoal = null;
            this.ticksSinceLastAction = 0;
            this.aiTaskPlanner = null;
            this.simpleTaskPlanner = null;
            this.idleFollowAction = null;
            this.stopRequested = false;
            TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor] Created ActionExecutor for Terra '{terra.TerraName}'");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Processes a natural language command by planning tasks and queuing them for execution.
        /// Uses the AI-powered TaskPlanner if an API key is configured, otherwise falls back to SimpleTaskPlanner.
        /// This method is thread-safe: it queues results to be processed on the main thread.
        /// </summary>
        /// <param name="command">The natural language command to process.</param>
        public async System.Threading.Tasks.Task ProcessNaturalLanguageCommand(string command)
        {
            TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ProcessNaturalLanguageCommand] ENTER - Terra '{terra.TerraName}', command='{command}'");

            if (string.IsNullOrWhiteSpace(command))
            {
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ProcessNaturalLanguageCommand] EXIT - Empty command, returning early");
                return;
            }

            // Signal that we want to stop current action (will be processed on main thread)
            stopRequested = true;
            TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ProcessNaturalLanguageCommand] Set stopRequested=true");

            try
            {
                TaskPlanResponse response;

                // Check if an API key is configured - use AI TaskPlanner if so, otherwise fall back to simple planner
                if (HasApiKeyConfigured())
                {
                    // Use the AI-powered TaskPlanner
                    var parsedResponse = await AITaskPlanner.PlanTasksAsync(terra, command);

                    // Map ParsedResponse to TaskPlanResponse
                    response = MapParsedResponseToTaskPlanResponse(parsedResponse);
                }
                else
                {
                    // Fall back to simple task planner when no API key is configured
                    TerraAIMod.Instance?.Logger.Info("No API key configured, using SimpleTaskPlanner fallback");
                    response = await SimpleTaskPlannerInstance.PlanTasksAsync(command);
                }

                if (response != null)
                {
                    // Queue the result to be processed on the main thread
                    pendingCommandResults.Enqueue(new PendingCommandResult
                    {
                        Plan = response.Plan,
                        Tasks = response.Tasks ?? new List<Task>(),
                        IsError = false
                    });

                    TerraAIMod.Instance?.Logger.Info($"Terra '{terra.TerraName}' planned {response.Tasks?.Count ?? 0} tasks for goal: {response.Plan}");
                }
            }
            catch (Exception ex)
            {
                TerraAIMod.Instance?.Logger.Error($"Error processing command: {ex.Message}");

                // Queue the error message to be displayed on main thread
                pendingCommandResults.Enqueue(new PendingCommandResult
                {
                    ErrorMessage = $"I had trouble understanding that command: {ex.Message}",
                    IsError = true
                });
            }
        }

        /// <summary>
        /// Maps a ParsedResponse from the AI TaskPlanner to the TaskPlanResponse format expected by ActionExecutor.
        /// </summary>
        /// <param name="parsedResponse">The parsed response from the AI TaskPlanner.</param>
        /// <returns>A TaskPlanResponse with the plan and tasks.</returns>
        private TaskPlanResponse MapParsedResponseToTaskPlanResponse(ParsedResponse parsedResponse)
        {
            var response = new TaskPlanResponse();

            if (parsedResponse == null)
            {
                response.Plan = "Failed to get response from AI";
                response.Tasks = new List<Task>();
                return response;
            }

            if (!parsedResponse.Success)
            {
                // If parsing failed, log the error but still try to use any extracted tasks
                TerraAIMod.Instance?.Logger.Warn($"AI response parsing issue: {parsedResponse.Error}");
            }

            // Map the plan - use Reasoning + Plan, or just Plan if Reasoning is empty
            if (!string.IsNullOrEmpty(parsedResponse.Reasoning) && !string.IsNullOrEmpty(parsedResponse.Plan))
            {
                response.Plan = parsedResponse.Plan;
            }
            else if (!string.IsNullOrEmpty(parsedResponse.Plan))
            {
                response.Plan = parsedResponse.Plan;
            }
            else if (!string.IsNullOrEmpty(parsedResponse.Reasoning))
            {
                response.Plan = parsedResponse.Reasoning;
            }
            else
            {
                response.Plan = "Executing command";
            }

            // Map tasks directly - they use the same Task class
            response.Tasks = parsedResponse.Tasks ?? new List<Task>();

            return response;
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
        /// Queues a chat message to be sent on the main thread.
        /// Thread-safe method that can be called from any thread.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void QueueChatMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                pendingChatMessages.Enqueue(message);
            }
        }

        /// <summary>
        /// Updates the action executor each game tick.
        /// Handles action completion, task dequeuing, and idle behavior.
        /// Must be called from the main thread.
        /// </summary>
        public void Tick()
        {
            // Debug: Log tick entry periodically (every 60 ticks to avoid spam)
            if (ticksSinceLastAction % 60 == 0)
            {
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.Tick] Terra '{terra.TerraName}': ticksSinceLastAction={ticksSinceLastAction}, queueCount={taskQueue.Count}, hasCurrentAction={currentAction != null}, currentGoal='{currentGoal}'");
            }

            // Process any pending chat messages from background threads
            ProcessPendingChatMessages();

            // Check if stop was requested from background thread
            if (stopRequested)
            {
                StopCurrentActionInternal();
                stopRequested = false;
            }

            // Process any pending command results from background threads
            ProcessPendingCommandResults();

            // Check if current action is complete
            if (currentAction != null && currentAction.IsComplete)
            {
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.Tick] Terra '{terra.TerraName}': Current action '{currentAction.Description}' is COMPLETE. Result={(currentAction.Result != null ? (currentAction.Result.Success ? "Success" : "Failed") : "null")}");
                // Log completion to memory
                LogActionCompletion(currentAction);
                currentAction = null;
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.Tick] Terra '{terra.TerraName}': currentAction set to null after completion");
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
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.Tick] Terra '{terra.TerraName}': DEQUEUING task '{task.Action}' (ticksSinceLastAction={ticksSinceLastAction} >= actionDelay={actionDelay}). Remaining queue count: {taskQueue.Count}");
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
        /// Should only be called from main thread.
        /// </summary>
        public void StopCurrentAction()
        {
            StopCurrentActionInternal();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Processes pending chat messages queued from background threads.
        /// </summary>
        private void ProcessPendingChatMessages()
        {
            while (pendingChatMessages.TryDequeue(out string message))
            {
                terra.SendChatMessage(message);
            }
        }

        /// <summary>
        /// Processes pending command results from background async operations.
        /// </summary>
        private void ProcessPendingCommandResults()
        {
            while (pendingCommandResults.TryDequeue(out PendingCommandResult result))
            {
                if (result.IsError)
                {
                    // Send error message
                    terra.SendChatMessage(result.ErrorMessage);
                }
                else
                {
                    // Apply the command result
                    currentGoal = result.Plan;

                    // Update Terra's memory with the current goal
                    if (terra.Memory != null)
                    {
                        terra.Memory.CurrentGoal = currentGoal;
                    }

                    // Queue all tasks from the response
                    if (result.Tasks != null)
                    {
                        foreach (var task in result.Tasks)
                        {
                            TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ProcessPendingCommandResults] ENQUEUE task: Action='{task.Action}'");
                            taskQueue.Enqueue(task);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Internal implementation of StopCurrentAction.
        /// </summary>
        private void StopCurrentActionInternal()
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

        /// <summary>
        /// Executes a task by creating and starting the appropriate action.
        /// </summary>
        /// <param name="task">The task to execute.</param>
        private void ExecuteTask(Task task)
        {
            TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ExecuteTask] ENTER - Terra '{terra.TerraName}', task.Action='{task?.Action}'");

            if (task == null)
            {
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ExecuteTask] EXIT - Task was null");
                return;
            }

            TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ExecuteTask] Creating action for task '{task.Action}'");
            var action = CreateAction(task);

            if (action != null)
            {
                currentAction = action;
                TerraAIMod.Instance?.Logger.Debug($"[ActionExecutor.ExecuteTask] Calling Start() on action '{action.Description}'");
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

                case "say":
                    // Say action sends a chat message immediately
                    return new SayAction(terra, task);

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
        public System.Threading.Tasks.Task<TaskPlanResponse> PlanTasksAsync(string command)
        {
            // Simple keyword-based task planning (no async needed)
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
            return System.Threading.Tasks.Task.FromResult(response);
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
