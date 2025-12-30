using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria.ModLoader.IO;
using TerraAIMod.NPCs;

namespace TerraAIMod.Memory
{
    /// <summary>
    /// Terra's memory system for storing goals, tasks, conversation history, and recent actions.
    /// Provides persistence through TagCompound serialization.
    /// </summary>
    public class TerraMemory
    {
        #region Constants

        /// <summary>
        /// Maximum number of recent actions to keep in memory.
        /// </summary>
        private const int MAX_RECENT_ACTIONS = 50;

        /// <summary>
        /// Maximum number of conversation messages to keep in memory.
        /// </summary>
        private const int MAX_CONVERSATION_HISTORY = 20;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a single message in conversation history.
        /// </summary>
        public class ConversationMessage
        {
            public string Role { get; set; }
            public string Content { get; set; }
            public DateTime Timestamp { get; set; }

            public ConversationMessage(string role, string content)
            {
                Role = role;
                Content = content;
                Timestamp = DateTime.Now;
            }
        }

        /// <summary>
        /// Represents an action record with metadata.
        /// </summary>
        public class ActionRecord
        {
            public string Action { get; set; }
            public bool Success { get; set; }
            public DateTime Timestamp { get; set; }

            public ActionRecord(string action, bool success = true)
            {
                Action = action;
                Success = success;
                Timestamp = DateTime.Now;
            }
        }

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
        private LinkedList<ActionRecord> recentActions;

        /// <summary>
        /// Conversation history with the player.
        /// </summary>
        private LinkedList<ConversationMessage> conversationHistory;

        /// <summary>
        /// Important facts or observations Terra has learned.
        /// </summary>
        private Dictionary<string, string> learnedFacts;

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
            recentActions = new LinkedList<ActionRecord>();
            conversationHistory = new LinkedList<ConversationMessage>();
            learnedFacts = new Dictionary<string, string>();
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
        /// <param name="success">Whether the action was successful.</param>
        public void AddAction(string action, bool success = true)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            recentActions.AddLast(new ActionRecord(action, success));

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
        /// <returns>A list of the most recent action descriptions, newest last.</returns>
        public List<string> GetRecentActions(int count)
        {
            if (count <= 0)
                return new List<string>();

            // Take the last 'count' items from the linked list
            return recentActions
                .Skip(System.Math.Max(0, recentActions.Count - count))
                .Select(r => r.Success ? r.Action : $"[FAILED] {r.Action}")
                .ToList();
        }

