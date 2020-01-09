namespace AKBDLib.Interactivity
{
    public interface IInteractionHandler
    {
        void ExitWithError(string message);

        void ExitWithSuccess(string message);

        string GetTextInput(string prompt, string defaultValue);

        string GetTextInput(string prompt);

        int GetIntegerInput(string prompt, int defaultValue, int min = int.MinValue, int max = int.MaxValue);

        bool GetYesNoResponse(string prompt);

        bool IsPassive();
    }
}