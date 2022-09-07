using System;
using System.Threading.Tasks;

namespace WPFUtil
{
    public static class Extensions
    {
#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
        public static async void FireAndForget(this Task task, Action<Exception> errorHandler)
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                errorHandler(e);
            }
        }
    }
}