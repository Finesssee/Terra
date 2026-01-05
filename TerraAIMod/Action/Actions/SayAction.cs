using TerraAIMod.NPCs;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action that sends a chat message from Terra to the player.
    /// Used for greetings, responses, and conversational interactions.
    /// </summary>
    public class SayAction : BaseAction
    {
        private readonly string message;

        /// <summary>
        /// Creates a new SayAction with the specified message.
        /// </summary>
        /// <param name="terra">The Terra NPC that will speak.</param>
        /// <param name="task">The task containing the message parameter.</param>
        public SayAction(TerraNPC terra, Task task) : base(terra, task)
        {
            message = task.GetParameter<string>("message", "...");
            TerraAIMod.Instance?.Logger.Info($"SayAction created with message: {message}");
        }

        /// <summary>
        /// A human-readable description of this action.
        /// </summary>
        public override string Description => $"Say: {(message.Length > 50 ? message.Substring(0, 47) + "..." : message)}";

        /// <summary>
        /// Called when the action starts. Sends the chat message immediately.
        /// </summary>
        protected override void OnStart()
        {
            TerraAIMod.Instance?.Logger.Info($"SayAction.OnStart() - Sending message: {message}");

            // Send the message to chat
            terra.SendChatMessage(message);

            // Mark as complete immediately
            result = ActionResult.Succeed($"Said: {message}");

            TerraAIMod.Instance?.Logger.Info($"SayAction completed successfully");
        }

        /// <summary>
        /// Called each tick. No ongoing processing needed for say action.
        /// </summary>
        protected override void OnTick()
        {
            // Nothing to do - action completes immediately in OnStart
        }

        /// <summary>
        /// Called when the action is cancelled. No cleanup needed.
        /// </summary>
        protected override void OnCancel()
        {
            // Nothing to clean up
        }
    }
}
