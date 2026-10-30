using CustomEvents.Checkout;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace CustomEvents.Telemetry;

public static class TelemetryExtensions
{
    public static IHostApplicationBuilder AddCustomEventTelemetry(this IHostApplicationBuilder builder)
    {
        // HMAC rather than erasing, so events from the same customer still group together.
        builder.Logging.EnableRedaction();
        builder.Services.AddRedaction(redaction =>
            redaction.SetHmacRedactor(builder.Configuration.GetSection("Redaction"), new DataClassificationSet(DataTaxonomy.CustomerData)));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("custom-events-example"))
            .WithLogging(logging =>
            {
                // Processors run in order, so these have to come before the exporter.
                logging.AddProcessor(new FreezePooledValuesProcessor());
                logging.AddProcessor(new NewRelicEventTypeProcessor(CheckoutEvents.NewRelicEventTypes));

                // The OTLP exporter reads the standard OTEL_EXPORTER_OTLP_* variables.
                if (string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                {
                    logging.AddConsoleExporter();
                }
                else
                {
                    logging.AddOtlpExporter();
                }
            });

        return builder;
    }
}
