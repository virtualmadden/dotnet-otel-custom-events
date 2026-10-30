using CustomEvents.Checkout;
using CustomEvents.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.AddCustomEventTelemetry();

// Disposing the host flushes any events still buffered in the exporter.
using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

var order = new Order("ord_1042", 3850, "USD", "ada@example.com", CardNumber: "4111111111111111");
logger.CheckoutCompleted(order);
logger.CheckoutFailed(order with { OrderId = "ord_1043" }, "card_declined");
