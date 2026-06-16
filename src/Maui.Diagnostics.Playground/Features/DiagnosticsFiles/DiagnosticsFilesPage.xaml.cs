using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace Maui.Diagnostics.Playground.Features.DiagnosticsFiles;

public partial class DiagnosticsFilesPage : ContentPage
{
    public const string Route = "diagnostics-files";

    private readonly IDiagnosticsArtifactService artifactService;
    private bool loaded;
    private bool isLoading;
    private string statusText = "Loading diagnostics files...";

    public DiagnosticsFilesPage(IDiagnosticsArtifactService artifactService)
    {
        this.artifactService = artifactService;
        Artifacts.CollectionChanged += ArtifactsChanged;
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        RefreshCommand = new Command(async () => await RefreshAsync(), () => IsNotLoading);
        OpenFileCommand = new Command<DiagnosticsArtifactFile>(OpenFile);
        ShareFileCommand = new Command<DiagnosticsArtifactFile>(ShareFile);
        ShareAllCommand = new Command(ShareAll, () => HasFiles);
        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<DiagnosticsArtifactFile> Artifacts { get; } = [];

    public ICommand BackCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand OpenFileCommand { get; }

    public ICommand ShareFileCommand { get; }

    public ICommand ShareAllCommand { get; }

    public string StatusText
    {
        get => statusText;
        private set
        {
            statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotLoading));
            ((Command)RefreshCommand).ChangeCanExecute();
        }
    }

    public bool IsNotLoading => !IsLoading;

    public bool HasFiles => Artifacts.Count > 0;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!loaded)
        {
            loaded = true;
            await RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var files = await artifactService.ListAsync();
            Artifacts.Clear();

            foreach (var file in files)
            {
                Artifacts.Add(file);
            }

            StatusText = files.Count == 0
                ? "No diagnostics files found yet. Trigger a crash and relaunch the app to inspect app-private artifacts."
                : $"{files.Count} diagnostics file{(files.Count == 1 ? string.Empty : "s")} found.";
        }
        catch (IOException ex)
        {
            await DisplayAlertAsync("Could not refresh diagnostics files", ex.Message, "OK");
        }
        catch (UnauthorizedAccessException ex)
        {
            await DisplayAlertAsync("Could not refresh diagnostics files", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async void OpenFile(DiagnosticsArtifactFile? file)
    {
        if (file is null)
        {
            return;
        }

        await Shell.Current.GoToAsync(DiagnosticsFileDetailPage.Route, new ShellNavigationQueryParameters
        {
            { "path", file.FullPath }
        });
    }

    private async void ShareFile(DiagnosticsArtifactFile? file)
    {
        if (file is null)
        {
            return;
        }

        await artifactService.ShareAsync(file);
    }

    private async void ShareAll()
    {
        if (!HasFiles)
        {
            await DisplayAlertAsync("No diagnostics files", "Trigger a crash and relaunch the app before sharing diagnostics artifacts.", "OK");
            return;
        }

        await artifactService.ShareAsync(Artifacts.ToArray());
    }

    private void ArtifactsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasFiles));
        ((Command)ShareAllCommand).ChangeCanExecute();
    }
}
