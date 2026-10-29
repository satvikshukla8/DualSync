using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DualSync.App;

// A saved-entry picker is independent of the live EQ: selecting an entry does not load it.
public sealed class SavedEqViewModel : Observable
{
    private string? selectedName;
    private string name = "";
    public ObservableCollection<string> Names { get; } = [];
    public string? SelectedName
    {
        get => selectedName;
        set
        {
            if (!Set(ref selectedName, value)) return;
            if (value is not null) Name = value;
            Notify(nameof(HasSelection));
        }
    }
    public string Name
    {
        get => name;
        set { if (Set(ref name, value)) { Notify(nameof(CanSave)); Notify(nameof(SaveLabel)); } }
    }
    public bool HasSelection => SelectedName is not null && Names.Contains(SelectedName);
    public bool CanSave => !string.IsNullOrWhiteSpace(Name);
    public string SaveLabel => Names.Any(n => n.Equals(Name.Trim(), StringComparison.OrdinalIgnoreCase)) ? "Replace EQ" : "Save EQ";
    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }

    public SavedEqViewModel(Action<string> load, Action<string> save, Action<string> delete, Action<string> report)
    {
        LoadCommand = new ActionCommand(_ => { if (HasSelection) load(SelectedName!); });
        SaveCommand = new ActionCommand(_ =>
        {
            if (!CanSave) return;
            try { save(Name.Trim()); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { report(ex.Message); }
        });
        DeleteCommand = new ActionCommand(_ => { if (HasSelection) delete(SelectedName!); });
    }

    public void Refresh(IEnumerable<string> names, string? select = null)
    {
        var previous = select ?? SelectedName;
        var draft = Name;
        Names.Clear();
        foreach (var value in names.Order(StringComparer.OrdinalIgnoreCase)) Names.Add(value);
        SelectedName = Names.FirstOrDefault(n => n.Equals(previous, StringComparison.OrdinalIgnoreCase));
        if (select is null) Name = draft;
        Notify(nameof(HasSelection)); Notify(nameof(SaveLabel));
    }
}