        /// <summary>
        /// Gets the most recent action records with full metadata.
        /// </summary>
        /// <param name="count">The number of recent actions to retrieve.</param>
        /// <returns>A list of action records.</returns>
        public List<ActionRecord> GetRecentActionRecords(int count)
        {
            if (count <= 0)
                return new List<ActionRecord>();

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
        /// </summary>
        /// <param name="role">The role of the message sender (user/assistant).</param>
        /// <param name="content">The message content.</param>
        public void AddMessage(string role, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            conversationHistory.AddLast(new ConversationMessage(role, content));

            // Trim if we exceed the maximum
            while (conversationHistory.Count > MAX_CONVERSATION_HISTORY)
            {
                conversationHistory.RemoveFirst();
            }
        }

        /// <summary>
        /// Gets the conversation history as a list of messages.
        /// </summary>
        /// <param name="count">Maximum number of messages to retrieve.</param>
        /// <returns>A list of conversation messages, oldest first.</returns>
        public List<ConversationMessage> GetConversationHistory(int count = MAX_CONVERSATION_HISTORY)
        {
            if (count <= 0)
                return new List<ConversationMessage>();

            return conversationHistory
                .Skip(System.Math.Max(0, conversationHistory.Count - count))
                .ToList();
        }

        /// <summary>
        /// Clears the conversation history.
        /// </summary>
        public void ClearConversationHistory()
        {
            conversationHistory.Clear();
        }

        /// <summary>
        /// Stores a learned fact with a key for later retrieval.
        /// </summary>
        /// <param name="key">The key to identify this fact.</param>
        /// <param name="value">The fact value.</param>
        public void SetFact(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            learnedFacts[key] = value;
        }

        /// <summary>
        /// Retrieves a learned fact by key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>The fact value, or null if not found.</returns>
        public string GetFact(string key)
        {
            return learnedFacts.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Gets all learned facts.
        /// </summary>
        /// <returns>A dictionary of all learned facts.</returns>
        public Dictionary<string, string> GetAllFacts()
        {
            return new Dictionary<string, string>(learnedFacts);
        }

        /// <summary>
        /// Generates a context summary suitable for LLM prompts.
        /// Includes current goal, recent actions, and conversation context.
        /// </summary>
        /// <returns>A formatted context summary string.</returns>
        public string GetContextSummary()
        {
            var sb = new StringBuilder();

            // Current goal
            if (!string.IsNullOrEmpty(currentGoal))
            {
                sb.AppendLine($"Current Goal: {currentGoal}");
            }

            // Pending tasks
            if (taskQueue.Count > 0)
            {
                sb.AppendLine($"Pending Tasks ({taskQueue.Count}): {string.Join(", ", taskQueue.Take(5))}");
                if (taskQueue.Count > 5)
                    sb.AppendLine($"  ... and {taskQueue.Count - 5} more");
            }

            // Recent actions (last 10 for context)
            var actions = GetRecentActions(10);
            if (actions.Count > 0)
            {
                sb.AppendLine("Recent Actions:");
                foreach (var action in actions)
                {
                    sb.AppendLine($"  - {action}");
                }
            }

            // Conversation context (last 5 messages)
            var messages = GetConversationHistory(5);
            if (messages.Count > 0)
            {
                sb.AppendLine("Recent Conversation:");
                foreach (var msg in messages)
                {
                    sb.AppendLine($"  [{msg.Role}]: {msg.Content}");
                }
            }

            // Learned facts
            if (learnedFacts.Count > 0)
            {
                sb.AppendLine("Known Facts:");
                foreach (var fact in learnedFacts.Take(10))
                {
                    sb.AppendLine($"  - {fact.Key}: {fact.Value}");
                }
            }

            return sb.ToString();
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

            // Save recent actions as serialized data
            if (recentActions.Count > 0)
            {
                var actionList = recentActions.Select(a => new TagCompound
                {
                    ["Action"] = a.Action,
                    ["Success"] = a.Success
                }).ToList();
                tag["RecentActions"] = actionList;
            }

            // Save conversation history
            if (conversationHistory.Count > 0)
            {
                var messageList = conversationHistory.Select(m => new TagCompound
                {
                    ["Role"] = m.Role,
                    ["Content"] = m.Content
                }).ToList();
                tag["ConversationHistory"] = messageList;
            }

            // Save learned facts
            if (learnedFacts.Count > 0)
            {
                var factList = learnedFacts.Select(kvp => new TagCompound
                {
                    ["Key"] = kvp.Key,
                    ["Value"] = kvp.Value
                }).ToList();
                tag["LearnedFacts"] = factList;
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
            conversationHistory.Clear();
            learnedFacts.Clear();

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
                var actions = tag.GetList<TagCompound>("RecentActions");
                foreach (var actionTag in actions)
                {
                    var action = actionTag.GetString("Action");
                    var success = actionTag.GetBool("Success");
                    recentActions.AddLast(new ActionRecord(action, success));
                }
            }

            // Load conversation history
            if (tag.ContainsKey("ConversationHistory"))
            {
                var messages = tag.GetList<TagCompound>("ConversationHistory");
                foreach (var msgTag in messages)
                {
                    var role = msgTag.GetString("Role");
                    var content = msgTag.GetString("Content");
                    conversationHistory.AddLast(new ConversationMessage(role, content));
                }
            }

            // Load learned facts
            if (tag.ContainsKey("LearnedFacts"))
            {
                var facts = tag.GetList<TagCompound>("LearnedFacts");
                foreach (var factTag in facts)
                {
                    var key = factTag.GetString("Key");
                    var value = factTag.GetString("Value");
                    if (!string.IsNullOrEmpty(key))
                    {
                        learnedFacts[key] = value;
                    }
                }
            }
        }

        #endregion
    }
}
