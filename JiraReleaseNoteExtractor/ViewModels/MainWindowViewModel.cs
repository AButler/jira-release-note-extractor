using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using JiraReleaseNoteExtractor.Helpers;
using JiraReleaseNoteExtractor.Models;
using JiraReleaseNoteExtractor.Properties;
using Newtonsoft.Json.Linq;
using Svg;

namespace JiraReleaseNoteExtractor.ViewModels {
  internal class MainWindowViewModel : ViewModelBase {
    private readonly string _includeInReleaseNotesLabel;

    private readonly string _noReleaseNoteText;

    private readonly HttpClient _httpClient;

    private readonly JiraUriBuilder _uriBuilder;

    private readonly string _baseIconPath;

    private readonly string _releaseNoteFieldName;

    private string _releaseNoteFieldId;

    private bool _isBusy;

    private string _message;

    private string _email;

    private string _apiToken;

    private bool _isConnected;

    private ICollection<Project> _projects;

    private string _selectedProject;

    private ICollection<ProjectVersion> _versions;

    private string _selectedVersion;

    private string _versionSummary;

    private string _releaseNotesText;

    public bool IsBusy {
      get => _isBusy;
      set => Set( ref _isBusy, value );
    }

    public string Message {
      get => _message;
      set => Set( ref _message, value );
    }

    public string Email {
      get => _email;
      set => Set( ref _email, value );
    }

    public string ApiToken {
      get => _apiToken;
      set => Set( ref _apiToken, value );
    }

    public bool IsConnected {
      get => _isConnected;
      set => Set( ref _isConnected, value );
    }

    public ICollection<Project> Projects {
      get => _projects;
      set => Set( ref _projects, value );
    }

    public string SelectedProject {
      get => _selectedProject;
      set => Set( ref _selectedProject, value );
    }

    public ICollection<ProjectVersion> Versions {
      get => _versions;
      set => Set( ref _versions, value );
    }

    public string SelectedVersion {
      get => _selectedVersion;
      set => Set( ref _selectedVersion, value );
    }

    public string VersionSummary {
      get => _versionSummary;
      set => Set( ref _versionSummary, value );
    }

    public string ReleaseNotesText {
      get => _releaseNotesText;
      set => Set( ref _releaseNotesText, value );
    }

    public RelayCommand GenerateApiTokenCommand { get; }

    public RelayCommand ConnectCommand { get; }

    public RelayCommand ViewIssuesCommand { get; }

    public RelayCommand RefreshIssuesCommand { get; }

    public RelayCommand<string> CopyCommand { get; }

    public string Version { get; }

    public MainWindowViewModel() {
      Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
      Message = "Ready";
      VersionSummary = "(Select a version)";

      GenerateApiTokenCommand = new RelayCommand( GenerateApiToken );
      ConnectCommand = new RelayCommand( Connect, CanConnect );
      ViewIssuesCommand = new RelayCommand( ViewIssues, HasSelectedVersion );
      RefreshIssuesCommand = new RelayCommand( Refresh, HasSelectedVersion );
      CopyCommand = new RelayCommand<string>( CopyText, CanCopyText );

      var baseUrl = ConfigurationManager.AppSettings["JiraBaseUrl"];
      _uriBuilder = new JiraUriBuilder( baseUrl );
      _releaseNoteFieldName = ConfigurationManager.AppSettings["ReleaseNoteFieldName"];
      _noReleaseNoteText = ConfigurationManager.AppSettings["NoReleaseNoteText"];
      _includeInReleaseNotesLabel = ConfigurationManager.AppSettings["IncludeInReleaseNotesLabel"];

      Email = Settings.Default.LastEmail;
      ApiToken = Settings.Default.LastApiToken;

      _baseIconPath = Path.Combine( Path.GetTempPath(), "JiraReleaseNoteExtractor", "ProjectAvatarCache" );
      if ( !Directory.Exists( _baseIconPath ) ) {
        Directory.CreateDirectory( _baseIconPath );
      }

      _httpClient = new HttpClient();
      _httpClient.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );

      PropertyChanged += OnPropertyChanged;
    }

    private void GenerateApiToken() {
      var url = "https://id.atlassian.com/manage/api-tokens";

      Process.Start( new ProcessStartInfo( url ) { UseShellExecute = true } );
    }

    private async void OnPropertyChanged( object sender, PropertyChangedEventArgs e ) {
      switch ( e.PropertyName ) {
        case nameof(SelectedProject):
          await OnSelectedProjectChanged();
          break;
        case nameof(SelectedVersion):
          await OnSelectedVersionChanged();
          break;
      }
    }

    private async Task OnSelectedProjectChanged() {
      Settings.Default.LastProject = SelectedProject;
      Settings.Default.Save();

      IsBusy = true;
      Message = "Retrieving versions...";

      Versions = await GetVersions( SelectedProject );

      if ( Versions.Any( p => string.Equals( p.Id, Settings.Default.LastVersion, StringComparison.InvariantCultureIgnoreCase ) ) ) {
        SelectedVersion = Settings.Default.LastVersion;
      }

      IsBusy = false;
      Message = "Ready";
    }

