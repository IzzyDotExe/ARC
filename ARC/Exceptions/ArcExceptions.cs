namespace Arc.Exceptions;

public class ArcException : Exception
{
    protected const string Empty = "No message given";
    protected ArcException(string message) : base(message) { }
}
public class ArcNotInitializedException : ArcException
{
    public ArcNotInitializedException(string? message = null) : base($"ARC was not properly initialized: {message ?? Empty}") { }
}

public class ArcInitFailedException : ArcException
{
    public ArcInitFailedException(string? message = null) : base($"ARC Initialization failed: {message ?? Empty}") { }
}