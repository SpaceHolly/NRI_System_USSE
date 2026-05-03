using System;
using System.Collections.Generic;

namespace Nri.Server.Content;

public sealed class ContentLoadReport
{
    public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public int FilesFound { get; set; }
    public int FilesRead { get; set; }
    public int ErrorCount { get; set; }
    public Dictionary<string, int> RecordsByCategory { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; set; } = new List<string>();
}
