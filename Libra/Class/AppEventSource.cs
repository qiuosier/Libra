using System.Diagnostics.Tracing;

/// <summary>
/// This class is from
/// https://code.msdn.microsoft.com/windowsapps/Logging-Sample-for-Windows-0b9dffd7
/// </summary>

namespace Libra
{
    sealed class AppEventSource : EventSource
    {
        public static AppEventSource Log = new AppEventSource();

        [Event(1, Level = EventLevel.Verbose)]
        public void Debug(string message)
        {
            this.WriteEvent(1, message);
        }

        [Event(2, Level = EventLevel.Informational)]
        public void Info(string message)
        {
            this.WriteEvent(2, message);
        }

        [Event(3, Level = EventLevel.Warning)]
        public void Warn(string message)
        {
            this.WriteEvent(3, message);
        }

        [Event(4, Level = EventLevel.Error)]
        public void Error(string message)
        {
            this.WriteEvent(4, message);
        }

        [Event(5, Level = EventLevel.Critical)]
        public void Critical(string message)
        {
            this.WriteEvent(5, message);
        }
    }
}
