namespace JiraReleaseNoteExtractor.Models {
  internal class Project {
    public string Id { get; }
    public string Key { get; }
    public string Name { get; }

    public Project( string id, string key, string name ) {
      Id = id;
      Key = key;
      Name = name;
    }
  }
}
