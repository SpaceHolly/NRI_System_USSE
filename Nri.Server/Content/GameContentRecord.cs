using System.Collections.Generic;
using System.Text.Json;

namespace Nri.Server.Content;

public sealed class GameContentRecord
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> ExtraFields { get; set; } = new Dictionary<string, JsonElement>();
}
