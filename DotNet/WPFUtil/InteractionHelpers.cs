using System.Windows;

namespace WPFUtil;

public static class InteractionHelpers
{
    public static bool GetConfirmation(string message, string title = "Confirm Action")
    {
        return Application.Current.Dispatcher.Invoke(
            () => MessageBox.Show(
                      Application.Current.MainWindow!,
                      message,
                      title,
                      MessageBoxButton.YesNo,
                      MessageBoxImage.Question)
                  == MessageBoxResult.Yes);
    }
}