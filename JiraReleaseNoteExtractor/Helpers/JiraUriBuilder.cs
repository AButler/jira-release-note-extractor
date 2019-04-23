using System;

namespace JiraReleaseNoteExtractor.Helpers {
  internal class JiraUriBuilder {
    private readonly string _baseUrl;

    public JiraUriBuilder( string baseUrl ) {
      _baseUrl = baseUrl;
    }

    public string GenerateIssueUrl( string issueKey ) {
      return $"{_baseUrl}/browse/{issueKey}";
    }

    public string GenerateViewIssuesUrl( string query ) {
      return $"{_baseUrl}/issues/?jql={Uri.EscapeDataString( query )}";
    }

    public string GenerateViewEpicsUrl( string query ) {
      return $"{_baseUrl}/issues/?jql={Uri.EscapeDataString( query )}";
    }

    public string GenerateReleaseNotesQueryUrl( string query ) {
      return $"{_baseUrl}/rest/api/2/search?jql={Uri.EscapeDataString( query )}";
    }
    
    public string GenerateEpicQueryUrl( string query ) {
      return $"{_baseUrl}/rest/api/2/search?jql={Uri.EscapeDataString( query )}";
    }
    
    public string GenerateFieldInfoUrl() {
      return $"{_baseUrl}/rest/api/2/field";
    }

    public string GenerateGetProjectsUrl() {
      return $"{_baseUrl}/rest/api/2/project";
    }

    public string GenerateGetVersionsUrl( string projectId ) {
      return $"{_baseUrl}/rest/api/2/project/{projectId}/versions";
    }

    public string GenerateUnresolvedIssueCountUrl( string versionId ) {
      return $"{_baseUrl}/rest/api/2/version/{versionId}/unresolvedIssueCount";
    }
  }
}
