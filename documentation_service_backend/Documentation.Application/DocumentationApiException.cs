namespace Documentation.Application;

/// <summary>Erreur métier à mapper vers un code HTTP côté API.</summary>
public sealed class DocumentationApiException(int statusCode, string message, object? payload = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public object? Payload { get; } = payload;
}
