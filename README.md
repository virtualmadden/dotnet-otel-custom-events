# Custom events in .NET with OpenTelemetry

> **Archived.** This is a finished example, kept for reference. It isn't maintained, and its dependencies will age.

A small .NET 10 app that records business events, such as "checkout completed", with the logging tools built into .NET, exports them over OpenTelemetry, and turns them into [New Relic custom events](https://docs.newrelic.com/docs/data-apis/custom-data/custom-events/report-custom-event-data/) you can query with NRQL.

This repo started in 2018 as `NewRelic.CustomEvents`, a library that used reflection and its own attributes to send objects to New Relic's agent API. Everything it did now ships with .NET and OpenTelemetry, so this repo shows how to do the same with those instead. The original library is still in the history at [`a04794b`](https://github.com/virtualmadden/new-relic-custom-events/tree/a04794b).

| 2018 library | Today |
| --- | --- |
| `new ServiceEvent(...).SendAsync()` | A `[LoggerMessage]` method, generated at compile time ([`CheckoutEvents.cs`](src/CustomEvents/Checkout/CheckoutEvents.cs)) |
| Reflection over the event's properties | `[LogProperties]` on the method parameter |
| `[NewRelicIgnore]` | `[LogPropertyIgnore]` |
| `[NewRelicName("...")]` | `[TagName("...")]`, on parameters for now ([see below](#things-to-know)) |
| Event type from the class name | `EventName` on `[LoggerMessage]`, mapped to a New Relic event type |
| Nothing for sensitive values | `[CustomerData]` classification, HMAC-hashed before export ([`Order.cs`](src/CustomEvents/Checkout/Order.cs)) |
| New Relic's .NET agent only | Any OpenTelemetry backend |

## How it works

```csharp
public sealed record Order(
    string OrderId,
    long TotalCents,
    string Currency,
    [property: CustomerData] string CustomerEmail,       // hashed before it leaves the process
    [property: LogPropertyIgnore] string? CardNumber);   // never logged

[LoggerMessage(EventId = 1, EventName = "com.example.checkout.completed", Level = LogLevel.Information, Message = "Checkout completed")]
public static partial void CheckoutCompleted(this ILogger logger, [LogProperties(OmitReferenceName = true)] Order order);

logger.CheckoutCompleted(order);
```

That produces an OpenTelemetry event (a log record with an event name) with `OrderId`, `TotalCents`, `Currency` and a hashed `CustomerEmail`. New Relic stores OpenTelemetry events as logs unless they carry a `newrelic.event.type` attribute, so [`NewRelicEventTypeProcessor`](src/CustomEvents/Telemetry/NewRelicEventTypeProcessor.cs) adds one for each event listed in `CheckoutEvents.NewRelicEventTypes`. Each of those events then lands as a custom event of that type:

```sql
SELECT count(*), sum(TotalCents) / 100 FROM CheckoutCompleted FACET Currency SINCE 1 day ago
```

## Running it

You need the .NET 10 SDK. Redaction needs an HMAC key of at least 44 base64 characters; use a different one in every environment and keep it secret.

```sh
export Redaction__Key="$(openssl rand -base64 32)" Redaction__KeyId=1
dotnet run --project src/CustomEvents
dotnet test
```

Without an OTLP endpoint, the events print to the console. To send them to New Relic, set the standard OpenTelemetry variables with your [license key](https://docs.newrelic.com/docs/apis/intro-apis/new-relic-api-keys/#license-key) (use `https://otlp.eu01.nr-data.net:4318` for EU accounts):

```sh
export OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp.nr-data.net:4318
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
export OTEL_EXPORTER_OTLP_HEADERS="api-key=<license key>"
dotnet run --project src/CustomEvents
```

The tests run the app's real telemetry pipeline, with a batching exporter like OTLP's, and check what would be exported: event names and types, attribute values, the hash, and that the card number never appears.

## Things to know

- **`[TagName]` is ignored on `[LogProperties]` members.** Attributes from an object are named after its properties, so `CheckoutFailed` renames its `reason` parameter instead. Tracked in [dotnet/extensions#6605](https://github.com/dotnet/extensions/issues/6605).
- **Redacted values vanish from OTLP exports without a fix.** Redaction hands the logger pooled values that are recycled once the log call returns, but the OTLP exporter reads attributes later on another thread, so redacted attributes are silently dropped. [`FreezePooledValuesProcessor`](src/CustomEvents/Telemetry/FreezePooledValuesProcessor.cs) turns them into strings first. Tracked in [open-telemetry/opentelemetry-dotnet#5276](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5276).
- **Event names and types differ in style.** OpenTelemetry event names are namespaced (`com.example.checkout.completed`); New Relic event types are PascalCase (`CheckoutCompleted`). The mapping is explicit because dashboards and alerts depend on the type name.
- **Store money as numbers.** OpenTelemetry exports `decimal` as a string, which New Relic can't sum, so the order total is in cents as a `long`.

## Layout

| Path | What |
| --- | --- |
| `src/CustomEvents/Checkout/` | The event definitions and the `Order` they describe |
| `src/CustomEvents/Telemetry/` | Redaction, OpenTelemetry setup and the two processors |
| `tests/CustomEvents.Tests/` | End-to-end tests of the telemetry pipeline |
