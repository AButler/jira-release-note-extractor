using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using JiraReleaseNoteExtractor.Models;
using JiraReleaseNoteExtractor.Properties;
using Newtonsoft.Json.Linq;

namespace JiraReleaseNoteExtractor.ViewModels {
  internal class MainWindowViewModel: ViewModelBase {
    private readonly HttpClient _httpClient;
    private readonly string _jiraBaseUrl;
    private readonly string _releaseNoteFieldName;
    private string _releaseNoteFieldId;

    private bool _isBusy;
    private string _username;
    private string _password;
    private bool _isConnected;
    private ICollection<Project> _projects;
    private string _selectedProject;
    private ICollection<ProjectVersion> _versions;
    private string _selectedVersion;
    private string _versionSummary;
    private string _resultsText;

    public bool IsBusy {
      get => _isBusy;
      set => Set( ref _isBusy, value );
    }

    public string Username {
      get => _username;
      set => Set( ref _username, value );
    }

    public string Password {
      get => _password;
      set => Set( ref _password, value );
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

    public string ResultsText {
      get => _resultsText;
      set => Set( ref _resultsText, value );
    }

    public RelayCommand ConnectCommand { get; }
    public RelayCommand ViewIssuesCommand { get; }

    public string Version { get; }

    public MainWindowViewModel() {
      Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

      ConnectCommand = new RelayCommand( Connect, CanConnect );
      ViewIssuesCommand = new RelayCommand( ViewIssues, CanViewIssues );

      _jiraBaseUrl = ConfigurationManager.AppSettings["JiraBaseUrl"];
      _releaseNoteFieldName = ConfigurationManager.AppSettings["ReleaseNoteFieldName"];

      Username = Settings.Default.LastUsername;

      _httpClient = new HttpClient();
      _httpClient.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );

      PropertyChanged += OnPropertyChanged;
    }

    private async void OnPropertyChanged( object sender, PropertyChangedEventArgs e ) {
      switch ( e.PropertyName ) {
        case nameof( SelectedProject ):
          await OnSelectedProjectChanged();
          break;
        case nameof( SelectedVersion ):
          await OnSelectedVersionChanged();
          break;
      }
    }

    private async Task OnSelectedProjectChanged() {
      Settings.Default.LastProject = SelectedProject;
      Settings.Default.Save();

      IsBusy = true;

      Versions = await GetVersions( SelectedProject );

      if ( Versions.Any( p => string.Equals( p.Id, Settings.Default.LastVersion, StringComparison.InvariantCultureIgnoreCase ) ) ) {
        SelectedVersion = Settings.Default.LastVersion;
      }

      IsBusy = false;
    }

    private async Task OnSelectedVersionChanged() {
      Settings.Default.LastVersion = SelectedVersion;
      Settings.Default.Save();

      IsBusy = true;

      ResultsText = null;
      VersionSummary = await GetVersionSummary( SelectedVersion );
      ResultsText = await GenerateReleaseNotes();

      ViewIssuesCommand.RaiseCanExecuteChanged();

      IsBusy = false;
    }

    private async void Connect() {
      Settings.Default.LastUsername = Username;
      Settings.Default.Save();

      _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Basic", Convert.ToBase64String( Encoding.ASCII.GetBytes( $"{Username}:{Password}" ) ) );

      IsBusy = true;

      _releaseNoteFieldId = await GetReleaseNoteFieldId();
      Projects = await GetProjects();

      if ( Projects.Any( p => string.Equals( p.Id, Settings.Default.LastProject, StringComparison.InvariantCultureIgnoreCase ) ) ) {
        SelectedProject = Settings.Default.LastProject;
      }

      IsConnected = true;
      IsBusy = false;
    }

    private async Task<string> GenerateReleaseNotes() {
      var query = GetJiraJql();
      var url = $"{_jiraBaseUrl}/rest/api/2/search?jql={Uri.EscapeDataString( query )}";
      var response = await _httpClient.GetStringAsync( url );
      var json = JObject.Parse( response );

      var resultsText = new StringBuilder( "### Resolved Issues\r\n\r\n" );

      foreach ( var issueJson in json["issues"] ) {
        var id = issueJson.Value<string>( "id" );
        var key = issueJson.Value<string>( "key" );
        var releaseNote = issueJson["fields"].Value<string>( _releaseNoteFieldId );

        if ( string.IsNullOrWhiteSpace( releaseNote ) ) {
          releaseNote = $"!!! NO RELEASE NOTE: {_jiraBaseUrl}/browse/{key} !!!";
        }

        resultsText.AppendLine( $"  * {releaseNote} [{key}]" );
      }

      return resultsText.ToString();
    }

    private string GetJiraJql() {
      return $"fixVersion = {SelectedVersion} AND ( affectedVersion is empty or affectedVersion != {SelectedVersion} ) and status = Closed and resolution = Fixed ORDER BY key ASC";
    }

    private void ViewIssues() {
      var query = GetJiraJql();
      var url = $"{_jiraBaseUrl}/issues/?jql={Uri.EscapeDataString( query )}";

      Process.Start( new ProcessStartInfo( url ) { UseShellExecute =  true } );
    }

    private async Task<string> GetReleaseNoteFieldId() {
      var url = $"{_jiraBaseUrl}/rest/api/2/field";
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
      var url = $"{_jiraBaseUrl}/rest/api/2/project";
      var response = await _httpClient.GetStringAsync( url );
      var json = JArray.Parse( response );

      var projects = new List<Project>();

      foreach ( var item in json ) {
        var id = item.Value<string>( "id" );
        var key = item.Value<string>( "key" );
        var name = item.Value<string>( "name" );

        projects.Add( new Project( id, key, name ) );
      }

      return projects;
    }

    private async Task<ICollection<ProjectVersion>> GetVersions( string projectId ) {
      var url = $"{_jiraBaseUrl}/rest/api/2/project/{projectId}/versions";
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

    private async Task<string> GetVersionSummary( string versionId ) {
      var url = $"{_jiraBaseUrl}/rest/api/2/version/{versionId}/unresolvedIssueCount";

      var response = await _httpClient.GetStringAsync( url );
      var json = JObject.Parse( response );

      var issueCount = json.Value<int>( "issuesCount" );
      var todoIssueCount = json.Value<int>( "issuesUnresolvedCount" );

      return $"Issues: {issueCount} total, {todoIssueCount} left to do";
    }

    private bool CanConnect() {
      return !string.IsNullOrWhiteSpace( Username ) && !string.IsNullOrEmpty( Password );
    }

    private bool CanViewIssues() {
      return !string.IsNullOrEmpty( SelectedVersion );
    }
  }
}
