namespace Prime.Application;

/// <summary>Erreur métier à mapper vers un code HTTP côté API.</summary>
public sealed class PrimeApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
