using ADLib.Util;
using Microsoft.VisualBasic;
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

    public static void ShowError(string message, string title = "Error")
    {
        Application.Current.Dispatcher.Invoke(
            () => MessageBox.Show(
                Application.Current.MainWindow!,
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error));
    }

    public static string? GetInput(
        string message,
        string title = "Input Required",
        string? defaultValue = null)
    {
        var name = Interaction.InputBox(
            message,
            title,
            defaultValue ?? "");

        return name.IsEmpty() ? null : name.Trim();
    }
}