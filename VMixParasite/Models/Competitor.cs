using CommunityToolkit.Mvvm.ComponentModel;

namespace VMixParasite.Models;

public partial class Competitor : ObservableObject
{
    [ObservableProperty]
    private int _dorsal;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _club = string.Empty;
}
