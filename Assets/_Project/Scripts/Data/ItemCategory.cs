namespace GrandmasHouse.Data
{
    // Broad kinds of things, used by containers to decide what they hold
    // (a harvest basket takes produce, a jar crate takes jars).
    public enum ItemCategory
    {
        Other,
        Produce,
        Jar,
        Egg,
        Tool,
        Container,
        Seed,
        // Pots and the canner. Added last so saved numbers don't shift.
        Cookware,
        // Pantry staples: vinegar, salt.
        Staple,
    }
}
