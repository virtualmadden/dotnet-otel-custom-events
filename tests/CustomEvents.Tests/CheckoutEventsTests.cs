using CustomEvents.Checkout;
using CustomEvents.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace CustomEvents.Tests;

// Runs the app's real telemetry pipeline and inspects each record as the exporter would see it.
public sealed partial class CheckoutEventsTests
{
    private const string CardNumber = "4111111111111111";
    private const string Email = "ada@example.com";

    private static readonly Order SampleOrder = new("ord_1042", 3850, "USD", Email, CardNumber);

    [Fact]
    public void CompletedOrderBecomesANewRelicCustomEvent()
    {
        var exported = Capture(logger => logger.CheckoutCompleted(SampleOrder));

        var record = Assert.Single(exported);
        Assert.Equal(CheckoutEvents.Completed, record.Name);
        Assert.Equal("CheckoutCompleted", record.Attributes[NewRelicEventTypeProcessor.AttributeName]);
        Assert.Equal("ord_1042", record.Attributes["OrderId"]);
        Assert.Equal(3850L, record.Attributes["TotalCents"]);
        Assert.Equal("USD", record.Attributes["Currency"]);
    }

    [Fact]
    public void FailedOrderCarriesTheRenamedReason()
    {
        var exported = Capture(logger => logger.CheckoutFailed(SampleOrder, "card_declined"));

        var record = Assert.Single(exported);
        Assert.Equal(CheckoutEvents.Failed, record.Name);
        Assert.Equal("CheckoutFailed", record.Attributes[NewRelicEventTypeProcessor.AttributeName]);
        Assert.Equal("card_declined", record.Attributes["checkout.failure_reason"]);
    }

    [Fact]
    public void CustomerEmailIsHashedConsistently()
    {
        var secondOrder = SampleOrder with { OrderId = "ord_1043" };
        var exported = Capture(logger =>
        {
            logger.CheckoutCompleted(SampleOrder);
            logger.CheckoutCompleted(secondOrder);
        });

        var hashes = exported.Select(record => record.Attributes["CustomerEmail"]).ToList();
        Assert.All(hashes, hash => Assert.DoesNotContain("@", Assert.IsType<string>(hash), StringComparison.Ordinal));
        Assert.Single(hashes.Distinct());
    }

    [Fact]
    public void CardNumberNeverLeavesTheProcess()
    {
        var exported = Capture(logger => logger.CheckoutCompleted(SampleOrder));

        var record = Assert.Single(exported);
        Assert.DoesNotContain(nameof(Order.CardNumber), record.Attributes.Keys);
        Assert.DoesNotContain(CardNumber, record.Attributes.Values.Select(value => value?.ToString()));
    }

    [Fact]
    public void UnmappedEventsStayLogs()
    {
        var exported = Capture(logger =>
        {
            UnmappedEvent(logger);
            PlainLog(logger);
        });

        Assert.Equal(2, exported.Count);
        Assert.All(exported, record => Assert.DoesNotContain(NewRelicEventTypeProcessor.AttributeName, record.Attributes.Keys));
    }

    private static List<ExportedRecord> Capture(Action<ILogger> log)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redaction:Key"] = Convert.ToBase64String(new byte[32]),
            ["Redaction:KeyId"] = "1",
        });
        builder.AddCustomEventTelemetry();

        var exported = new List<ExportedRecord>();
        // Batched, like the OTLP exporter, so values are read on another thread after the log call returns.
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging =>
            logging.AddProcessor(new BatchLogRecordExportProcessor(new CaptureExporter(exported))));

        // Disposing the host flushes the pipeline, so every record has been processed on return.
        using (var host = builder.Build())
        {
            log(host.Services.GetRequiredService<ILogger<CheckoutEventsTests>>());
        }

        return exported;
    }

    [LoggerMessage(EventId = 9, EventName = "com.example.other", Level = LogLevel.Information, Message = "Something else happened")]
    private static partial void UnmappedEvent(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "No event name at all")]
    private static partial void PlainLog(ILogger logger);

    private sealed record ExportedRecord(string? Name, Dictionary<string, object?> Attributes);

    // LogRecords are pooled and reused after export, so copy what the tests need. Values that
    // aren't primitives are formatted, as the OTLP exporter does.
    private sealed class CaptureExporter(List<ExportedRecord> exported) : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                exported.Add(new(
                    record.EventId.Name,
                    (record.Attributes ?? []).ToDictionary(pair => pair.Key, pair => pair.Value switch
                    {
                        null or string or bool or long or int or double => pair.Value,
                        var other => other.ToString(),
                    })));
            }

            return ExportResult.Success;
        }
    }
}
