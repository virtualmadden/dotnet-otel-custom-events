using CustomEvents.Telemetry;
using Microsoft.Extensions.Logging;

namespace CustomEvents.Checkout;

// [LogProperties] turns each property into an attribute named after it. [CustomerData] routes a
// value through the redactor, and [LogPropertyIgnore] keeps a property off the event entirely.
public sealed record Order(
    string OrderId,
    // Minor units, so New Relic can sum it; OpenTelemetry exports decimals as strings.
    long TotalCents,
    string Currency,
    [property: CustomerData] string CustomerEmail,
    [property: LogPropertyIgnore] string? CardNumber);
