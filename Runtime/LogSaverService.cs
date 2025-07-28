using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Security;
using System.Text.RegularExpressions;
using static UnityEngine.EventSystems.EventTrigger;
using System.Runtime.CompilerServices;

public class LogSaverService : MonoBehaviour
{
    [SerializeField] private string fileName = "PlayerLog";
    [SerializeField][Range(1, 10)] private int fileSaveLogInterval = 5;
    [SerializeField][Range(1, 2500)] private int delay = 500;

    private string logFilePath;
    private StringBuilder htmlContent;
    private int logCounter = 0;
    private int intervalCount = 0;  

    private Queue<LogEntry> logQueue = new Queue<LogEntry>();
    private List<LogEntry> logContainer = new List<LogEntry>();
    private CancellationTokenSource cancellationTokenSource;
    private Task logWritingTask;

    private class LogEntry
    {
        public string Message;
        public string StackTrace;
        public LogType Type;
        public int Counter;
    }

    #region Unity
    void Awake()
    {
        Begin();
    }

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        Application.logMessageReceivedThreaded += HandleLogInterval;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceivedThreaded -= HandleLogInterval;
        End();
    }

    void Update()
    {
        if (intervalCount >= fileSaveLogInterval)
        {
            Save();
            intervalCount = 0;
        }
    }

    void OnApplicationQuit()
    {
        End();
    }
    #endregion

    #region Events
    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logCounter++;

        if (type == LogType.Log && string.IsNullOrEmpty(stackTrace))
        {
            string environmentStackTrace = Environment.StackTrace;

            string[] lines = environmentStackTrace.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder cleanStackTrace = new StringBuilder();
            bool foundOrigin = false;
            foreach (string line in lines)
            {
                if (foundOrigin || (!line.Contains("LogSaverService") && !line.Contains("UnityEngine.Application") && !line.Contains("UnityEngine.Debug")))
                {
                    cleanStackTrace.AppendLine(line.Trim());
                    foundOrigin = true;
                }
            }
            stackTrace = cleanStackTrace.ToString().Trim();
        }

        string processedLogString = ConvertUnityRichTextToHtml(logString);
        LogEntry entry = new LogEntry
        {
            Message = processedLogString,
            StackTrace = stackTrace,
            Type = type,
            Counter = logCounter
        };

        if (!logContainer.Contains(entry))
        {
            logContainer.Add(entry);
        }

        lock (logQueue)
        {
            logQueue.Enqueue(entry);
            if (!logContainer.Contains(entry)) 
            {
                logContainer.Add(entry);
            }
            Monitor.Pulse(logQueue);
        }
    }

    void HandleLogInterval(string logString, string stackTrace, LogType type)
    {
        intervalCount++;
        if (intervalCount >= fileSaveLogInterval)
        {
            Save();
            intervalCount = 0;
        }
    }
    #endregion

    private string ConvertUnityRichTextToHtml(string unityRichText)
    {
        if (string.IsNullOrEmpty(unityRichText))
        {
            return unityRichText;
        }

        unityRichText = Regex.Replace(unityRichText, @"<color=(#[0-9a-fA-F]{6}|#[0-9a-fA-F]{8}|aqua|black|blue|brown|cyan|darkblue|fuchsia|green|grey|lightblue|lime|magenta|maroon|navy|olive|purple|red|silver|teal|white|yellow)>(.*?)</color>",
                                     "<span style=\"color:$1;\">$2</span>", RegexOptions.IgnoreCase);

        unityRichText = Regex.Replace(unityRichText, @"<b>(.*?)</b>", "<strong>$1</strong>", RegexOptions.IgnoreCase);

        unityRichText = Regex.Replace(unityRichText, @"<i>(.*?)</i>", "<em>$1</em>", RegexOptions.IgnoreCase);

        unityRichText = Regex.Replace(unityRichText, @"<size=(\d+)>(.*?)</size>", "<span style=\"font-size:$1px;\">$2</span>", RegexOptions.IgnoreCase);

        return unityRichText;
    }

    #region FileAsync
    private void Begin()
    {
        logFilePath = Path.Combine(Application.persistentDataPath, fileName + ".html");
        Debug.Log($"Guardando logs en: {logFilePath}");

        BeginSync();

        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        cancellationTokenSource = new CancellationTokenSource();
        logWritingTask = Task.Run(() => WriteLogs(cancellationTokenSource.Token), cancellationTokenSource.Token);
    }

    private void WriteLogs(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LogEntry entry = null;
            lock (logQueue)
            {
                while (logQueue.Count == 0 && !cancellationToken.IsCancellationRequested)
                {
                    Monitor.Wait(logQueue);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (logQueue.Count > 0)
                {
                    entry = logQueue.Dequeue();
                }
            }

            if (entry != null)
            {
                string logClass = "";
                string logTypeString = "";

                switch (entry.Type)
                {
                    case LogType.Log:
                        logClass = "log-type-Log";
                        logTypeString = "Log";
                        break;
                    case LogType.Warning:
                        logClass = "log-type-Warning";
                        logTypeString = "Warning";
                        break;
                    case LogType.Error:
                        logClass = "log-type-Error";
                        logTypeString = "Error";
                        break;
                    case LogType.Exception:
                        logClass = "log-type-Exception";
                        logTypeString = "Error";
                        break;
                    case LogType.Assert:
                        logClass = "log-type-Error";
                        logTypeString = "Error";
                        break;
                }

                string escapedStackTrace = SecurityElement.Escape(entry.StackTrace ?? string.Empty);

                StringBuilder currentLogHtml = new StringBuilder();

                string originalLogStringEscaped = SecurityElement.Escape(Regex.Replace(entry.Message, @"<[^>]+>", string.Empty));

                currentLogHtml.Append($"        <div class=\"log-entry {logClass}\" data-log-text=\"{originalLogStringEscaped}\" data-log-type=\"{logTypeString}\">\n");

                string logHeaderId = $"logHeader_{entry.Counter}";
                string stackTraceContainerId = $"stackTraceContainer_{entry.Counter}";

                currentLogHtml.Append($"            <div class=\"log-message\" id=\"{logHeaderId}\" onclick=\"toggleStackTrace('{stackTraceContainerId}', '{logHeaderId}')\">\n");
                currentLogHtml.Append($"                <span class=\"toggle-icon\">&#9654;</span>\n");
                currentLogHtml.Append($"                {entry.Message}\n");
                currentLogHtml.Append("            </div>\n");

                currentLogHtml.Append($"            <div id=\"{stackTraceContainerId}\" class=\"stack-trace-container\">\n");
                if (!string.IsNullOrEmpty(escapedStackTrace.Trim()))
                {
                    currentLogHtml.Append($"                <pre class=\"stack-trace-content\">{escapedStackTrace}</pre>\n");
                }
                else
                {
                    currentLogHtml.Append($"                <pre class=\"stack-trace-content\">Stack trace generated but whitout detailed information </pre>\n");
                }
                currentLogHtml.Append("            </div>\n");
                currentLogHtml.Append("        </div>\n");

                htmlContent.Append(currentLogHtml.ToString());
            }
        }
    }

    private void End()
    {
        if (cancellationTokenSource == null) return;

        cancellationTokenSource.Cancel();
        lock (logQueue)
        {
            Monitor.Pulse(logQueue);
        }

        if (logWritingTask != null && !logWritingTask.IsCompleted)
        {
            logWritingTask.Wait();
        }

        htmlContent.Append(@"</div>
        </body>
        </html>");

        try
        {
            using (FileStream fs = new FileStream(logFilePath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    writer.Write(htmlContent.ToString());
                }
            }
            Debug.Log($"Logs finales guardados en: {logFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al guardar el archivo de logs final: {ex.Message}");
        }

        cancellationTokenSource.Dispose();
        cancellationTokenSource = null;
    }
    #endregion

    #region FileSync
    private void BeginSync()
    {
        logFilePath = Path.Combine(Application.persistentDataPath, fileName + ".html");
        Debug.Log($"Guardando logs en: {logFilePath}");

        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        htmlContent = new StringBuilder();
        
        htmlContent.Append(@"
<!DOCTYPE html>
<html>
<head>"
+
    $"<title>[{Application.productName}] Logs</title>"
+
    @"<style>
        body { font-family: Arial, sans-serif; background-color: #f0f0f0; padding: 20px; }
        .log-entry { background-color: #fff; border: 1px solid #ddd; margin-bottom: 10px; padding: 10px; border-radius: 5px; position: relative; }
        .log-message { font-weight: bold; cursor: pointer; display: flex; align-items: center; padding: 5px; }
        .log-type-Log { color: #333; }
        .log-type-Warning { color: #f39c12; }
        .log-type-Error, .log-type-Exception, .log-type-Assert { color: #e74c3c; }
        .stack-trace-container {
            display: none;
            margin-top: 5px;
            border-top: 1px solid #eee;
            padding-top: 5px;
        }
        .stack-trace-content { white-space: pre-wrap; font-family: monospace; font-size: 0.9em; background-color: #f9f9f9; padding: 10px; border-radius: 3px; overflow-x: auto;}
        .toggle-icon { margin-right: 8px; font-size: 1.2em; transition: transform 0.2s; }
        .toggle-icon.expanded { transform: rotate(90deg); }

        /* Estilos para el buscador y botones de filtro */
        .search-container { 
            margin-bottom: 20px; 
            padding: 10px; 
            background-color: #e0e0e0; 
            border-radius: 5px; 
            display: flex; 
            align-items: center; 
            flex-wrap: wrap; 
        }
        .search-container label { margin-right: 10px; font-weight: bold; }
        .search-container input { 
            padding: 8px; 
            border: 1px solid #ccc; 
            border-radius: 4px; 
            flex-grow: 1; 
            max-width: 400px; 
            margin-right: auto; 
        } 
        .filter-buttons { 
            display: flex; 
            gap: 10px; 
            flex-wrap: wrap; 
            margin-top: 5px; 
            margin-left: 15px; 
        }
        @media (max-width: 768px) {
            .filter-buttons {
                width: 100%; 
                margin-left: 0; 
                justify-content: center; 
            }
            .search-container input {
                margin-right: 0; 
            }
        }
        
        .filter-buttons button { padding: 8px 15px; border: 1px solid #ccc; border-radius: 4px; background-color: #f0f0f0; cursor: pointer; transition: background-color 0.2s, border-color 0.2s; }
        .filter-buttons button.active { background-color: #007bff; color: white; border-color: #007bff; }
        .filter-buttons button:hover:not(.active) { background-color: #e0e0e0; }
        
        /* Estilo para el contador de colapso */
        .collapse-count {
            position: absolute;
            right: 10px;
            top: 10px;
            background-color: #555;
            color: white;
            padding: 2px 8px;
            border-radius: 12px;
            font-size: 0.8em;
            font-weight: bold;
            min-width: 20px;
            text-align: center;
        }
    </style>
</head>
<body>"
+
$"<h1>{Application.productName} Logs:</h1>"
+
    @"
    <div class=""search-container"">
        <label for=""logSearch"">Search Log:</label>
        <input type=""text"" id=""logSearch"" onkeyup=""applyFilters()"" placeholder=""Write to apply filter logs"">
        
        <div class=""filter-buttons"">
            <button id=""filterAll"" onclick=""setLogFilter('All', this)"" class=""active"">All</button>
            <button id=""filterLog"" onclick=""setLogFilter('Log', this)"">Info</button>
            <button id=""filterWarning"" onclick=""setLogFilter('Warning', this)"">Warning</button>
            <button id=""filterError"" onclick=""setLogFilter('Error', this)"">Error</button>
        </div>
    </div>

    <div id=""logContainer"">
");

        htmlContent.Append(@"
            <script>
                var currentLogTypeFilter = 'All';

                function toggleStackTrace(stackTraceContainerId, logHeaderId) {
                    var stackTraceDiv = document.getElementById(stackTraceContainerId);
                    var logHeaderDiv = document.getElementById(logHeaderId);

                    if (stackTraceDiv) {
                        if (stackTraceDiv.style.display === 'none' || stackTraceDiv.style.display === '') {
                            stackTraceDiv.style.display = 'block';
                            if (logHeaderDiv) {
                                var icon = logHeaderDiv.querySelector('.toggle-icon');
                                if (icon) icon.classList.add('expanded');
                            }
                        } else {
                            stackTraceDiv.style.display = 'none';
                            if (logHeaderDiv) {
                                var icon = logHeaderDiv.querySelector('.toggle-icon');
                                if (icon) icon.classList.remove('expanded');
                            }
                        }
                    }
                }

                function setLogFilter(type, clickedButton) {
                    currentLogTypeFilter = type;
            
                    var buttons = document.querySelectorAll('.filter-buttons button');
                    buttons.forEach(function(button) {
                        button.classList.remove('active');
                    });
                    clickedButton.classList.add('active');

                    applyFilters(); 
                }

                function applyFilters() {
                    var textInput = document.getElementById('logSearch');
                    var textFilter = textInput.value.toLowerCase();
                    var logContainer = document.getElementById('logContainer');
                    var logEntries = logContainer.getElementsByClassName('log-entry');
            
                    // Primero, resetear el estado de visibilidad y contadores para todos los logs
                    for (var i = 0; i < logEntries.length; i++) {
                        logEntries[i].style.display = ''; // Hacer todos visibles
                        var countSpan = logEntries[i].querySelector('.collapse-count');
                        if(countSpan) countSpan.remove(); // Eliminar cualquier contador existente
                    }

                    // Aplicar filtros de texto y tipo
                    for (var i = 0; i < logEntries.length; i++) {
                        var logEntry = logEntries[i];
                        var logMessageText = logEntry.dataset.logText || '';
                        var logType = logEntry.dataset.logType;

                        var matchesText = (logMessageText.toLowerCase().indexOf(textFilter) > -1);
                        var matchesType = (currentLogTypeFilter === 'All' || logType === currentLogTypeFilter);

                        if (matchesText && matchesType) {
                            logEntry.style.display = ''; 
                        } else {
                            logEntry.style.display = 'none';
                        }
                    }

                    // Después de aplicar los filtros de texto y tipo, aplicar el colapso a los logs VISIBLES
                    // Si el texto del buscador está vacío, el colapso se aplicará a todos los logs del tipo filtrado.
                    // Si hay texto en el buscador, el colapso se aplicará a los logs que coincidan con el texto Y el tipo.
                    collapseVisibleDuplicateLogs(); 
                }

                function collapseVisibleDuplicateLogs() {
                    var logContainer = document.getElementById('logContainer');
                    var logEntries = logContainer.getElementsByClassName('log-entry');
                    var seenLogs = {}; 

                    // Iterar sobre los logs para aplicar el colapso
                    for (var i = 0; i < logEntries.length; i++) {
                        var currentLogEntry = logEntries[i];

                        // Solo procesar logs que son actualmente visibles por applyFilters()
                        if (currentLogEntry.style.display === 'none') {
                            continue; // Saltar logs que ya están ocultos por los filtros de texto/tipo
                        }

                        var logText = currentLogEntry.dataset.logText; 
                
                        if (logText in seenLogs) {
                            currentLogEntry.style.display = 'none'; // Ocultar duplicado
                            var originalLogIndex = seenLogs[logText].index;
                            var originalLogElement = logEntries[originalLogIndex];
                    
                            // Asegurarse de que el log original todavía es visible antes de añadir el contador
                            if (originalLogElement.style.display !== 'none') {
                                var originalCountSpan = originalLogElement.querySelector('.collapse-count');
                        
                                if (originalCountSpan) {
                                    var currentCount = parseInt(originalCountSpan.textContent);
                                    originalCountSpan.textContent = (currentCount + 1).toString();
                                } else {
                                    originalCountSpan = document.createElement('span');
                                    originalCountSpan.className = 'collapse-count';
                                    originalCountSpan.textContent = '2'; 
                                    originalLogElement.appendChild(originalCountSpan);
                                }
                            }
                        } else {
                            seenLogs[logText] = { 
                                index: i,
                                element: currentLogEntry
                            };
                        }
                    }
                }
        
                // Llamar a applyFilters al cargar la página para establecer el estado inicial (colapsado, todos los tipos)
                window.onload = applyFilters; 
            </script>
        ");
    }

    private void WriteLogsSync()
    {
        foreach(var entry in logContainer)
        {
            if (entry != null)
            {
                string logClass = "";
                string logTypeString = "";

                switch (entry.Type)
                {
                    case LogType.Log:
                        logClass = "log-type-Log";
                        logTypeString = "Log";
                        break;
                    case LogType.Warning:
                        logClass = "log-type-Warning";
                        logTypeString = "Warning";
                        break;
                    case LogType.Error:
                        logClass = "log-type-Error";
                        logTypeString = "Error";
                        break;
                    case LogType.Exception:
                        logClass = "log-type-Exception";
                        logTypeString = "Error";
                        break;
                    case LogType.Assert:
                        logClass = "log-type-Error";
                        logTypeString = "Error";
                        break;
                }

                string escapedStackTrace = SecurityElement.Escape(entry.StackTrace ?? string.Empty);

                StringBuilder currentLogHtml = new StringBuilder();

                string originalLogStringEscaped = SecurityElement.Escape(Regex.Replace(entry.Message, @"<[^>]+>", string.Empty));

                currentLogHtml.Append($"        <div class=\"log-entry {logClass}\" data-log-text=\"{originalLogStringEscaped}\" data-log-type=\"{logTypeString}\">\n");

                string logHeaderId = $"logHeader_{entry.Counter}";
                string stackTraceContainerId = $"stackTraceContainer_{entry.Counter}";

                currentLogHtml.Append($"            <div class=\"log-message\" id=\"{logHeaderId}\" onclick=\"toggleStackTrace('{stackTraceContainerId}', '{logHeaderId}')\">\n");
                currentLogHtml.Append($"                <span class=\"toggle-icon\">&#9654;</span>\n");
                currentLogHtml.Append($"                {entry.Message}\n");
                currentLogHtml.Append("            </div>\n");

                currentLogHtml.Append($"            <div id=\"{stackTraceContainerId}\" class=\"stack-trace-container\">\n");
                if (!string.IsNullOrEmpty(escapedStackTrace.Trim()))
                {
                    currentLogHtml.Append($"                <pre class=\"stack-trace-content\">{escapedStackTrace}</pre>\n");
                }
                else
                {
                    currentLogHtml.Append($"                <pre class=\"stack-trace-content\">Stack trace generated but whitout detailed information </pre>\n");
                }
                currentLogHtml.Append("            </div>\n");
                currentLogHtml.Append("        </div>\n");

                htmlContent.Append(currentLogHtml.ToString());

            }
        }
    }

    private void EndSync()
    {
        logContainer.Clear();

        htmlContent.Append(@"</div>
        </body>
        </html>");

        try
        {
            using (FileStream fs = new FileStream(logFilePath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    writer.Write(htmlContent.ToString());
                }
            }
            Debug.Log($"Logs finales guardados en: {logFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al guardar el archivo de logs final: {ex.Message}");
        }
    }
    #endregion

    public async void Save()
    {
        Debug.Log("Save To Log To File");

        BeginSync();
        await Task.Delay(100);
        WriteLogsSync();
        await Task.Delay(100);
        EndSync();
    }
}