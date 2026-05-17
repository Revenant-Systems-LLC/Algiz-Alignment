using System;
using System.Collections.Generic;

namespace SageRage.Prompting
{
    public sealed record ContextItem(
        string Title,
        string? SourceId,
        string Snippet,
        string? Url = null,
        DateTimeOffset? Timestamp = null);

    public sealed record PromptPackage(
        string SystemInstruction,
        string UserMessage,
        IReadOnlyList<ContextItem> Context);
}
