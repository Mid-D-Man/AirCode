// mid_HelperFunctions.js - Production-ready JavaScript logging and utility helper
// Similar to C# MID_HelperFunctions but for client-side JavaScript

/**
 * Debug message type enumeration
 */
const DebugClass = Object.freeze({
    LOG: 'log',
    WARNING: 'warning',
    ERROR: 'error',
    EXCEPTION: 'exception',
    INFO: 'info'
});

/**
 * Environment detection
 */
const Environment = Object.freeze({
    DEVELOPMENT: 'development',
    PRODUCTION: 'production',
    STAGING: 'staging'
});

/**
 * JavaScript Helper Functions for AirCode with improved production practices
 */
class MID_HelperFunctions {
    #isDebugMode = false;
    #environment = Environment.DEVELOPMENT;
    #logBuffer = [];
    #maxLogBuffer = 1000;
    #remoteLoggingEndpoint = null;
    #sessionId = null;

    // Cache for compiled regex patterns
    #regexCache = new Map();

    // Validation constants
    #invalidStringValues = ['NULL', 'UNDEFINED', 'NONE', 'N/A', 'EMPTY'];

    constructor() {
        this.#detectEnvironment();
        this.#generateSessionId();
        this.#setupGlobalErrorHandling();

        // Auto-initialize based on environment
        this.#isDebugMode = this.#environment !== Environment.PRODUCTION;

        this.debugMessage('MID_HelperFunctions initialized', DebugClass.INFO);
    }

    // #region Initialization and Configuration

    /**
     * Set the debug mode manually
     * @param {boolean} isDebugMode - Whether to enable debug mode
     */
    setDebugMode(isDebugMode) {
        const wasDebugMode = this.#isDebugMode;
        this.#isDebugMode = isDebugMode;

        if (wasDebugMode !== isDebugMode) {
            this.debugMessage(`Debug mode changed to: ${isDebugMode}`, DebugClass.INFO);
        }
    }

    /**
     * Configure remote logging endpoint for production error tracking
     * @param {string} endpoint - The remote logging endpoint URL
     */
    setRemoteLoggingEndpoint(endpoint) {
        this.#remoteLoggingEndpoint = endpoint;
        this.debugMessage(`Remote logging endpoint set: ${endpoint}`, DebugClass.INFO);
    }

    /**
     * Get current environment
     * @returns {string} Current environment
     */
    getEnvironment() {
        return this.#environment;
    }

    /**
     * Check if currently in debug mode
     * @returns {boolean} True if debug mode is enabled
     */
    isDebugMode() {
        return this.#isDebugMode;
    }

    // #endregion

    // #region Environment Detection

    /**
     * Detect the current environment based on various factors
     * @private
     */
    #detectEnvironment() {
        // Check for explicit environment variable
        if (typeof process !== 'undefined' && process.env) {
            const nodeEnv = process.env.NODE_ENV?.toLowerCase();
            if (nodeEnv === 'production') {
                this.#environment = Environment.PRODUCTION;
                return;
            }
            if (nodeEnv === 'staging') {
                this.#environment = Environment.STAGING;
                return;
            }
        }

        // Browser-based detection
        if (typeof window !== 'undefined') {
            const hostname = window.location?.hostname?.toLowerCase();

            // Production indicators
            if (hostname && (
                !hostname.includes('localhost') &&
                !hostname.includes('127.0.0.1') &&
                !hostname.includes('dev') &&
                !hostname.includes('test') &&
                !hostname.includes('.local')
            )) {
                this.#environment = Environment.PRODUCTION;
                return;
            }

            // Staging indicators
            if (hostname && (
                hostname.includes('staging') ||
                hostname.includes('stage') ||
                hostname.includes('preview')
            )) {
                this.#environment = Environment.STAGING;
                return;
            }
        }

