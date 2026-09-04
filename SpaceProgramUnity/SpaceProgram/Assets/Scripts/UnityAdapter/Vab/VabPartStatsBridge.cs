/// <summary>
/// Bridges <see cref="PartDatabase"/> templates to Core VAB physics stats.
/// </summary>
public static class VabPartStatsBridge
{
    public static bool TryLookup(string partTemplateId, out VabPartPhysicsStats stats)
    {
        stats = default;

        if (string.IsNullOrEmpty(partTemplateId))
        {
            return false;
        }

        PartTemplate template = PartDatabase.GetById(partTemplateId);

        if (template == null)
        {
            return false;
        }

        stats = VabBlueprintExporter.StatsFromPartTemplate(template);
        return true;
    }
}
