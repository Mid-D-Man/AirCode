using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;

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
//we should also add checks for bad words for valid string to prevent any un professional words cause ya go never sabi the kind bullshit
//pikins go use as username
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
                    Console.WriteLine($"{prefix} LOG: {message} ");
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

        
        #region  Debug Utilities/StructMember Values

        public static string GetStructOrClassMemberValues<T>(T structOrClassInstance) where T : notnull
    {
        if (structOrClassInstance == null)
        {
            return "null";
        }

        StringBuilder result = new StringBuilder();
        Type type = structOrClassInstance.GetType();

        // Start the recursive process with depth 0
        GetStructOrClassMemberValuesRecursive(structOrClassInstance, result, 0);

        return result.ToString();
    }

    private static void GetStructOrClassMemberValuesRecursive(object structOrClassInstance, StringBuilder result, int depth)
    {
        if (structOrClassInstance == null)
        {
            result.AppendLine("null");
            return;
        }

        Type type = structOrClassInstance.GetType();
        string indentation = GetIndentation(depth);
        string arrowIndentation = GetArrowIndentation(depth);

        BindingFlags bindingFlags = type.IsClass
            ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
            : BindingFlags.Public | BindingFlags.Instance;

        // Get all fields
        FieldInfo[] fields = type.GetFields(bindingFlags);
        foreach (var field in fields)
        {
            AppendMemberValue(result, field.Name, field.GetValue(structOrClassInstance), depth);
        }

        // Get all properties
        PropertyInfo[] properties = type.GetProperties(bindingFlags);
        foreach (var property in properties)
        {
            if (property.CanRead)
            {
                try
                {
                    AppendMemberValue(result, property.Name, property.GetValue(structOrClassInstance), depth);
                }
                catch (Exception ex)
                {
                    result.AppendLine($"{indentation}{arrowIndentation} {property.Name} :: [Error accessing property: {ex.Message}]");
                }
            }
        }
    }

    private static void AppendMemberValue(StringBuilder result, string name, object value, int depth)
    {
        string indentation = GetIndentation(depth);
        string arrowIndentation = GetArrowIndentation(depth);

        if (value == null)
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: null");
        }
        else if (value is string || value.GetType().IsPrimitive)
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: {value}");
        }
        else if (value is IEnumerable enumerable && !(value is string))
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: [");
            int itemIndex = 0;
            foreach (var item in enumerable)
            {
                result.AppendLine($"{indentation}    {arrowIndentation} [{itemIndex}]:");
                if (item == null)
                {
                    result.AppendLine($"{indentation}    {arrowIndentation} null");
                }
                else if (item is string || item.GetType().IsPrimitive)
                {
                    result.AppendLine($"{indentation}    {arrowIndentation} {item}");
                }
                else
                {
                    GetStructOrClassMemberValuesRecursive(item, result, depth + 2);
                }
                itemIndex++;
            }
            result.AppendLine($"{indentation}{arrowIndentation} ]");
        }
        else
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: {{");
            GetStructOrClassMemberValuesRecursive(value, result, depth + 1);
            result.AppendLine($"{indentation}{arrowIndentation} }}");
        }
    }

    private static string GetIndentation(int depth)
    {
        return new string(' ', depth * 4);
    }

    private static string GetArrowIndentation(int depth)
    {
        if (depth == 0) return "";
        if (depth == 1) return "->";
        if (depth == 2) return "-->";
        if (depth == 3) return "--->";

        // For deeper nesting, use the number of levels
        return new string('-', depth) + ">";
    }

    // Convert any class or struct to a JSON string with pretty formatting
    public static string ToJson<T>(T obj, bool prettyPrint = true) where T : notnull
    {
        try
        {
            return JsonConvert.SerializeObject(obj, prettyPrint ? Formatting.Indented : Formatting.None);
        }
        catch (Exception ex)
        {
            // Replace Unity-specific debug method with standard logging
            DebugMessage($"Failed to serialize object to JSON: {ex.Message}",DebugClass.Exception);
            return "{}";
        }
    }

    // Convert any class or struct to XML string
    public static string ToXml<T>(T obj) where T : notnull
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }
        catch (Exception ex)
        {
            // Replace Unity-specific debug method with standard logging
            DebugMessage($"Failed to serialize object to XML: {ex.Message}",DebugClass.Exception);
            return "<error>Failed to serialize</error>";
        }
    }

        #endregion
    }
}