using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TerraAIMod.NPCs;

namespace TerraAIMod.Memory
{
    /// <summary>
    /// Terra's memory system for storing goals, tasks, and recent actions.
    /// Provides persistence through TagCompound serialization.
    /// </summary>
    public class TerraMemory
    {
        #region Constants

        /// <summary>
        /// Maximum number of recent actions to keep in memory.
        /// </summary>
        private const int MAX_RECENT_ACTIONS = 20;

        #endregion

        #region Fields

        /// <summary>
        /// Reference to the Terra NPC that owns this memory.
        /// </summary>
        private TerraNPC terra;

        /// <summary>
        /// The current high-level goal Terra is working towards.
        /// </summary>
        private string currentGoal;

        /// <summary>
        /// Queue of tasks to be executed in order.
        /// </summary>
        private Queue<string> taskQueue;

        /// <summary>
        /// List of recent actions performed, newest at the end.
        /// </summary>
        private LinkedList<string> recentActions;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the current goal Terra is working towards.
        /// </summary>
        public string CurrentGoal
        {
            get => currentGoal;
            set => currentGoal = value;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new TerraMemory instance.
        /// </summary>
        public TerraMemory()
        {
            taskQueue = new Queue<string>();
            recentActions = new LinkedList<string>();
            currentGoal = null;
        }

        /// <summary>
        /// Creates a new TerraMemory instance with a reference to the owning NPC.
        /// </summary>
        /// <param name="terra">The TerraNPC that owns this memory.</param>
        public TerraMemory(TerraNPC terra) : this()
        {
            this.terra = terra;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the Terra NPC reference.
        /// </summary>
        /// <param name="terra">The TerraNPC that owns this memory.</param>
        public void SetTerra(TerraNPC terra)
        {
            this.terra = terra;
        }

        /// <summary>
        /// Adds an action to the recent actions list.
        /// Trims the list if it exceeds MAX_RECENT_ACTIONS.
        /// </summary>
        /// <param name="action">The action description to add.</param>
        public void AddAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            recentActions.AddLast(action);

            // Trim if we exceed the maximum
            while (recentActions.Count > MAX_RECENT_ACTIONS)
            {
                recentActions.RemoveFirst();
            }
        }

        /// <summary>
        /// Gets the most recent actions performed.
        /// </summary>
        /// <param name="count">The number of recent actions to retrieve.</param>
        /// <returns>A list of the most recent actions, newest last.</returns>
        public List<string> GetRecentActions(int count)
        {
            if (count <= 0)
                return new List<string>();

            // Take the last 'count' items from the linked list
            return recentActions
                .Skip(System.Math.Max(0, recentActions.Count - count))
                .ToList();
        }

        /// <summary>
        /// Enqueues a task to be executed.
        /// </summary>
        /// <param name="task">The task description to enqueue.</param>
        public void EnqueueTask(string task)
        {
            if (!string.IsNullOrWhiteSpace(task))
            {
                taskQueue.Enqueue(task);
            }
        }

        /// <summary>
        /// Dequeues the next task to be executed.
        /// </summary>
        /// <returns>The next task, or null if the queue is empty.</returns>
        public string DequeueTask()
        {
            return taskQueue.Count > 0 ? taskQueue.Dequeue() : null;
        }

        /// <summary>
        /// Peeks at the next task without removing it.
        /// </summary>
        /// <returns>The next task, or null if the queue is empty.</returns>
        public string PeekTask()
        {
            return taskQueue.Count > 0 ? taskQueue.Peek() : null;
        }

        /// <summary>
        /// Gets the number of tasks in the queue.
        /// </summary>
        /// <returns>The number of pending tasks.</returns>
        public int GetTaskCount()
        {
            return taskQueue.Count;
        }

        /// <summary>
        /// Clears the task queue and resets the current goal.
        /// </summary>
        public void ClearTaskQueue()
        {
            taskQueue.Clear();
            currentGoal = null;
        }

        /// <summary>
        /// Adds a message to the conversation history.
        /// This is a compatibility method for the existing placeholder interface.
        /// </summary>
        /// <param name="role">The role of the message sender (user/assistant).</param>
        /// <param name="content">The message content.</param>
        public void AddMessage(string role, string content)
        {
            // Record as an action for now
            AddAction($"[{role}]: {content}");
        }

        /// <summary>
        /// Saves the memory state to a TagCompound for persistence.
        /// </summary>
        /// <returns>A TagCompound containing the serialized memory state.</returns>
        public TagCompound Save()
        {
            var tag = new TagCompound();

            // Save current goal
            if (!string.IsNullOrEmpty(currentGoal))
            {
                tag["CurrentGoal"] = currentGoal;
            }

            // Save recent actions as a list
            if (recentActions.Count > 0)
            {
                tag["RecentActions"] = recentActions.ToList();
            }

            // Note: Task queue is intentionally not saved as tasks are transient

            return tag;
        }

        /// <summary>
        /// Loads the memory state from a TagCompound.
        /// </summary>
        /// <param name="tag">The TagCompound containing the serialized memory state.</param>
        public void Load(TagCompound tag)
        {
            // Clear existing state
            taskQueue.Clear();
            recentActions.Clear();

            // Load current goal
            if (tag.ContainsKey("CurrentGoal"))
            {
                currentGoal = tag.GetString("CurrentGoal");
            }
            else
            {
                currentGoal = null;
            }

            // Load recent actions
            if (tag.ContainsKey("RecentActions"))
            {
                var actions = tag.GetList<string>("RecentActions");
                foreach (var action in actions)
                {
                    recentActions.AddLast(action);
                }
            }
        }

        #endregion
    }
}
