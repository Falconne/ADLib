using System;

namespace ADLib.Exceptions
{
    public class FatalException : Exception
    {
        public FatalException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}