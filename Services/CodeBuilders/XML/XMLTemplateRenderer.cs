#nullable enable

using System;
using System.Collections.Generic;
using OpennessCopy.Services.CodeBuilders.SimaticSd;

namespace OpennessCopy.Services.CodeBuilders.XML;

public sealed class XMLTemplateRenderer(INetworkTemplateRepository repository)
{
    private readonly INetworkTemplateRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public string RenderBlock(
        string templateKey,
        string blockName,
        string blockNumber,
        IReadOnlyDictionary<string, string>? additionalTokens = null)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            throw new ArgumentException("Template key must be provided.", nameof(templateKey));
        }

        if (string.IsNullOrWhiteSpace(blockName))
        {
            throw new ArgumentException("Block name must be provided.", nameof(blockName));
        }

        if (string.IsNullOrWhiteSpace(blockNumber))
        {
            throw new ArgumentException("Block number must be provided.", nameof(blockNumber));
        }

        var template = _repository.Load(templateKey);

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BlockName"] = blockName,
            ["BlockNumber"] = blockNumber,
        };

        if (additionalTokens != null)
        {
            foreach (var kvp in additionalTokens)
            {
                tokens[kvp.Key] = kvp.Value;
            }
        }

        return TemplateRenderer.Render(template, tokens);
    }
}
