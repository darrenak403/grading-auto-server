namespace GradingSystem.Application.Exceptions;

/// <summary>
/// Base type for application/domain errors surfaced to the API layer.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }

    protected AppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
