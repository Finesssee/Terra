namespace TerraAIMod.Action
{
    /// <summary>
    /// Represents the result of an action execution.
    /// Indicates success/failure and whether replanning is required.
    /// </summary>
    public class ActionResult
    {
        /// <summary>
        /// Whether the action completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// A descriptive message about the action result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Whether the action failure requires replanning.
        /// Only relevant when Success is false.
        /// </summary>
        public bool RequiresReplanning { get; }

        /// <summary>
        /// Private constructor to enforce factory method usage.
        /// </summary>
        /// <param name="success">Whether the action succeeded.</param>
        /// <param name="message">The result message.</param>
        /// <param name="requiresReplanning">Whether replanning is required on failure.</param>
        private ActionResult(bool success, string message, bool requiresReplanning)
        {
            Success = success;
            Message = message;
            RequiresReplanning = requiresReplanning;
        }

        /// <summary>
        /// Creates a successful action result.
        /// </summary>
        /// <param name="message">A message describing the successful outcome.</param>
        /// <returns>A new ActionResult indicating success.</returns>
        public static ActionResult Succeed(string message)
        {
            return new ActionResult(true, message, false);
        }

        /// <summary>
        /// Creates a failed action result.
        /// </summary>
        /// <param name="message">A message describing the failure.</param>
        /// <param name="replan">Whether this failure requires replanning. Defaults to true.</param>
        /// <returns>A new ActionResult indicating failure.</returns>
        public static ActionResult Fail(string message, bool replan = true)
        {
            return new ActionResult(false, message, replan);
        }
    }
}
