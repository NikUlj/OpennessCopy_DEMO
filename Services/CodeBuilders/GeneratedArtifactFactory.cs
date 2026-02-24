#nullable enable

using System.Collections.Generic;
using OpennessCopy.Services.CodeBuilders.SimaticSd;

namespace OpennessCopy.Services.CodeBuilders;

public static class GeneratedArtifactFactory
{
    public static IReadOnlyList<GeneratedArtifact> CreateBlockArtifacts(
        string basePath,
        string name,
        string codeContent,
        string textContent)
    {
        return CreatePair(
            basePath,
            name,
            codeContent,
            SimaticSdArtifactKinds.BlockCode,
            textContent,
            SimaticSdArtifactKinds.BlockText);
    }

    public static IReadOnlyList<GeneratedArtifact> CreateUdtArtifacts(
        string basePath,
        string name,
        string codeContent,
        string textContent)
    {
        return CreatePair(
            basePath,
            name,
            codeContent,
            SimaticSdArtifactKinds.UdtCode,
            textContent,
            SimaticSdArtifactKinds.UdtText);
    }

    private static IReadOnlyList<GeneratedArtifact> CreatePair(
        string basePath,
        string name,
        string codeContent,
        string codeKind,
        string textContent,
        string textKind)
    {
        var normalizedBase = basePath.TrimEnd('/', '\\');
        var artifactPath = $"{normalizedBase}/{name}";

        return
        [
            new GeneratedArtifact(
                relativePath: $"{artifactPath}/{name}.s7dcl",
                kind: codeKind,
                content: codeContent),
            new GeneratedArtifact(
                relativePath: $"{artifactPath}/{name}.s7res",
                kind: textKind,
                content: textContent)
        ];
    }
}
