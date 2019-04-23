namespace JiraReleaseNoteExtractor.Models {
  internal class Project {
    public string Id { get; }
    public string Key { get; }
    public string Name { get; }
    public string Icon { get; }

    public Project( string id, string key, string name, string icon ) {
      Id = id;
      Key = key;
      Name = name;
      Icon = icon;
    }
  }
}
