using System;
using System.Threading.Tasks;

namespace Configurator.Client.Services;

public class AppState
{
    public event Action? OnChange;
    public event Func<Task>? OnSaveRequested;

    private string _searchTerm = "";
    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm != value)
            {
                _searchTerm = value;
                NotifyStateChanged();
            }
        }
    }

    public async Task RequestSave()
    {
        if (OnSaveRequested != null)
        {
            await OnSaveRequested.Invoke();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
