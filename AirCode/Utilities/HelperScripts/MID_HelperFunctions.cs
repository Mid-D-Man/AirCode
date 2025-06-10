using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Microsoft.JSInterop;

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
        private static IJSRuntime _jsRuntime;

        /// <summary>
        /// Initialize the helper functions with JSRuntime for browser console logging
        /// </summary>
        /// <param name="jsRuntime">IJSRuntime instance for browser interop</param>
        public static void Initialize(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

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
        /// Prints debug messages with color support for both console and browser
        /// </summary>
        /// <param name="message">The message to print</param>
        /// <param name="debugClass">The type of debug message</param>
        public static void DebugMessage(string message, DebugClass debugClass = DebugClass.Log)
        {
            if (!IsDebugMode)
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string prefix = $"[{timestamp}] [{debugClass}]";

            // Console logging with colors (server-side)
            ConsoleColor originalColor = Console.ForegroundColor;
            
            try
            {
                switch (debugClass)
                {
                    case DebugClass.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"{prefix} WARNING: {message}");
                        break;
                    case DebugClass.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{prefix} ERROR: {message}");
                        break;
                    case DebugClass.Exception:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"{prefix} EXCEPTION: {message}");
                        break;
                    case DebugClass.Info:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"{prefix} INFO: {message}");
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"{prefix} LOG: {message}");
                        break;
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }

            // Browser console logging with colors (client-side)
            if (_jsRuntime != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string color = GetBrowserConsoleColor(debugClass);
                        string consoleMethod = GetBrowserConsoleMethod(debugClass);
                        
                        await _jsRuntime.InvokeVoidAsync($"console.{consoleMethod}", 
                            $"%c{prefix} {GetDebugPrefix(debugClass)}: {message}",
                            $"color: {color}; font-weight: bold;");
                    }
                    catch
                    {
                        // Ignore JS interop errors during logging
                    }
                });
            }
        }

        /// <summary>
        /// Gets the appropriate browser console color for the debug class
        /// </summary>
        private static string GetBrowserConsoleColor(DebugClass debugClass)
        {
            return debugClass switch
            {
                DebugClass.Warning => "#FFA500",  // Orange
                DebugClass.Error => "#FF4444",    // Red
                DebugClass.Exception => "#8B0000", // Dark Red
                DebugClass.Info => "#00CED1",     // Dark Turquoise
                _ => "#FFFFFF"                    // White
            };
        }

        /// <summary>
        /// Gets the appropriate browser console method for the debug class
        /// </summary>
        private static string GetBrowserConsoleMethod(DebugClass debugClass)
        {
            return debugClass switch
            {
                DebugClass.Warning => "warn",
                DebugClass.Error => "error",
                DebugClass.Exception => "error",
                DebugClass.Info => "info",
                _ => "log"
            };
        }

        /// <summary>
        /// Gets the debug prefix for the message
        /// </summary>
        private static string GetDebugPrefix(DebugClass debugClass)
        {
            return debugClass switch
            {
                DebugClass.Warning => "⚠️ WARNING",
                DebugClass.Error => "❌ ERROR",
                DebugClass.Exception => "💥 EXCEPTION",
                DebugClass.Info => "ℹ️ INFO",
                _ => "📝 LOG"
            };
        }

        /// <summary>
        /// Logs a message with custom color to browser console
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">Hex color code (e.g., "#FF0000")</param>
        /// <param name="fontSize">Font size (e.g., "12px")</param>
        /// <param name="fontWeight">Font weight (e.g., "bold")</param>
        public static async Task LogWithColorAsync(string message, string color = "#FFFFFF", 
            string fontSize = "12px", string fontWeight = "normal")
        {
            if (_jsRuntime != null && IsDebugMode)
            {
                try
                {
                    await _jsRuntime.InvokeVoidAsync("console.log", 
                        $"%c{message}",
                        $"color: {color}; font-size: {fontSize}; font-weight: {fontWeight};");
                }
                catch
                {
                    // Fallback to standard console
                    Console.WriteLine(message);
                }
            }
        }

        /// <summary>
        /// Creates a styled banner message in the browser console
        /// </summary>
        /// <param name="message">The banner message</param>
        /// <param name="backgroundColor">Background color</param>
        /// <param name="textColor">Text color</param>
        public static async Task LogBannerAsync(string message, string backgroundColor = "#007ACC", 
            string textColor = "#FFFFFF")
        {
            if (_jsRuntime != null && IsDebugMode)
            {
                try
                {
                    await _jsRuntime.InvokeVoidAsync("console.log", 
                        $"%c {message} ",
                        $"background: {backgroundColor}; color: {textColor}; font-size: 16px; " +
                        $"font-weight: bold; padding: 4px 8px; border-radius: 4px;");
                }
                catch
                {
                    Console.WriteLine($"=== {message} ===");
                }
            }
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
            DebugMessage($"Failed to serialize object to XML: {ex.Message}",DebugClass.Exception);
            return "<error>Failed to serialize</error>";
        }
    }

        #endregion
    }
}