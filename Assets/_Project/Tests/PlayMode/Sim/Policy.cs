namespace GrandmasHouse.Tests.Sim
{
    // How a scripted player keeps the homestead: the chance they skip each
    // chore on a given day, and which of the kitchen's ways of keeping food
    // they bother with.
    public class Policy
    {
        public string Name;
        // Per bed, per day.
        public float SkipWatering;
        // Weeds, hornworms and suckers, per bed, per day.
        public float SkipTending;
        // Pulls the frost cloth over when the radio calls a frost, and back
        // off in the morning.
        public float CoversFrost;
        public float ForgetsUncover;
        // Leaves the coop door open, or a hen out, at night.
        public float ForgetsDoor;
        public float SkipHens;
        // Puts up jars of what won't keep, and cabbage in the crock.
        public float CansOnADay;
        // Chance a jar goes in with the headspace wrong.
        public float BadHeadspace;
        // Cooks soup rather than eating vegetables raw.
        public bool Cooks;
        // Root crops to the cellar, the rest in the fridge.
        public bool KeepsCold;
        // Days between trips to the store and the neighbors.
        public int TripEvery;
        // Buys logs and splits them for winter.
        public bool LaysInWood;

        public static readonly Policy Diligent = new Policy
        {
            Name = "diligent", SkipWatering = 0.03f, SkipTending = 0.05f, CoversFrost = 0.95f, ForgetsUncover = 0.02f,
            ForgetsDoor = 0.02f, SkipHens = 0.03f, CansOnADay = 0.9f, BadHeadspace = 0.05f, Cooks = true, KeepsCold = true,
            TripEvery = 2, LaysInWood = true,
        };

        public static readonly Policy Sloppy = new Policy
        {
            Name = "sloppy", SkipWatering = 0.2f, SkipTending = 0.4f, CoversFrost = 0.5f, ForgetsUncover = 0.2f,
            ForgetsDoor = 0.1f, SkipHens = 0.15f, CansOnADay = 0.5f, BadHeadspace = 0.3f, Cooks = true, KeepsCold = false,
            TripEvery = 4, LaysInWood = true,
        };

        public static readonly Policy Lazy = new Policy
        {
            Name = "lazy", SkipWatering = 0.4f, SkipTending = 0.8f, CoversFrost = 0f, ForgetsUncover = 0f,
            ForgetsDoor = 0.25f, SkipHens = 0.3f, CansOnADay = 0f, BadHeadspace = 0.5f, Cooks = false, KeepsCold = false,
            TripEvery = 7, LaysInWood = false,
        };

        public static readonly Policy[] All = { Diligent, Sloppy, Lazy };
    }
}
