namespace ADLib.Exceptions;

public class MissingDependencyException : Exception
{
    public MissingDependencyException(string message) : base(message)
    {
    }
}