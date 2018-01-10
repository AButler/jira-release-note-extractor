using System;

namespace JiraReleaseNoteExtractor.Models {
  internal class ProjectVersion {
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsReleased { get; }
    public DateTime ReleaseDate { get; }

    public ProjectVersion( string id, string name, string description, bool isReleased, DateTime releaseDate ) {
      Id = id;
      Name = name;
      Description = description;
      IsReleased = isReleased;
      ReleaseDate = releaseDate;
    }
  }
}
