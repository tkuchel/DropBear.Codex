#region

using Serilog;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace DropBear.Codex.Core.Logging;

public class NoOpLogger : ILogger
{
    public ILogger ForContext(ILogEventEnricher enricher)
    {
        return this;
    }

    public ILogger ForContext(IEnumerable<ILogEventEnricher> enrichers)
    {
        return this;
    }

    public ILogger ForContext(string propertyName, object value, bool destructureObjects = false)
    {
        return this;
    }

    public ILogger ForContext<TSource>()
    {
        return this;
    }

    public ILogger ForContext(Type source)
    {
        return this;
    }

    public void Write(LogEvent logEvent)
    {
        // No operation performed
    }

    public void Write(LogEventLevel level, string messageTemplate)
    {
        // No operation performed
    }

    public void Write<T>(LogEventLevel level, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Write<T0, T1>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Write<T0, T1, T2>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1,
        T2 propertyValue2)
    {
        // No operation performed
    }

    public void Write(LogEventLevel level, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Write(LogEventLevel level, Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0,
        T1 propertyValue1)
    {
        // No operation performed
    }

    public void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0,
        T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Write(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public bool IsEnabled(LogEventLevel level)
    {
        return false;
    }

    public void Verbose(string messageTemplate)
    {
        // No operation performed
    }

    public void Verbose<T>(string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Verbose<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Verbose(string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Verbose(Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Verbose<T>(Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Verbose<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Verbose<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1,
        T2 propertyValue2)
    {
        // No operation performed
    }

    public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Debug(string messageTemplate)
    {
        // No operation performed
    }

    public void Debug<T>(string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Debug<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Debug(string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Debug(Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Debug<T>(Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Debug<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1,
        T2 propertyValue2)
    {
        // No operation performed
    }

    public void Debug(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Information(string messageTemplate)
    {
        // No operation performed
    }

    public void Information<T>(string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Information<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Information<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Information(string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Information(Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Information<T>(Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Information<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0,
        T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Information(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Warning(string messageTemplate)
    {
        // No operation performed
    }

    public void Warning<T>(string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Warning<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Warning(string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Warning(Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Warning<T>(Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1,
        T2 propertyValue2)
    {
        // No operation performed
    }

    public void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Error(string messageTemplate)
    {
        // No operation performed
    }

    public void Error<T>(string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Error<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Error(string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Error(Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Error<T>(Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Error<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1,
        T2 propertyValue2)
    {
        // No operation performed
    }

    public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Fatal(string messageTemplate)
    {
        // No operation performed
    }

    public void Fatal<T>(string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Fatal<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
    {
        // No operation performed
    }

    public void Fatal(string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public void Fatal(Exception exception, string messageTemplate)
    {
        // No operation performed
    }

    public void Fatal<T>(Exception exception, string messageTemplate, T propertyValue)
    {
        // No operation performed
    }

    public void Fatal<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
    {
        // No operation performed
    }

    public void Fatal<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1,
        T2 propertyValue2)
    {
        // No operation performed
    }

    public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        // No operation performed
    }

    public bool BindMessageTemplate(string messageTemplate, object[] propertyValues, out MessageTemplate parsedTemplate,
        out IEnumerable<LogEventProperty> boundProperties)
    {
        parsedTemplate = null!;
        boundProperties = null!;
        return false;
    }

    public bool BindProperty(string propertyName, object value, bool destructureObjects, out LogEventProperty property)
    {
        property = null!;
        return false;
    }
}
