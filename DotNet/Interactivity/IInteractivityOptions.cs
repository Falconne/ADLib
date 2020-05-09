namespace ADLib.Interactivity
{
    public interface IInteractivityOptions
    {
        bool Debug { get; }

        string Log { get; }

        bool Passive { get; }

        bool Pause { get; }
    }
}