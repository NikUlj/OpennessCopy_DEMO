using System;
using System.IO;
using System.Text;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.SimaticSd
{
    public sealed class FileNetworkTemplateRepository : INetworkTemplateRepository
    {
        private readonly string _rootDirectory;
        private readonly string _defaultExtension;

        public FileNetworkTemplateRepository(string rootDirectory, string defaultExtension = ".txt")
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
            }

            _rootDirectory = Path.GetFullPath(rootDirectory);
            _defaultExtension = defaultExtension.StartsWith(".")
                ? defaultExtension
                : "." + defaultExtension;
        }

        public string Load(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Template key must be provided.", nameof(key));
            }

            var normalized = key
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            var path = Path.Combine(_rootDirectory, normalized);
            if (!Path.HasExtension(path))
            {
                path += _defaultExtension;
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Network template '{key}' not found.", path);
            }

            return File.ReadAllText(path, Encoding.UTF8);
        }
    }
}
