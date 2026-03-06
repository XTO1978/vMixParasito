using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VMixParasite.Models;
using VMixParasite.Services;

namespace VMixParasite.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly VmixApiService _vmixService;
    private HashSet<string>? _preRecordingFiles;

    public MainViewModel(VmixApiService vmixService)
    {
        _vmixService = vmixService;
        _ = AutoConnectAsync();
    }

    private async Task AutoConnectAsync()
    {
        _vmixService.Host = VmixHost;
        if (int.TryParse(VmixPort, out var port))
            _vmixService.Port = port;

        StatusMessage = "Conectando a vMix...";
        var (connected, message) = await _vmixService.TestConnectionAsync();
        IsConnected = connected;
        ConnectionStatus = connected ? "● Conectado" : "✖ Error";
        StatusMessage = message;
    }

    // --- Connection settings ---

    [ObservableProperty]
    private string _vmixHost = "127.0.0.1";

    [ObservableProperty]
    private string _vmixPort = "8088";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Desconectado";

    // --- Title field mapping ---

    [ObservableProperty]
    private string _titleInput = "1";

    [ObservableProperty]
    private string _nameField = "TextBlock1.Text";

    [ObservableProperty]
    private string _dorsalField = "TextBlock0.Text";

    [ObservableProperty]
    private string _clubField = "TextBlock2.Text";

    [ObservableProperty]
    private string _nextUpField = "TextBlock3.Text";

    // --- Recording ---

    [ObservableProperty]
    private string _recordingFolder = string.Empty;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingButtonText = "⏺ GRABAR";

    [ObservableProperty]
    private Color _recordingButtonColor = Colors.Green;

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        try
        {
            var result = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync(default);
            if (result.IsSuccessful)
            {
                RecordingFolder = result.Folder.Path;
                StatusMessage = $"Carpeta: {RecordingFolder}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error seleccionando carpeta: {ex.Message}";
        }
    }

    // --- Competitors ---

    public ObservableCollection<Competitor> Competitors { get; } = new();

    [ObservableProperty]
    private Competitor? _selectedCompetitor;

    [ObservableProperty]
    private bool _hasSelectedCompetitor;

    // --- Status ---

    [ObservableProperty]
    private string _statusMessage = "Listo";

    partial void OnSelectedCompetitorChanged(Competitor? value)
    {
        HasSelectedCompetitor = value != null;
        SendToTitleCommand.NotifyCanExecuteChanged();

        if (value != null)
            _ = SendToTitleAsync();
    }

    partial void OnVmixHostChanged(string value) => _vmixService.Host = value;

    partial void OnVmixPortChanged(string value)
    {
        if (int.TryParse(value, out var port))
            _vmixService.Port = port;
    }

    // --- Commands ---

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        _vmixService.Host = VmixHost;
        if (int.TryParse(VmixPort, out var port))
            _vmixService.Port = port;

        StatusMessage = $"Conectando a {VmixHost}:{VmixPort}...";
        var (connected, message) = await _vmixService.TestConnectionAsync();
        IsConnected = connected;
        ConnectionStatus = connected ? "● Conectado" : "✖ Error";
        StatusMessage = message;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedCompetitor))]
    private async Task SendToTitleAsync()
    {
        if (SelectedCompetitor == null) return;

        try
        {
            var results = new List<string>();
            results.Add(await _vmixService.SetTitleTextAsync(TitleInput, NameField, SelectedCompetitor.Name));
            results.Add(await _vmixService.SetTitleTextAsync(TitleInput, DorsalField, SelectedCompetitor.Dorsal.ToString()));
            results.Add(await _vmixService.SetTitleTextAsync(TitleInput, ClubField, SelectedCompetitor.Club));

            // Next Up: enviar el siguiente competidor de la lista
            var idx = Competitors.IndexOf(SelectedCompetitor);
            string nextName;
            if (idx >= 0 && idx < Competitors.Count - 1)
                nextName = Competitors[idx + 1].Name;
            else
                nextName = " "; // espacio para que vMix "limpie" el campo

            var nextUpResult = await _vmixService.SetTitleTextAsync(TitleInput, NextUpField, nextName);
            results.Add(nextUpResult);

            System.Diagnostics.Debug.WriteLine($"[NextUp] idx={idx}, count={Competitors.Count}, nextName=\"{nextName}\", field={NextUpField}, result={nextUpResult}");

            StatusMessage = $"#{SelectedCompetitor.Dorsal} {SelectedCompetitor.Name} | NextUp(idx={idx})=\"{nextName}\" | {string.Join(" | ", results)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error enviando rótulo: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        try
        {
            if (!IsRecording)
            {
                // Snapshot files before recording starts
                if (!string.IsNullOrEmpty(RecordingFolder) && Directory.Exists(RecordingFolder))
                    _preRecordingFiles = new HashSet<string>(Directory.GetFiles(RecordingFolder));

                await _vmixService.StartRecordingAsync();
                IsRecording = true;
                RecordingButtonText = "⏹ DETENER";
                RecordingButtonColor = Colors.Red;
                StatusMessage = "Grabando...";
            }
            else
            {
                await _vmixService.StopRecordingAsync();
                IsRecording = false;
                RecordingButtonText = "⏺ GRABAR";
                RecordingButtonColor = Colors.Green;

                await RenameRecordingAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error grabación: {ex.Message}";
        }
    }

    private async Task RenameRecordingAsync()
    {
        if (SelectedCompetitor == null || _preRecordingFiles == null || string.IsNullOrEmpty(RecordingFolder))
        {
            StatusMessage = "Grabación detenida (sin renombrar — selecciona un competidor y carpeta)";
            return;
        }

        // Wait for vMix to finalize the file
        await Task.Delay(2000);

        try
        {
            var currentFiles = Directory.GetFiles(RecordingFolder);
            var newFiles = currentFiles
                .Where(f => !_preRecordingFiles.Contains(f))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (newFiles.Count > 0)
            {
                var recordedFile = newFiles[0];
                var ext = Path.GetExtension(recordedFile);
                var safeName = SanitizeFileName(SelectedCompetitor.Name);
                var newFileName = $"{SelectedCompetitor.Dorsal}_{safeName}{ext}";
                var newPath = Path.Combine(RecordingFolder, newFileName);

                // Avoid overwriting existing files
                if (File.Exists(newPath))
                {
                    var timestamp = DateTime.Now.ToString("HHmmss");
                    newFileName = $"{SelectedCompetitor.Dorsal}_{safeName}_{timestamp}{ext}";
                    newPath = Path.Combine(RecordingFolder, newFileName);
                }

                File.Move(recordedFile, newPath);
                StatusMessage = $"Clip guardado: {newFileName}";
            }
            else
            {
                StatusMessage = "Grabación detenida (archivo no encontrado en la carpeta)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error renombrando clip: {ex.Message}";
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    [RelayCommand]
    private void AddCompetitor()
    {
        var nextDorsal = Competitors.Count > 0 ? Competitors.Max(c => c.Dorsal) + 1 : 1;
        var competitor = new Competitor { Dorsal = nextDorsal, Name = "", Club = "" };
        Competitors.Add(competitor);
        SelectedCompetitor = competitor;
    }

    [RelayCommand]
    private void RemoveCompetitor()
    {
        if (SelectedCompetitor != null)
        {
            var idx = Competitors.IndexOf(SelectedCompetitor);
            Competitors.Remove(SelectedCompetitor);
            if (Competitors.Count > 0)
                SelectedCompetitor = Competitors[Math.Min(idx, Competitors.Count - 1)];
            else
                SelectedCompetitor = null;
        }
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar CSV de lista de salida",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv", ".txt" } },
                })
            });

            if (result == null) return;

            var lines = await File.ReadAllLinesAsync(result.FullPath);
            Competitors.Clear();

            foreach (var line in lines.Skip(1)) // Skip header row
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ';', ',' });
                if (parts.Length >= 3 && int.TryParse(parts[0].Trim(), out var dorsal))
                {
                    Competitors.Add(new Competitor
                    {
                        Dorsal = dorsal,
                        Name = parts[1].Trim(),
                        Club = parts[2].Trim()
                    });
                }
            }

            StatusMessage = $"Importados {Competitors.Count} competidores";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importando CSV: {ex.Message}";
        }
    }
}
