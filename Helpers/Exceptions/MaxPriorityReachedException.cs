namespace PangolinWatchdog.Helpers.Exceptions;

/// <summary>
/// We have reached the maximum priority for a rule and cannot increase it further.
/// </summary>
public class MaxPriorityReachedException : ApplicationException
{
    public MaxPriorityReachedException()
    {
    }

    public MaxPriorityReachedException(string? message) : base(message)
    {
    }

    public MaxPriorityReachedException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}