// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder;

public sealed class SignInDialog : ICredentialPrompt
{
    public CredentialAnswer? Ask(SignInRequirement requirement)
    {
        var user = new TextBox { Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(8, 6, 8, 6) };
        var password = new PasswordBox { Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(8, 6, 8, 6) };
        var error = new TextBlock { Foreground = System.Windows.Media.Brushes.DarkRed, TextWrapping = TextWrapping.Wrap };
        var accepted = false;
        var window = new Window
        {
            Title = "Sign in to " + requirement.Service,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            Owner = Application.Current?.MainWindow
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(CreateButton("Cancel", () => window.Close()));
        buttons.Children.Add(CreateButton("Sign in", () =>
        {
            if (string.IsNullOrWhiteSpace(user.Text) || password.SecurePassword.Length == 0)
            {
                error.Text = "Enter a user name and password.";
                return;
            }

            accepted = true;
            window.Close();
        }));
        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock { Text = requirement.Prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) },
                new TextBlock { Text = "User name" },
                user,
                new TextBlock { Text = "Password" },
                password,
                error,
                buttons
            }
        };

        window.ShowDialog();
        if (!accepted)
        {
            password.Clear();
            user.Clear();
            return null;
        }

        var passwordChars = ReadPassword(password.SecurePassword);
        var result = new CredentialAnswer(user.Text.Trim(), passwordChars);
        Array.Clear(passwordChars);
        password.Clear();
        user.Clear();
        return result;
    }

    private static Button CreateButton(string content, Action action)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(8, 8, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
            MinWidth = 88
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static char[] ReadPassword(SecureString secret)
    {
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(secret);
        try
        {
            return (Marshal.PtrToStringUni(pointer) ?? "").ToCharArray();
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
            secret.Dispose();
        }
    }
}
