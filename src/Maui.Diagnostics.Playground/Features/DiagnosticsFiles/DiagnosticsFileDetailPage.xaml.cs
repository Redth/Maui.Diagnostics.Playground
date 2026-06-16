using System.Windows.Input;

namespace Maui.Diagnostics.Playground.Features.DiagnosticsFiles;

public partial class DiagnosticsFileDetailPage : ContentPage, IQueryAttributable
{
    public const string Route = "diagnostics-file";

    private readonly IDiagnosticsArtifactService artifactService;
    private DiagnosticsArtifactFile? file;
    private string fileName = "Diagnostics file";
    private string metadataText = "Loading...";
    private string fileContent = "Loading...";

    public DiagnosticsFileDetailPage(IDiagnosticsArtifactService artifactService)
    {
        this.artifactService = artifactService;
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ReloadCommand = new Command(async () => await LoadContentAsync(), () => HasFile);
        ShareCommand = new Command(async () => await ShareAsync(), () => HasFile);
        InitializeComponent();
        BindingContext = this;
    }

    public ICommand BackCommand { get; }

    public ICommand ReloadCommand { get; }

    public ICommand ShareCommand { get; }

    public string FileName
    {
        get => fileName;
        private set
        {
            fileName = value;
            OnPropertyChanged();
        }
    }

    public string MetadataText
    {
        get => metadataText;
        private set
        {
            metadataText = value;
            OnPropertyChanged();
        }
    }

    public string FileContent
    {
        get => fileContent;
        private set
        {
            fileContent = value;
            OnPropertyChanged();
        }
    }

    public bool HasFile => file is not null;

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("path", out var value) || value is not string path)
        {
            throw new InvalidOperationException("Diagnostics file navigation requires a path query parameter.");
        }

        file = await artifactService.GetAsync(path);
        FileName = file.Name;
        MetadataText = $"{file.SummaryText}{Environment.NewLine}{file.FullPath}";
        OnPropertyChanged(nameof(HasFile));
        ((Command)ReloadCommand).ChangeCanExecute();
        ((Command)ShareCommand).ChangeCanExecute();

        await LoadContentAsync();
    }

    private async Task LoadContentAsync()
    {
        if (file is null)
        {
            throw new InvalidOperationException("Cannot load diagnostics file content before navigation supplies a path.");
        }

        FileContent = await artifactService.ReadTextAsync(file);
    }

    private async Task ShareAsync()
    {
        if (file is null)
        {
            throw new InvalidOperationException("Cannot share diagnostics file before navigation supplies a path.");
        }

        await artifactService.ShareAsync(file);
    }
}
