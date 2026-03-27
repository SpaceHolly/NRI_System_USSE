using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Server.ContentPacks;

public interface IContentPackReader
{
    ContentPackManifest ReadManifest(string path);
    IReadOnlyCollection<ClassDefinition> ReadClassBranchPack(string path);
    IReadOnlyCollection<SkillDefinition> ReadUnclassedSkillsPack(string path);
}

public interface IContentPackWriter
{
    void WriteManifest(string path, ContentPackManifest manifest);
    void WriteClassBranchPack(string path, IReadOnlyCollection<ClassDefinition> definitions);
    void WriteUnclassedSkillsPack(string path, IReadOnlyCollection<SkillDefinition> definitions);
}

public sealed class ContentPackManifest
{
    public string PackCode { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public List<string> BranchPacks { get; set; } = new List<string>();
    public List<string> UnclassedSkillPacks { get; set; } = new List<string>();
}
