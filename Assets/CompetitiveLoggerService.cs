using UnityEngine;
using System.Reflection;
using System;
using System.Collections.Generic;

interface ILogRecord {}

class ExceptionRecord : ILogRecord {
    public Exception exception {
        get;
        private set;
    }
    public UnityEngine.Object context {
        get;
        private set;
    }

    public ExceptionRecord(Exception exception, UnityEngine.Object context) {
        this.exception = exception;
        this.context = context;
    }
}

class RegularRecord : ILogRecord {
    public LogType logType {
        get;
        private set;
    }
    public UnityEngine.Object context {
        get;
        private set;
    }
    public string format {
        get;
        private set;
    }
    public object[] args {
        get;
        private set;
    }

    public RegularRecord(LogType logType, UnityEngine.Object context, string format, params object[] args) {
        this.logType = logType;
        this.context = context;
        this.format = format;
        this.args = args;
    }
}


class CompetitiveLogHandler : ILogHandler {
    private ILogger oldLogger;
    private Queue<ILogRecord> cache;
    private bool isLogOutputEnabled;
    
    public CompetitiveLogHandler(ILogger oldLogger) {
        this.oldLogger = oldLogger;
        this.cache = new Queue<ILogRecord>();
        this.isLogOutputEnabled = true;
    }

    public void LogException(Exception exception, UnityEngine.Object context) {
        // If log output is disabled, add the record to the queue
        if (this.isLogOutputEnabled) {
            oldLogger.LogException(exception, context);
        } else {
            this.cache.Enqueue(new ExceptionRecord(exception, context));
        }
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) {
        // If log output is disabled, add the record to the queue
        if (this.isLogOutputEnabled) {
            oldLogger.LogFormat(logType, context, format, args);
        } else {
            this.cache.Enqueue(new RegularRecord(logType, context, format, args));
        }
    }

    public void dumpCache() {
        // If there are any records in the queue, dump it
        if (this.cache.Count > 0) {
            Debug.Log("[CompetitiveLogger] Dumping cache of " + this.cache.Count + " log records...");

            // Log each queued record
            while (this.cache.Count > 0) {
                ILogRecord theRecord = this.cache.Dequeue();

                // Handle record type accordingly
                if (theRecord is RegularRecord) {
                    RegularRecord record = (RegularRecord)theRecord;
                    this.LogFormat(record.logType, record.context, record.format, record.args);
                } else if (theRecord is ExceptionRecord) {
                    ExceptionRecord record = (ExceptionRecord)theRecord;
                    this.LogException(record.exception, record.context);
                }
            }

            Debug.Log("[CompetitiveLogger] Cache dump complete.");
        }
    }

    public void enableLogOutput() {
        this.isLogOutputEnabled = true;
        Debug.Log("[CompetitiveLogger] Log output enabled.");
        this.dumpCache();
    }

    public void disableLogOutput() {
        Debug.Log("[CompetitiveLogger] Log output disabled.");
        this.isLogOutputEnabled = false;
    }
}

public class CompetitiveLoggerService : MonoBehaviour {
    private static CompetitiveLogHandler handler;
    private static ILogger logger;
    private static ILogger oldLogger;
    private static bool hasLoggerBeenReplaced = false;

    private static void replaceLogger() {
        if (!hasLoggerBeenReplaced) {
            // Get the old logger
            var unityLoggerField = typeof(Debug).GetField("s_Logger", BindingFlags.Static | BindingFlags.NonPublic);
            oldLogger = (ILogger) unityLoggerField.GetValue(null);

            // Setup the new logger
            handler = new CompetitiveLogHandler(oldLogger);
            logger = new Logger(handler);
            unityLoggerField.SetValue(null, logger);
            hasLoggerBeenReplaced = true;
            Debug.Log("[CompetitiveLogger] Replaced Unity default logger.");
        }
    }

    private static void resetLogger() {
        if (hasLoggerBeenReplaced) {
            // Dump any remaining output
            handler.enableLogOutput();

            // Get the new logger
            var unityLoggerField = typeof(Debug).GetField("s_Logger", BindingFlags.Static | BindingFlags.NonPublic);

            // Setup the new logger
            unityLoggerField.SetValue(null, oldLogger);
            hasLoggerBeenReplaced = false;
            Debug.Log("[CompetitiveLogger] Reset Unity default logger.");
        }
    }
    
    KMBombInfo bombInfo;
    KMGameInfo gameInfo;

    void Awake() {
        Debug.Log("[CompetitiveLogger] Initializing...");

        // Register bomb info listeners as a just-in-case
        bombInfo = GetComponent<KMBombInfo>();
        bombInfo.OnBombExploded += OnBombExplodes;
        bombInfo.OnBombSolved += OnBombDefused;

        // Register game state listener
        gameInfo = GetComponent<KMGameInfo>();
        gameInfo.OnStateChange += OnStateChange;

        // Replace the default logger
        replaceLogger();

        Debug.Log("[CompetitiveLogger] Done initializing.");
    }

    void OnDestroy() {
        Debug.Log("[CompetitiveLogger] Tearing down...");

        // Reset the logger to the default
        resetLogger();

        Debug.Log("[CompetitiveLogger] Done tearing down.");
    }

    protected void OnBombExplodes() {
        handler.enableLogOutput();
    }

    protected void OnBombDefused() {
        handler.enableLogOutput();
    }

    protected void OnStateChange(KMGameInfo.State newState) {
        if (newState == KMGameInfo.State.Gameplay) {
            handler.disableLogOutput();
        } else {
            handler.enableLogOutput();
        }
    }
}