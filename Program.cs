using System.Diagnostics;

namespace XMouse;

static class Program
{
    private const string EventSourceName = "XMouse";
    private const string EventLogName = "Application";

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Ensure the event source exists
        if (!EventLog.SourceExists(EventSourceName))
        {
            EventLog.CreateEventSource(EventSourceName, EventLogName);
        }

        using (EventLog eventLog = new EventLog(EventLogName))
        {
            eventLog.Source = EventSourceName;

            try
            {
                eventLog.WriteEntry("XMouse application starting.", EventLogEntryType.Information, 100);

                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Application.Run(new MyApplicationContext());
            }
            catch (Exception ex)
            {
                string errorMessage = $"XMouse application error: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }
                eventLog.WriteEntry(errorMessage, EventLogEntryType.Error, 101);
            }
            finally
            {
                eventLog.WriteEntry("XMouse application exiting.", EventLogEntryType.Information, 102);
            }
        }
    }
}