        // Default to development
        this.#environment = Environment.DEVELOPMENT;
    }

    /**
     * Generate unique session ID for tracking
     * @private
     */
    #generateSessionId() {
        this.#sessionId = 'sess_' + Date.now().toString(36) + '_' + Math.random().toString(36).substring(2);
    }

    // #endregion

    // #region String Validation

    /**
     * Validates if a string is not null, empty, or invalid values
     * @param {string|null|undefined} input - The string to validate
     * @returns {boolean} True if the string is valid, false otherwise
     */
    isValidString(input) {
        if (input == null || input === '') return false;

        if (typeof input !== 'string') return false;

        const trimmedInput = input.trim();
        if (trimmedInput === '') return false;

        const upperInput = trimmedInput.toUpperCase();
        return !this.#invalidStringValues.includes(upperInput);
    }

    /**
     * Validates if a string matches a regex pattern with caching
     * @param {string} input - The string to validate
     * @param {string} pattern - The regex pattern
     * @returns {boolean} True if matches, false otherwise
     */
    isValidPattern(input, pattern) {
        if (!this.isValidString(input) || !this.isValidString(pattern)) {
            return false;
        }

        try {
            let regex = this.#regexCache.get(pattern);
            if (!regex) {
                regex = new RegExp(pattern);
                this.#regexCache.set(pattern, regex);
            }
            return regex.test(input);
        } catch (error) {
            this.debugMessage(`Regex validation error for pattern '${pattern}': ${error.message}`, DebugClass.EXCEPTION);
            return false;
        }
    }

    // #endregion

    // #region Debug and Logging

    /**
     * Main debug message function with multiple output channels
     * @param {string} message - The message to log
     * @param {string} debugClass - The debug class/level
     * @param {Object} metadata - Additional metadata to log
     */
    debugMessage(message, debugClass = DebugClass.LOG, metadata = {}) {
        if (!this.#isDebugMode && debugClass !== DebugClass.ERROR && debugClass !== DebugClass.EXCEPTION) {
            // In production, only log errors and exceptions
            if (this.#environment === Environment.PRODUCTION) {
                return;
            }
        }

        const timestamp = new Date().toISOString();
        const logEntry = {
            timestamp,
            level: debugClass,
            message,
            sessionId: this.#sessionId,
            environment: this.#environment,
            url: typeof window !== 'undefined' ? window.location?.href : undefined,
            userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : undefined,
            metadata: Object.keys(metadata).length > 0 ? metadata : undefined
        };

        // Add to buffer for potential remote logging
        this.#addToLogBuffer(logEntry);

        // Console output (only in debug mode or for critical messages)
        if (this.#isDebugMode || debugClass === DebugClass.ERROR || debugClass === DebugClass.EXCEPTION) {
            this.#logToConsole(logEntry);
        }

        // Remote logging for errors and exceptions (even in production)
        if ((debugClass === DebugClass.ERROR || debugClass === DebugClass.EXCEPTION) && this.#remoteLoggingEndpoint) {
            this.#logRemotely(logEntry);
        }
    }

    /**
     * Async version of debugMessage
     * @param {string} message - The message to log
     * @param {string} debugClass - The debug class/level
     * @param {Object} metadata - Additional metadata
     * @returns {Promise<void>}
     */
    async debugMessageAsync(message, debugClass = DebugClass.LOG, metadata = {}) {
        return new Promise((resolve) => {
            this.debugMessage(message, debugClass, metadata);
            resolve();
        });
    }

    /**
     * Log to browser console with appropriate method
     * @param {Object} logEntry - The structured log entry
     * @private
     */
    #logToConsole(logEntry) {
        const formattedMessage = `[${logEntry.timestamp}] [${logEntry.level.toUpperCase()}] ${logEntry.message}`;

        switch (logEntry.level) {
            case DebugClass.WARNING:
                console.warn(formattedMessage, logEntry.metadata || '');
                break;
            case DebugClass.ERROR:
            case DebugClass.EXCEPTION:
                console.error(formattedMessage, logEntry.metadata || '');
                break;
            case DebugClass.INFO:
                console.info(formattedMessage, logEntry.metadata || '');
                break;
            default:
                console.log(formattedMessage, logEntry.metadata || '');
                break;
        }
    }

    /**
     * Add log entry to buffer
     * @param {Object} logEntry - The log entry to buffer
     * @private
     */
    #addToLogBuffer(logEntry) {
        this.#logBuffer.push(logEntry);

        // Maintain buffer size
        if (this.#logBuffer.length > this.#maxLogBuffer) {
            this.#logBuffer = this.#logBuffer.slice(-this.#maxLogBuffer);
        }
    }

    /**
     * Send log entry to remote endpoint
     * @param {Object} logEntry - The log entry to send
     * @private
     */
    async #logRemotely(logEntry) {
        if (!this.#remoteLoggingEndpoint) return;

        try {
            await fetch(this.#remoteLoggingEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(logEntry)
            });
        } catch (error) {
            // Fallback to console if remote logging fails
            console.error('Remote logging failed:', error);
        }
    }

    /**
     * Get recent log entries from buffer
     * @param {number} count - Number of recent entries to return
     * @returns {Array} Recent log entries
     */
    getRecentLogs(count = 50) {
        return this.#logBuffer.slice(-count);
    }

    /**
     * Clear the log buffer
     */
    clearLogBuffer() {
        this.#logBuffer = [];
        this.debugMessage('Log buffer cleared', DebugClass.INFO);
    }

    // #endregion

    // #region Global Error Handling

    /**
     * Set up global error handling
     * @private
     */
    #setupGlobalErrorHandling() {
        if (typeof window === 'undefined') return;

        // Handle uncaught errors
        window.addEventListener('error', (event) => {
            this.debugMessage(`Global error: ${event.error?.message || event.message}`, DebugClass.EXCEPTION, {
                filename: event.filename,
                lineno: event.lineno,
                colno: event.colno,
                stack: event.error?.stack
            });
        });

        // Handle unhandled promise rejections
        window.addEventListener('unhandledrejection', (event) => {
            this.debugMessage(`Unhandled promise rejection: ${event.reason}`, DebugClass.EXCEPTION, {
                promise: event.promise,
                reason: event.reason
            });
        });
    }

    // #endregion

    // #region Utility Methods

    /**
     * Execute function with timeout
     * @param {Function} func - Function to execute
     * @param {number} timeoutMs - Timeout in milliseconds
     * @returns {Promise} Promise that resolves with function result or rejects on timeout
     */
    async executeWithTimeout(func, timeoutMs) {
        return Promise.race([
            func(),
            new Promise((_, reject) => {
                setTimeout(() => reject(new Error(`Function timed out after ${timeoutMs}ms`)), timeoutMs);
            })
        ]);
    }

    /**
     * Safe execute wrapper with error handling
     * @param {Function} func - Function to execute
     * @param {string} actionName - Name of the action for logging
     * @returns {Promise<any>} Result or null if error occurred
     */
    async safeExecute(func, actionName = 'unknown') {
        try {
            return await func();
        } catch (error) {
            this.debugMessage(`SafeExecute error in '${actionName}': ${error.message}`, DebugClass.EXCEPTION, {
                stack: error.stack,
                actionName
            });
            return null;
        }
    }

    /**
     * Generate cryptographically secure random string
     * @param {number} length - Length of the string
     * @param {boolean} useSpecialChars - Include special characters
     * @returns {string} Random string
     */
    generateRandomString(length, useSpecialChars = false) {
        if (length <= 0) return '';

        const basicChars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
        const specialChars = '!@#$%^&*()_+-=[]{}|;:,.<>?';
        const chars = useSpecialChars ? basicChars + specialChars : basicChars;

        // Use crypto.getRandomValues if available, fallback to Math.random
        const values = new Uint8Array(length);
        if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
            crypto.getRandomValues(values);
            return Array.from(values, byte => chars[byte % chars.length]).join('');
        } else {
            return Array.from({ length }, () => chars[Math.floor(Math.random() * chars.length)]).join('');
        }
    }

    /**
     * Deep clone an object safely
     * @param {any} obj - Object to clone
     * @returns {any} Cloned object
     */
    deepClone(obj) {
        if (obj === null || typeof obj !== 'object') return obj;
        if (obj instanceof Date) return new Date(obj);
        if (obj instanceof Array) return obj.map(item => this.deepClone(item));
        if (typeof obj === 'object') {
            const clonedObj = {};
            for (const key in obj) {
                if (obj.hasOwnProperty(key)) {
                    clonedObj[key] = this.deepClone(obj[key]);
                }
            }
            return clonedObj;
        }
        return obj;
    }

    /**
     * Get detailed object information for debugging
     * @param {any} obj - Object to inspect
     * @param {number} maxDepth - Maximum depth to traverse
     * @returns {string} Formatted string with object details
     */
    inspectObject(obj, maxDepth = 3) {
        const seen = new WeakSet();

        const inspect = (value, depth = 0) => {
            if (depth > maxDepth) return '[Max Depth Reached]';

            if (value === null) return 'null';
            if (value === undefined) return 'undefined';

            const type = typeof value;
            if (type === 'string') return `"${value}"`;
            if (type === 'number' || type === 'boolean') return String(value);
            if (type === 'function') return `[Function: ${value.name || 'anonymous'}]`;

            if (type === 'object') {
                if (seen.has(value)) return '[Circular Reference]';
                seen.add(value);

                if (Array.isArray(value)) {
                    const items = value.slice(0, 10).map(item => inspect(item, depth + 1));
                    return `[${items.join(', ')}${value.length > 10 ? '...' : ''}]`;
                }

                const entries = Object.entries(value)
                    .slice(0, 10)
                    .map(([key, val]) => `${key}: ${inspect(val, depth + 1)}`);

                return `{${entries.join(', ')}${Object.keys(value).length > 10 ? '...' : ''}}`;
            }

            return String(value);
        };

        return inspect(obj);
    }

    /**
     * Performance measurement wrapper
     * @param {Function} func - Function to measure
     * @param {string} label - Label for the measurement
     * @returns {Promise<any>} Function result with performance logged
     */
    async measurePerformance(func, label = 'operation') {
        const startTime = performance.now();

        try {
            const result = await func();
            const endTime = performance.now();
            const duration = endTime - startTime;

            this.debugMessage(`Performance: ${label} completed in ${duration.toFixed(2)}ms`, DebugClass.INFO, {
                duration,
                label,
                startTime,
                endTime
            });

            return result;
        } catch (error) {
            const endTime = performance.now();
            const duration = endTime - startTime;

            this.debugMessage(`Performance: ${label} failed after ${duration.toFixed(2)}ms`, DebugClass.ERROR, {
                duration,
                label,
                error: error.message,
                startTime,
                endTime
            });

            throw error;
        }
    }

    // #endregion

    // #region JSON/Data Utilities

    /**
     * Safe JSON parse with error handling
     * @param {string} jsonString - JSON string to parse
     * @param {any} defaultValue - Default value if parsing fails
     * @returns {any} Parsed object or default value
     */
    safeJsonParse(jsonString, defaultValue = null) {
        if (!this.isValidString(jsonString)) {
            return defaultValue;
        }

        try {
            return JSON.parse(jsonString);
        } catch (error) {
            this.debugMessage(`JSON parse error: ${error.message}`, DebugClass.WARNING, {
                jsonString: jsonString.substring(0, 100) + (jsonString.length > 100 ? '...' : ''),
                error: error.message
            });
            return defaultValue;
        }
    }

    /**
     * Safe JSON stringify with error handling
     * @param {any} obj - Object to stringify
     * @param {boolean} pretty - Whether to format with indentation
     * @returns {string} JSON string or error placeholder
     */
    safeJsonStringify(obj, pretty = false) {
        try {
            return JSON.stringify(obj, null, pretty ? 2 : 0);
        } catch (error) {
            this.debugMessage(`JSON stringify error: ${error.message}`, DebugClass.WARNING, {
                objectType: typeof obj,
                error: error.message
            });
            return '{"error": "Failed to stringify object"}';
        }
    }

    // #endregion

    // #region Production Utilities

    /**
     * Get minimal system information for error reporting
     * @returns {Object} System information object
     */
    getSystemInfo() {
        const info = {
            timestamp: new Date().toISOString(),
            sessionId: this.#sessionId,
            environment: this.#environment,
            debugMode: this.#isDebugMode
        };

        if (typeof window !== 'undefined') {
            info.url = window.location?.href;
            info.userAgent = navigator?.userAgent;
            info.viewport = {
                width: window.innerWidth,
                height: window.innerHeight
            };
        }

        return info;
    }

    /**
     * Export logs for debugging (development only)
     * @returns {string} JSON string of recent logs
     */
    exportLogs() {
        if (this.#environment === Environment.PRODUCTION) {
            this.debugMessage('Log export blocked in production environment', DebugClass.WARNING);
            return '{"error": "Log export not available in production"}';
        }

        return this.safeJsonStringify({
            exportedAt: new Date().toISOString(),
            systemInfo: this.getSystemInfo(),
            logs: this.getRecentLogs()
        }, true);
    }

    // #endregion
}

// Create and export global instance
const midHelperFunctions = new MID_HelperFunctions();

// Export both the class and instance for different use cases
if (typeof module !== 'undefined' && module.exports) {
    // Node.js environment
    module.exports = { MID_HelperFunctions, midHelperFunctions, DebugClass, Environment };
} else if (typeof window !== 'undefined') {
    // Browser environment
    window.MID_HelperFunctions = MID_HelperFunctions;
    window.midHelperFunctions = midHelperFunctions;
    window.DebugClass = DebugClass;
    window.Environment = Environment;
}

// ES6 module exports (only in module context)
if (typeof exports !== 'undefined' && typeof module === 'undefined') {
    // ES6 module environment
    export { MID_HelperFunctions, midHelperFunctions, DebugClass, Environment };
    export default midHelperFunctions;
}