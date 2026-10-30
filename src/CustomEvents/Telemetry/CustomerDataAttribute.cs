using Microsoft.Extensions.Compliance.Classification;

namespace CustomEvents.Telemetry;

public static class DataTaxonomy
{
    public static DataClassification CustomerData { get; } = new("CustomEvents", nameof(CustomerData));
}

// Marks a value as personal data, so the redactor replaces it before it leaves the process.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CustomerDataAttribute() : DataClassificationAttribute(DataTaxonomy.CustomerData);
