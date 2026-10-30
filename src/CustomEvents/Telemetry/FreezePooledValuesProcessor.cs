using Microsoft.Extensions.ObjectPool;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace CustomEvents.Telemetry;

// Redaction hands the logger pooled values that are recycled as soon as ILogger.Log returns, but
// batching exporters like OTLP read attributes later, on another thread. Without this, redacted
// attributes are silently dropped from OTLP exports:
// https://github.com/open-telemetry/opentelemetry-dotnet/issues/5276
internal sealed class FreezePooledValuesProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        if (data.Attributes is { } attributes && attributes.Any(pair => pair.Value is IResettable))
        {
            data.Attributes = [.. attributes.Select(pair => pair.Value is IResettable ? new(pair.Key, pair.Value.ToString()) : pair)];
        }
    }
}
