using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Ansary;

public sealed partial class LibraryWindow
{
    Button LocalButton(Panel panel, string label, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LocalButton(panel, label, (Func<Task>)(() =>
        {
            action();
            return Task.CompletedTask;
        }));
    }
}
