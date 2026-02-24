#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Text;
using OpennessCopy.Services.CodeBuilders.SimaticSd;

namespace OpennessCopy.Services.CodeBuilders;

public sealed class EmbeddedTemplateRepository : INetworkTemplateRepository
{
    private readonly Assembly _assembly;
    private readonly string _rootNamespace;
    private readonly string _defaultExtension;

    public EmbeddedTemplateRepository(string rootNamespace, string defaultExtension = ".txt", Assembly? assembly = null)
    {
        if (string.IsNullOrWhiteSpace(rootNamespace))
        {
            throw new ArgumentException("Root namespace must be provided.", nameof(rootNamespace));
        }

        _rootNamespace = rootNamespace.Trim('.'); // ensure clean concatenation
        _defaultExtension = NormalizeExtension(defaultExtension);
        _assembly = assembly ?? Assembly.GetExecutingAssembly();
    }

    public string Load(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Template key must be provided.", nameof(key));
        }

        var normalizedKey = key
            .Replace('\\', '.')
            .Replace('/', '.')
            .Trim('.');

        var resourceName = $"{_rootNamespace}.{normalizedKey}";
        if (!resourceName.EndsWith(_defaultExtension, StringComparison.OrdinalIgnoreCase))
        {
            resourceName += _defaultExtension;
        }

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded template '{resourceName}' not found.", resourceName);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".txt";
        }

        return extension.StartsWith(".") ? extension : "." + extension;
    }
}
