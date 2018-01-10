namespace JiraReleaseNoteExtractor.Models {
  internal class Issue {
    public string Id { get; }
    public string Key { get; }
    public string IssueType { get; }
    public string Description { get; }
    public string ReleaseNote { get; }

    public Issue( string id, string key, string issueType, string description, string releaseNote ) {
      Id = id;
      Key = key;
      IssueType = issueType;
      Description = description;
      ReleaseNote = releaseNote;
    }
  }
}
