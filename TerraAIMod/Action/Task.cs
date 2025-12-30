using System.Collections.Generic;

namespace TerraAIMod.Action
{
    /// <summary>
    /// Represents a task to be executed by an action.
    /// Contains the action type and associated parameters.
    /// </summary>
    public class Task
    {
        /// <summary>
        /// The action identifier/type for this task.
        /// </summary>
        public string Action { get; }

        /// <summary>
        /// Parameters associated with this task.
        /// </summary>
        public Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Creates a new task with the specified action and optional parameters.
        /// </summary>
        /// <param name="action">The action identifier/type.</param>
        /// <param name="parameters">Optional dictionary of parameters.</param>
        public Task(string action, Dictionary<string, object> parameters = null)
        {
            Action = action;
            Parameters = parameters ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets a parameter value with type conversion, returning a default value if not found.
        /// </summary>
        /// <typeparam name="T">The expected type of the parameter.</typeparam>
        /// <param name="key">The parameter key.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The parameter value cast to type T, or the default value.</returns>
        public T GetParameter<T>(string key, T defaultValue = default)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Attempt conversion for compatible types
                try
                {
                    return (T)System.Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Checks if a parameter with the specified key exists.
        /// </summary>
        /// <param name="key">The parameter key to check.</param>
        /// <returns>True if the parameter exists, false otherwise.</returns>
        public bool HasParameter(string key)
        {
            return Parameters.ContainsKey(key);
        }

        /// <summary>
        /// Checks if all specified parameters exist.
        /// </summary>
        /// <param name="keys">The parameter keys to check.</param>
        /// <returns>True if all parameters exist, false otherwise.</returns>
        public bool HasParameters(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!Parameters.ContainsKey(key))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
