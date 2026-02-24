namespace OpennessCopy.Services.CodeBuilders;

public sealed class GeneratedArtifact(string relativePath, string kind, string content)
{
    public string RelativePath { get; } = relativePath;
    public string Kind { get; } = kind;
    public string Content { get; } = content;
}