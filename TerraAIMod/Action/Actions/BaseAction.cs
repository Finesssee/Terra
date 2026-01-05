using TerraAIMod.Action;
using TerraAIMod.NPCs;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Abstract base class for all actions that can be executed by Terra.
    /// Provides lifecycle management (Start, Tick, Cancel) and result tracking.
    /// </summary>
    public abstract class BaseAction
    {
        /// <summary>
        /// Reference to the Terra NPC executing this action.
        /// </summary>
        protected readonly TerraNPC terra;

        /// <summary>
        /// The task being executed by this action.
        /// </summary>
        protected readonly Task task;

        /// <summary>
        /// The result of the action, set when complete.
        /// </summary>
        protected ActionResult result;

        /// <summary>
        /// Whether the action has been started.
        /// </summary>
        protected bool started;

        /// <summary>
        /// Whether the action has been cancelled.
        /// </summary>
        protected bool cancelled;

        /// <summary>
        /// Creates a new action for the specified Terra NPC and task.
        /// </summary>
        /// <param name="terra">The Terra NPC that will execute this action.</param>
        /// <param name="task">The task to execute.</param>
        protected BaseAction(TerraNPC terra, Task task)
        {
            this.terra = terra;
            this.task = task;
            this.result = null;
            this.started = false;
            this.cancelled = false;
        }

        /// <summary>
        /// Starts the action. Can only be called once.
        /// </summary>
        public void Start()
        {
            if (!started)
            {
                TerraAIMod.Instance?.Logger.Debug($"[BaseAction.Start] {GetType().Name} '{Description}' - Starting action for Terra '{terra.TerraName}'");
                started = true;
                OnStart();
                TerraAIMod.Instance?.Logger.Debug($"[BaseAction.Start] {GetType().Name} '{Description}' - OnStart() completed, IsComplete={IsComplete}");
            }
            else
            {
                TerraAIMod.Instance?.Logger.Debug($"[BaseAction.Start] {GetType().Name} '{Description}' - Already started, ignoring Start() call");
            }
        }

        /// <summary>
        /// Updates the action each game tick.
        /// Only processes if started and not yet complete.
        /// </summary>
        public void Tick()
        {
            if (started && !IsComplete)
            {
                OnTick();
            }
        }

        /// <summary>
        /// Cancels the action, marking it as failed.
        /// </summary>
        public void Cancel()
        {
            TerraAIMod.Instance?.Logger.Debug($"[BaseAction.Cancel] {GetType().Name} '{Description}' - Cancelling action for Terra '{terra.TerraName}'");
            cancelled = true;
            result = ActionResult.Fail("Cancelled");
            OnCancel();
            TerraAIMod.Instance?.Logger.Debug($"[BaseAction.Cancel] {GetType().Name} '{Description}' - Action cancelled");
        }

        /// <summary>
        /// Whether the action has completed (either succeeded, failed, or was cancelled).
        /// </summary>
        public bool IsComplete => result != null || cancelled;

        /// <summary>
        /// The result of the action. Null until the action is complete.
        /// </summary>
        public ActionResult Result => result;

        /// <summary>
        /// Called when the action starts. Override to implement initialization logic.
        /// </summary>
        protected abstract void OnStart();

        /// <summary>
        /// Called each game tick while the action is running. Override to implement update logic.
        /// </summary>
        protected abstract void OnTick();

        /// <summary>
        /// Called when the action is cancelled. Override to implement cleanup logic.
        /// </summary>
        protected abstract void OnCancel();

        /// <summary>
        /// A human-readable description of what this action does.
        /// </summary>
        public abstract string Description { get; }
    }
}
