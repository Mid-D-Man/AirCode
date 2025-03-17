using System;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AirCode.Utilities.HelperScripts
{
    /// <summary>
    /// Debug message type enumeration
    /// </summary>
    public enum DebugClass
    {
        Log,
        Warning,
        Error,
        Exception,
        Info
    }

    /// <summary>
    /// Helper functions for common operations
    /// </summary>
    public static class MID_HelperFunctions
    {
        // Set this to false in production environment
        private static bool IsDebugMode = true;

        /// <summary>
        /// Validates if a string is not null, empty, or variations of "NULL"
        /// </summary>
        /// <param name="input">The string to validate</param>
        /// <returns>True if the string is valid, false otherwise</returns>
        public static bool IsValidString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Check for variations of "NULL"
            string upperInput = input.ToUpper().Trim();
            if (upperInput == "NULL" || upperInput == "UNDEFINED" || upperInput == "NONE")
                return false;

            return true;
        }

        /// <summary>
        /// Validates if a string matches a specific regex pattern
        /// </summary>
        /// <param name="input">The string to validate</param>
        /// <param name="pattern">The regex pattern to match against</param>
        /// <returns>True if the string matches the pattern, false otherwise</returns>
        public static bool IsValidPattern(string input, string pattern)
        {
            if (!IsValidString(input))
                return false;

            try
            {
                return Regex.IsMatch(input, pattern);
            }
            catch (Exception ex)
            {
                DebugMessage($"Regex validation error: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        /// <summary>
        /// Prints debug messages based on debug mode setting
        /// </summary>
        /// <param name="message">The message to print</param>
        /// <param name="debugClass">The type of debug message</param>
        public static void DebugMessage(string message, DebugClass debugClass = DebugClass.Log)
        {
            if (!IsDebugMode)
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string prefix = $"[{timestamp}] [{debugClass}]";

            switch (debugClass)
            {
                case DebugClass.Warning:
                    Console.WriteLine($"{prefix} WARNING: {message}");
                    break;
                case DebugClass.Error:
                    Console.WriteLine($"{prefix} ERROR: {message}");
                    break;
                case DebugClass.Exception:
                    Console.WriteLine($"{prefix} EXCEPTION: {message}");
                    break;
                case DebugClass.Info:
                    Console.WriteLine($"{prefix} INFO: {message}");
                    break;
                default:
                    Console.WriteLine($"{prefix} LOG: {message}");
                    break;
            }

            // Add additional logging to file or service if needed
        }

        /// <summary>
        /// Get the current environment (Development, Production, etc.)
        /// </summary>
        /// <returns>The current environment name</returns>
        public static string GetEnvironment()
        {
            #if DEBUG
                return "Development";
            #else
                return "Production";
            #endif
        }

        /// <summary>
        /// Set the debug mode (typically called on application startup)
        /// </summary>
        /// <param name="isDebugMode">Whether to enable debug mode</param>
        public static void SetDebugMode(bool isDebugMode)
        {
            IsDebugMode = isDebugMode;
            DebugMessage($"Debug mode set to: {isDebugMode}", DebugClass.Info);
        }

        /// <summary>
        /// Executes a task with timeout
        /// </summary>
        /// <typeparam name="T">The task return type</typeparam>
        /// <param name="task">The task to execute</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>The task result or default if timed out</returns>
        public static async Task<T> ExecuteWithTimeout<T>(Task<T> task, int timeoutMs)
        {
            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                DebugMessage($"Task timed out after {timeoutMs} ms", DebugClass.Warning);
                return default;
            }
            
            return await task;
        }

        /// <summary>
        /// Safe try-catch wrapper for actions
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool SafeExecute(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                DebugMessage($"SafeExecute error: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }
        
        /// <summary>
        /// Generate a random string with specified length
        /// </summary>
        /// <param name="length">The length of the string</param>
        /// <returns>A random string</returns>
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}