    private async Task OnSelectedVersionChanged() {
      Settings.Default.LastVersion = SelectedVersion;
      Settings.Default.Save();

      await RefreshIssues();
    }

    private async void Connect() {
      Settings.Default.LastEmail = Email;
      Settings.Default.LastApiToken = ApiToken;
      Settings.Default.Save();

      _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Basic", Convert.ToBase64String( Encoding.ASCII.GetBytes( $"{Email}:{ApiToken}" ) ) );

      IsConnected = false;
      IsBusy = true;
      Message = "Connecting...";

      var releaseNoteFieldTask = GetReleaseNoteFieldId();
      var projectsTask = GetProjects();

      try {
        await Task.WhenAll( releaseNoteFieldTask, projectsTask );
      } catch ( Exception ex ) {
        Settings.Default.LastApiToken = null;
        Settings.Default.Save();

        MessageBox.Show( ex.ToString(), "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error );

        IsBusy = false;
        Message = "Error connecting!";
        return;
      }

      _releaseNoteFieldId = releaseNoteFieldTask.Result;
      Projects = projectsTask.Result;

      if ( Projects.Any( p => string.Equals( p.Id, Settings.Default.LastProject, StringComparison.InvariantCultureIgnoreCase ) ) ) {
        SelectedProject = Settings.Default.LastProject;
      }

      IsConnected = true;
      IsBusy = false;
      Message = "Connected";
    }

    private async Task<string> GenerateReleaseNotes() {
      var query = GetReleaseNotesJiraJql();
      var url = _uriBuilder.GenerateReleaseNotesQueryUrl( query );
      var response = await _httpClient.GetStringAsync( url );
      var json = JObject.Parse( response );

      var epicText = new StringBuilder();
      var storyText = new StringBuilder();
      var bugText = new StringBuilder();
      var unknownText = new StringBuilder();

      foreach ( var issueJson in json["issues"] ) {
        var id = issueJson.Value<string>( "id" );
        var key = issueJson.Value<string>( "key" );
        var type = issueJson["fields"]["issuetype"].Value<string>("name");
        var releaseNote = issueJson["fields"].Value<string>( _releaseNoteFieldId );

        if ( string.Equals( releaseNote, _noReleaseNoteText, StringComparison.InvariantCultureIgnoreCase ) ) {
          continue;
        }

        if ( string.IsNullOrWhiteSpace( releaseNote ) ) {
          var issueUrl = _uriBuilder.GenerateIssueUrl( key );
          releaseNote = $"!!! NO RELEASE NOTE: {issueUrl} !!!";
        }

        switch ( type ) {
          case "Epic":
            epicText.AppendLine( $"  * {releaseNote}" );
            break;
          case "Story":
            storyText.AppendLine( $"  * {releaseNote} [{key}]" );
            break;
          case "Bug":
            bugText.AppendLine( $"  * {releaseNote} [{key}]" );
            break;
          default:
            unknownText.AppendLine( $"  * {releaseNote} [{key}]" );
            break;
        }
      }

      var resultsText = new StringBuilder();

      if ( epicText.Length > 0 ) {
        resultsText.AppendLine( "### Highlights\r\n" );
        resultsText.AppendLine( "We've introduced the following features:\r\n" );

        resultsText.AppendLine( epicText.ToString() );
      }

      if ( storyText.Length > 0 ) {
        resultsText.AppendLine( "### Additional improvements and changes\r\n" );
        resultsText.AppendLine( "We've implemented improvements to the following areas:\r\n" );

        resultsText.AppendLine( storyText.ToString() );
      }

      if ( bugText.Length > 0 ) {
        resultsText.AppendLine( "### Resolved Issues\r\n" );
        resultsText.AppendLine( "We've resolved issues with updates to the following areas:\r\n" );

        resultsText.AppendLine( bugText.ToString() );
      }

      if( unknownText.Length > 0 ) {
        resultsText.AppendLine( "### UNKNOWN\r\n" );

        resultsText.AppendLine( unknownText.ToString() );
      }

      return resultsText.ToString();
    }

    private string GetReleaseNotesJiraJql() {
      return $"fixVersion = {SelectedVersion} AND  type in ( Epic, Story, Bug ) AND ( labels = \"{_includeInReleaseNotesLabel}\" OR ( affectedVersion is empty or affectedVersion != {SelectedVersion} ) AND \"Epic Link\" is empty AND status = Closed AND resolution in (Fixed,Done) ) ORDER BY key ASC";
    }

    private void ViewIssues() {
      var query = GetReleaseNotesJiraJql();
      var url = _uriBuilder.GenerateViewIssuesUrl( query );

      Process.Start( new ProcessStartInfo( url ) { UseShellExecute = true } );
    }

    private async void Refresh() {
      await RefreshIssues();
    }

    private async Task RefreshIssues() {
      if ( SelectedVersion == null ) {
        VersionSummary = null;
        ReleaseNotesText = null;
        return;
      }

      IsBusy = true;
      Message = "Generating release notes...";
      ReleaseNotesText = null;

      var versionSummaryTask = GetVersionSummary();
      var releaseNotesTask = GenerateReleaseNotes();

      await Task.WhenAll( versionSummaryTask, releaseNotesTask );

      VersionSummary = versionSummaryTask.Result;
      ReleaseNotesText = releaseNotesTask.Result;

      ViewIssuesCommand.RaiseCanExecuteChanged();
      CopyCommand.RaiseCanExecuteChanged();

      IsBusy = false;
      Message = "Ready";
    }

    private async Task<string> GetReleaseNoteFieldId() {
      var url = _uriBuilder.GenerateFieldInfoUrl();
      var response = await _httpClient.GetStringAsync( url );
      var json = JArray.Parse( response );

      foreach ( var fieldJson in json ) {
        var name = fieldJson.Value<string>( "name" );
        if ( string.Equals( name, _releaseNoteFieldName ) ) {
          return fieldJson.Value<string>( "id" );
        }
      }

      throw new Exception( "Release Note field not found" );
    }

    private async Task<ICollection<Project>> GetProjects() {
      var url = _uriBuilder.GenerateGetProjectsUrl();
      var response = await _httpClient.GetStringAsync( url );
      var json = JArray.Parse( response );

      var projects = new List<Project>();

      foreach ( var item in json ) {
        var id = item.Value<string>( "id" );
        var key = item.Value<string>( "key" );
        var name = item.Value<string>( "name" );
        var iconUrl = item.Value<JObject>( "avatarUrls" ).Value<string>( "48x48" );
        var iconPath = Path.Combine( _baseIconPath, $"{key}.png" );

        Task.Run( () => DownloadProjectIcon( iconUrl, iconPath ) );

        projects.Add( new Project( id, key, name, iconPath ) );
      }

      return projects;
    }

    private async void DownloadProjectIcon( string iconUrl, string iconPath ) {
      if ( File.Exists( iconPath ) ) {
        return;
      }

      var response = await _httpClient.GetAsync( iconUrl );
      if ( !response.IsSuccessStatusCode ) {
        return;
      }

      var contentType = response.Content.Headers.ContentType;

      if ( contentType.MediaType == "image/svg+xml" ) {
        var svgPath = Path.ChangeExtension( iconPath, ".svg" );

        if ( File.Exists( svgPath ) ) {
          File.Delete( svgPath );
        }

        using ( var file = File.OpenWrite( svgPath ) ) {
          var stream = await response.Content.ReadAsStreamAsync();
          await stream.CopyToAsync( file );
        }

        // Convert SVG to PNG
        var svgDocument = SvgDocument.Open( svgPath );
        svgDocument.ShapeRendering = SvgShapeRendering.Auto;

        var bmp = svgDocument.Draw( 48, 48 );
        bmp.Save( iconPath, ImageFormat.Png );
      }
      else {
        using ( var file = File.OpenWrite( iconPath ) ) {
          var stream = await response.Content.ReadAsStreamAsync();
          await stream.CopyToAsync( file );
        }
      }
    }

    private async Task<ICollection<ProjectVersion>> GetVersions( string projectId ) {
      var url = _uriBuilder.GenerateGetVersionsUrl( projectId );
      var response = await _httpClient.GetStringAsync( url );
      var json = JArray.Parse( response );

      var projects = new List<ProjectVersion>();

      foreach ( var item in json ) {
        var id = item.Value<string>( "id" );
        var name = item.Value<string>( "name" );
        var description = item.Value<string>( "description" );
        var isReleased = item.Value<bool>( "released" );
        var releaseDate = item.Value<DateTime>( "releaseDate" );

        projects.Add( new ProjectVersion( id, name, description, isReleased, releaseDate ) );
      }

      projects.Sort( ( v1, v2 ) => v2.ReleaseDate.CompareTo( v1.ReleaseDate ) );

      return projects;
    }

    private async Task<string> GetVersionSummary() {
      var url = _uriBuilder.GenerateUnresolvedIssueCountUrl( SelectedVersion );

      var response = await _httpClient.GetStringAsync( url );
      var json = JObject.Parse( response );

      var issueCount = json.Value<int>( "issuesCount" );
      var todoIssueCount = json.Value<int>( "issuesUnresolvedCount" );

      return $"Issues: {issueCount} total, {todoIssueCount} left to do";
    }

    private bool CanConnect() {
      return !string.IsNullOrWhiteSpace( Email ) && !string.IsNullOrEmpty( ApiToken );
    }

    private bool HasSelectedVersion() {
      return !string.IsNullOrEmpty( SelectedVersion );
    }

    private void CopyText( string text ) {
      Clipboard.SetText( text, TextDataFormat.Text );
      Message = "Copied";
    }

    private bool CanCopyText( string text ) {
      return !string.IsNullOrEmpty( text );
    }
  }
}