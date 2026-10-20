namespace GrandmasHouse.Audio
{
    // Every sound the game makes. Each has a placeholder made in code until a
    // real recording is added to the sound bank.
    public enum SoundId
    {
        // One-shots
        LidPing,
        LidClink,
        PotLid,
        KnifeChop,
        SliceDrop,
        DillRustle,
        SaltShake,
        KnobClick,
        SetDown,
        PickUp,
        WeedPull,
        SoilPat,
        StepWood,
        StepGrass,
        Paper,
        MenuClick,
        RadioStatic,

        // Loops
        Pour,
        Boil,
        Simmer,

        // One-shots added for survival, kept last so saved numbers don't shift.
        Eat,
        StomachGrowl,
    }
}
