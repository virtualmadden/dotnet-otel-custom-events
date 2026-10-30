using Microsoft.Extensions.Logging;

namespace CustomEvents.Checkout;

// Each method is one event. The source generator turns the [LogProperties(OmitReferenceName = true)] parameter's
// properties into attributes at compile time, with no reflection.
public static partial class CheckoutEvents
{
    public const string Completed = "com.example.checkout.completed";
    public const string Failed = "com.example.checkout.failed";

    // OpenTelemetry event names are namespaced snake case; New Relic event types are
    // PascalCase. Events listed here become New Relic custom events; others stay logs.
    public static readonly IReadOnlyDictionary<string, string> NewRelicEventTypes =
        new Dictionary<string, string>
        {
            [Completed] = "CheckoutCompleted",
            [Failed] = "CheckoutFailed",
        };

    [LoggerMessage(EventId = 1, EventName = Completed, Level = LogLevel.Information, Message = "Checkout completed")]
    public static partial void CheckoutCompleted(this ILogger logger, [LogProperties(OmitReferenceName = true)] Order order);

    // [TagName] renames a parameter's attribute. It's ignored on [LogProperties] members for now:
    // https://github.com/dotnet/extensions/issues/6605
    [LoggerMessage(EventId = 2, EventName = Failed, Level = LogLevel.Warning, Message = "Checkout failed: {checkout.failure_reason}")]
    public static partial void CheckoutFailed(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] Order order,
        [TagName("checkout.failure_reason")] string reason);
}
