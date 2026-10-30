using OpenTelemetry;
using OpenTelemetry.Logs;

namespace CustomEvents.Telemetry;

// New Relic stores OpenTelemetry events as Logs unless the record carries newrelic.event.type,
// in which case it becomes a custom event of that type (SELECT * FROM CheckoutCompleted).
internal sealed class NewRelicEventTypeProcessor(IReadOnlyDictionary<string, string> eventTypes) : BaseProcessor<LogRecord>
{
    public const string AttributeName = "newrelic.event.type";

    public override void OnEnd(LogRecord data)
    {
        if (data.EventId.Name is { } name && eventTypes.TryGetValue(name, out var eventType))
        {
            data.Attributes = [.. data.Attributes ?? [], new(AttributeName, eventType)];
        }
    }
}
