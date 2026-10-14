// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Windows;

namespace PsGuiBuilder.ViewModels;

public interface IUserConfirmation
{
    bool Confirm(string message);
}

public sealed class MessageBoxConfirmation : IUserConfirmation
{
    public bool Confirm(string message) =>
        MessageBox.Show(message, ProductName.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
