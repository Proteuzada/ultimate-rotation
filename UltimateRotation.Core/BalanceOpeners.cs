namespace UltimateRotation;

/// <summary>
/// The Balance standard opener for the selected job, in the order it is pressed.
/// One part, so a later press stays inside the opener.
/// </summary>
public static class BalanceOpeners
{
    public const string PhaseName = "Opener";
    private const double GcdMs = 2_500;
    private const double WeaveMs = 600;

    public static IReadOnlyList<GuideStep>? For(string? spec, bool includeOffGcds = true, bool includePotion = true)
    {
        var job = JobCatalog.Find(spec);
        if (job is null || !BySpec.TryGetValue(job.SpecName, out var presses))
            return null;

        var kept = new List<Press>(presses.Length);
        foreach (var press in presses)
        {
            if (!includeOffGcds && press.Kind == StepKind.OffGcd)
                continue;
            if (!includePotion && Gemdraughts.IsStep(press.Name))
                continue;
            kept.Add(press);
        }

        return Time(kept);
    }

    private static IReadOnlyList<GuideStep> Time(IReadOnlyList<Press> presses)
    {
        var offsets = new double[presses.Count];
        var pullStart = 0;
        while (pullStart < presses.Count && presses[pullStart].Prepull)
            pullStart++;

        Place(presses, offsets, 0, pullStart, 0);
        if (pullStart > 0)
        {
            var shift = offsets[pullStart - 1] + 400;
            for (var i = 0; i < pullStart; i++)
                offsets[i] -= shift;
        }

        Place(presses, offsets, pullStart, presses.Count, 0);
        for (var i = 1; i < offsets.Length; i++)
        {
            if (offsets[i] <= offsets[i - 1])
                offsets[i] = offsets[i - 1] + 40;
        }

        var steps = new List<GuideStep>(presses.Count);
        for (var i = 0; i < presses.Count; i++)
        {
            var press = presses[i];
            steps.Add(new GuideStep(i, press.ActionId, press.Name, press.Kind, 1, PhaseName, offsets[i]));
        }

        return steps;
    }

    private static void Place(IReadOnlyList<Press> presses, double[] offsets, int start, int end, double firstGcd)
    {
        var nextGcd = firstGcd;
        var cursor = firstGcd;
        var seenGcd = false;
        var weave = 0;
        for (var i = start; i < end; i++)
        {
            if (presses[i].Kind == StepKind.Gcd)
            {
                offsets[i] = nextGcd;
                cursor = nextGcd;
                nextGcd += GcdMs;
                weave = 0;
                seenGcd = true;
                continue;
            }

            if (!seenGcd)
            {
                offsets[i] = nextGcd;
                cursor = nextGcd;
                nextGcd += WeaveMs;
                continue;
            }

            weave++;
            var at = cursor + WeaveMs * weave;
            if (at >= nextGcd)
                at = nextGcd - 40;
            offsets[i] = at;
        }
    }

    private static Press G(uint actionId, string name, bool prepull = false)
        => new(name, actionId, StepKind.Gcd, prepull);

    private static Press A(uint actionId, string name, bool prepull = false)
        => new(name, actionId, StepKind.OffGcd, prepull);

    private static Press Potion(string stat, bool prepull = false)
        => new($"Gemdraught of {stat}", 0, StepKind.Other, prepull);

    private static readonly Dictionary<string, Press[]> BySpec = new()
    {
        ["Paladin"] =
        [
            G(7384, "Holy Spirit", prepull: true),
            G(9, "Fast Blade"),
            G(15, "Riot Blade"),
            Potion("Strength"),
            G(3539, "Royal Authority"),
            A(20, "Fight or Flight"),
            A(36921, "Imperator"),
            G(16459, "Confiteor"),
            A(23, "Circle of Scorn"),
            A(25747, "Expiacion"),
            G(25748, "Blade of Faith"),
            A(16461, "Intervene"),
            G(25749, "Blade of Truth"),
            A(16461, "Intervene"),
            G(25750, "Blade of Valor"),
            A(36922, "Blade of Honor"),
            G(3538, "Goring Blade"),
            G(16460, "Atonement"),
            G(36918, "Supplication"),
            G(36919, "Sepulchre"),
            G(7384, "Holy Spirit"),
        ],
        ["Warrior"] =
        [
            G(46, "Tomahawk", prepull: true),
            G(31, "Heavy Swing"),
            A(52, "Infuriate"),
            G(37, "Maim"),
            G(45, "Storm's Eye"),
            A(7389, "Inner Release"),
            Potion("Strength"),
            G(16465, "Inner Chaos"),
            A(7387, "Upheaval"),
            G(3549, "Fell Cleave"),
            A(7386, "Onslaught"),
            G(3549, "Fell Cleave"),
            A(7386, "Onslaught"),
            G(3549, "Fell Cleave"),
            A(7386, "Onslaught"),
            A(36924, "Primal Wrath"),
            G(25753, "Primal Rend"),
            G(36925, "Primal Ruination"),
            G(16465, "Inner Chaos"),
            G(31, "Heavy Swing"),
            G(37, "Maim"),
            G(42, "Storm's Path"),
            G(3549, "Fell Cleave"),
            A(52, "Infuriate"),
            G(16465, "Inner Chaos"),
        ],
        ["DarkKnight"] =
        [
            A(7393, "The Blackest Night", prepull: true),
            Potion("Strength", prepull: true),
            G(3624, "Unmend"),
            G(3617, "Hard Slash"),
            A(16470, "Edge of Shadow"),
            G(3623, "Syphon Strike"),
            A(16472, "Living Shadow"),
            G(3632, "Souleater"),
            G(36932, "Disesteem"),
            A(7390, "Delirium"),
            G(36928, "Scarlet Delirium"),
            A(16470, "Edge of Shadow"),
            A(3639, "Salted Earth"),
            G(36929, "Comeuppance"),
            A(16470, "Edge of Shadow"),
            A(25757, "Shadowbringer"),
            G(36930, "Torcleaver"),
            A(3643, "Carve and Spit"),
            A(25757, "Shadowbringer"),
            G(7392, "Bloodspiller"),
            A(16470, "Edge of Shadow"),
            A(25755, "Salt and Darkness"),
        ],
        ["Gunbreaker"] =
        [
            G(16143, "Lightning Shot", prepull: true),
            A(16164, "Bloodfest"),
            G(16137, "Keen Edge"),
            G(16139, "Brutal Shell"),
            A(16138, "No Mercy"),
            Potion("Strength"),
            G(16146, "Gnashing Fang"),
            A(16156, "Jugular Rip"),
            G(25760, "Double Down"),
            A(16159, "Bow Shock"),
            A(16165, "Blasting Zone"),
            G(16153, "Sonic Break"),
            G(16147, "Savage Claw"),
            A(16157, "Abdomen Tear"),
            G(16150, "Wicked Talon"),
            A(16158, "Eye Gouge"),
            G(36937, "Reign of Beasts"),
            G(36938, "Noble Blood"),
            G(36939, "Lion Heart"),
            G(16145, "Solid Barrel"),
            G(16146, "Gnashing Fang"),
            A(16156, "Jugular Rip"),
        ],
        ["WhiteMage"] =
        [
            G(25859, "Glare III", prepull: true),
            Potion("Mind"),
            G(16532, "Dia"),
            G(25859, "Glare III"),
            G(25859, "Glare III"),
            G(37009, "Glare IV"),
            A(136, "Presence of Mind"),
            G(16535, "Afflatus Misery"),
            A(3571, "Assize"),
            G(37009, "Glare IV"),
            G(37009, "Glare IV"),
            G(25859, "Glare III"),
            G(25859, "Glare III"),
            G(25859, "Glare III"),
            G(25859, "Glare III"),
            G(25859, "Glare III"),
            G(16532, "Dia"),
        ],
        ["Scholar"] =
        [
            G(25865, "Broil IV", prepull: true),
            G(16540, "Biolysis"),
            Potion("Mind"),
            G(25865, "Broil IV"),
            A(3587, "Dissipation"),
            G(25865, "Broil IV"),
            A(7436, "Chain Stratagem"),
            G(25865, "Broil IV"),
            A(167, "Energy Drain"),
            G(25865, "Broil IV"),
            A(167, "Energy Drain"),
            G(25865, "Broil IV"),
            A(167, "Energy Drain"),
            G(25865, "Broil IV"),
            A(37012, "Baneful Impaction"),
            G(25865, "Broil IV"),
            A(167, "Energy Drain"),
            G(25865, "Broil IV"),
            A(167, "Energy Drain"),
            G(25865, "Broil IV"),
            A(167, "Energy Drain"),
            G(16540, "Biolysis"),
        ],
        ["Astrologian"] =
        [
            G(7439, "Earthly Star", prepull: true),
            G(25871, "Fall Malefic", prepull: true),
            G(16554, "Combust III"),
            Potion("Mind"),
            G(25871, "Fall Malefic"),
            A(3606, "Lightspeed"),
            G(25871, "Fall Malefic"),
            G(25871, "Fall Malefic"),
            A(16552, "Divination"),
            A(37023, "the Balance"),
            G(25871, "Fall Malefic"),
            A(7444, "Lord of Crowns"),
            A(37018, "Umbral Draw"),
            G(25871, "Fall Malefic"),
            A(37029, "Oracle"),
            A(37026, "the Spear"),
            G(25871, "Fall Malefic"),
            G(25871, "Fall Malefic"),
            G(25871, "Fall Malefic"),
            G(25871, "Fall Malefic"),
            G(16554, "Combust III"),
            G(25871, "Fall Malefic"),
        ],
        ["Sage"] =
        [
            G(24290, "Eukrasia", prepull: true),
            Potion("Mind", prepull: true),
            G(24316, "Toxikon II", prepull: true),
            G(24314, "Eukrasian Dosis III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
            G(24313, "Phlegma III"),
            A(37033, "Psyche"),
            G(24313, "Phlegma III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
            G(24290, "Eukrasia"),
            G(24314, "Eukrasian Dosis III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
            G(24312, "Dosis III"),
        ],
        ["Monk"] =
        [
            G(74, "Dragon Kick"),
            A(69, "Perfect Balance"),
            G(61, "Twin Snakes"),
            G(66, "Demolish"),
            Potion("Strength"),
            G(36945, "Leaping Opo"),
            A(7396, "Brotherhood"),
            A(7395, "Riddle of Fire"),
            G(25768, "Rising Phoenix"),
            A(3547, "the Forbidden Chakra"),
            A(25766, "Riddle of Wind"),
            G(74, "Dragon Kick"),
            G(36949, "Wind's Reply"),
            G(36950, "Fire's Reply"),
            G(36945, "Leaping Opo"),
            A(69, "Perfect Balance"),
            G(74, "Dragon Kick"),
            G(36945, "Leaping Opo"),
            G(74, "Dragon Kick"),
            G(36948, "Elixir Burst"),
            G(36945, "Leaping Opo"),
        ],
        ["Dragoon"] =
        [
            A(94, "Elusive Jump", prepull: true),
            G(90, "Piercing Talon", prepull: true),
            G(75, "True Thrust"),
            Potion("Strength"),
            G(36955, "Spiral Blow"),
            A(85, "Lance Charge"),
            A(3557, "Battle Litany"),
            G(25772, "Chaotic Spring"),
            A(3555, "Geirskogul"),
            G(3556, "Wheeling Thrust"),
            A(16478, "High Jump"),
            G(36952, "Drakesbane"),
            A(83, "Life Surge"),
            A(96, "Dragonfire Dive"),
            G(16479, "Raiden Thrust"),
            A(7400, "Nastrond"),
            A(16480, "Stardiver"),
            G(36954, "Lance Barrage"),
            A(36956, "Starcross"),
            A(83, "Life Surge"),
            G(25771, "Heavens' Thrust"),
            A(36953, "Rise of the Dragon"),
            A(7399, "Mirage Dive"),
            G(3554, "Fang and Claw"),
            G(36952, "Drakesbane"),
            G(16479, "Raiden Thrust"),
            A(25773, "Wyrmwind Thrust"),
        ],
        ["Ninja"] =
        [
            A(2259, "Ten", prepull: true),
            A(2261, "Chi", prepull: true),
            A(2263, "Jin", prepull: true),
            G(2271, "Suiton", prepull: true),
            G(2240, "Spinning Edge"),
            A(2264, "Kassatsu"),
            G(2242, "Gust Slash"),
            Potion("Dexterity"),
            A(36957, "Dokumori"),
            G(25774, "Phantom Kamaitachi"),
            A(16493, "Bunshin"),
            G(3563, "Armor Crush"),
            G(16492, "Hyosho Ranryu"),
            A(36958, "Kunai's Bane"),
            G(2267, "Raiton"),
            A(3566, "Dream Within a Dream"),
            G(2265, "Fuma Shuriken"),
            A(7403, "Ten Chi Jin"),
            G(2267, "Raiton"),
            G(2271, "Suiton"),
            A(16489, "Meisui"),
            G(25778, "Fleeting Raiju"),
            A(36960, "Zesho Meppo"),
            A(36961, "Tenri Jindo"),
            G(25778, "Fleeting Raiju"),
            A(7402, "Bhavacakra"),
            G(2267, "Raiton"),
            G(25778, "Fleeting Raiju"),
        ],
        ["Samurai"] =
        [
            A(7499, "Meikyo Shisui", prepull: true),
            A(7546, "True North", prepull: true),
            G(7481, "Gekko"),
            Potion("Strength"),
            G(7482, "Kasha"),
            A(16482, "Ikishoten"),
            G(7480, "Yukikaze"),
            G(36966, "Tendo Setsugekka"),
            A(16481, "Hissatsu: Senei"),
            G(36968, "Tendo Kaeshi Setsugekka"),
            A(7499, "Meikyo Shisui"),
            G(7481, "Gekko"),
            A(36964, "Zanshin"),
            G(7489, "Higanbana"),
            A(16487, "Shoha"),
            G(25781, "Ogi Namikiri"),
            G(25782, "Kaeshi: Namikiri"),
            A(7490, "Hissatsu: Shinten"),
            G(7482, "Kasha"),
            A(7492, "Hissatsu: Gyoten"),
            G(7481, "Gekko"),
            G(36963, "Gyofu"),
            G(7480, "Yukikaze"),
            A(7490, "Hissatsu: Shinten"),
            G(36966, "Tendo Setsugekka"),
            G(36968, "Tendo Kaeshi Setsugekka"),
        ],
        ["Reaper"] =
        [
            G(24386, "Harpe", prepull: true),
            G(24378, "Shadow of Death"),
            Potion("Strength"),
            G(24380, "Soul Slice"),
            A(24405, "Arcane Circle"),
            A(24393, "Gluttony"),
            G(36970, "Executioner's Gibbet"),
            G(36971, "Executioner's Gallows"),
            G(24380, "Soul Slice"),
            G(24385, "Plentiful Harvest"),
            A(24394, "Enshroud"),
            G(24395, "Void Reaping"),
            A(36969, "Sacrificium"),
            G(24396, "Cross Reaping"),
            A(24399, "Lemure's Slice"),
            G(24395, "Void Reaping"),
            G(24396, "Cross Reaping"),
            A(24399, "Lemure's Slice"),
            G(24398, "Communio"),
            G(36973, "Perfectio"),
            A(24390, "Unveiled Gibbet"),
            G(24382, "Gibbet"),
            G(24378, "Shadow of Death"),
            G(24373, "Slice"),
        ],
        ["Viper"] =
        [
            A(34646, "Slither", prepull: true),
            G(34606, "Steel Fangs"),
            A(34647, "Serpent's Ire"),
            G(34609, "Swiftskin's Sting"),
            G(34620, "Vicewinder"),
            G(34621, "Hunter's Coil"),
            Potion("Dexterity"),
            A(34636, "Twinfang Bite"),
            A(34637, "Twinblood Bite"),
            G(34622, "Swiftskin's Coil"),
            A(34637, "Twinblood Bite"),
            A(34636, "Twinfang Bite"),
            G(34626, "Reawaken"),
            G(34627, "First Generation"),
            A(34640, "First Legacy"),
            G(34628, "Second Generation"),
            A(34641, "Second Legacy"),
            G(34629, "Third Generation"),
            A(34642, "Third Legacy"),
            G(34630, "Fourth Generation"),
            A(34643, "Fourth Legacy"),
            G(34631, "Ouroboros"),
            G(34633, "Uncoiled Fury"),
            A(34644, "Uncoiled Twinfang"),
            A(34645, "Uncoiled Twinblood"),
            G(34633, "Uncoiled Fury"),
            A(34644, "Uncoiled Twinfang"),
            A(34645, "Uncoiled Twinblood"),
            G(34612, "Hindsting Strike"),
            A(34634, "Death Rattle"),
            G(34620, "Vicewinder"),
            G(34621, "Hunter's Coil"),
            A(34636, "Twinfang Bite"),
            A(34637, "Twinblood Bite"),
            G(34622, "Swiftskin's Coil"),
            A(34637, "Twinblood Bite"),
            A(34636, "Twinfang Bite"),
        ],
        ["Bard"] =
        [
            G(7407, "Stormbite"),
            A(36975, "Heartbreak Shot"),
            G(7406, "Caustic Bite"),
            A(3558, "Empyreal Arrow"),
            A(3559, "the Wanderer's Minuet"),
            G(16495, "Burst Shot"),
            Potion("Dexterity"),
            A(118, "Battle Voice"),
            G(16495, "Burst Shot"),
            A(25785, "Radiant Finale"),
            G(7409, "Refulgent Arrow"),
            A(101, "Raging Strikes"),
            G(36977, "Radiant Encore"),
            A(107, "Barrage"),
            G(36976, "Resonant Arrow"),
            A(3562, "Sidewinder"),
            G(7409, "Refulgent Arrow"),
            A(3558, "Empyreal Arrow"),
            G(16495, "Burst Shot"),
            G(3560, "Iron Jaws"),
            G(16495, "Burst Shot"),
            A(7404, "Pitch Perfect"),
        ],
        ["Machinist"] =
        [
            A(2876, "Reassemble", prepull: true),
            Potion("Dexterity", prepull: true),
            G(16500, "Air Anchor"),
            G(16498, "Drill"),
            A(36979, "Double Check"),
            A(36980, "Checkmate"),
            G(25788, "Chain Saw"),
            A(7414, "Barrel Stabilizer"),
            G(36981, "Excavator"),
            G(16498, "Drill"),
            A(2876, "Reassemble"),
            A(16501, "Automaton Queen"),
            G(36982, "Full Metal Field"),
            A(2878, "Wildfire"),
            G(36978, "Blazing Shot"),
            A(17209, "Hypercharge"),
            G(36978, "Blazing Shot"),
            A(36979, "Double Check"),
            G(36978, "Blazing Shot"),
            A(36980, "Checkmate"),
            G(36978, "Blazing Shot"),
            A(36979, "Double Check"),
            G(36978, "Blazing Shot"),
            A(36980, "Checkmate"),
            G(16498, "Drill"),
            A(36979, "Double Check"),
            A(36980, "Checkmate"),
            G(7411, "Heated Split Shot"),
            A(36979, "Double Check"),
            G(7412, "Heated Slug Shot"),
            G(7413, "Heated Clean Shot"),
        ],
        ["Dancer"] =
        [
            G(15997, "Standard Step", prepull: true),
            G(15999, "Emboite", prepull: true),
            G(15999, "Emboite", prepull: true),
            Potion("Dexterity", prepull: true),
            G(16003, "Standard Finish"),
            G(15998, "Technical Step"),
            G(15999, "Emboite"),
            G(15999, "Emboite"),
            G(15999, "Emboite"),
            G(15999, "Emboite"),
            G(16004, "Technical Finish"),
            A(16011, "Devilment"),
            G(25790, "Tillana"),
            A(16013, "Flourish"),
            G(36985, "Dance of the Dawn"),
            A(25791, "Fan Dance IV"),
            G(36983, "Last Dance"),
            A(16009, "Fan Dance III"),
            G(36984, "Finishing Move"),
            G(16005, "Saber Dance"),
        ],
        ["BlackMage"] =
        [
            G(152, "Fire III", prepull: true),
            G(36986, "High Thunder"),
            A(7561, "Swiftcast"),
            G(3577, "Fire IV"),
            A(25796, "Amplifier"),
            Potion("Intelligence"),
            A(3573, "Ley Lines"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(16507, "Xenoglossy"),
            A(158, "Manafont"),
            G(3577, "Fire IV"),
            G(36989, "Flare Star"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(36986, "High Thunder"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(3577, "Fire IV"),
            G(36989, "Flare Star"),
            G(16505, "Despair"),
            A(7421, "Triplecast"),
            A(149, "Transpose"),
            G(154, "Blizzard III"),
            G(3576, "Blizzard IV"),
            G(25797, "Paradox"),
            A(149, "Transpose"),
            G(25797, "Paradox"),
            G(152, "Fire III"),
        ],
        ["Summoner"] =
        [
            G(3579, "Ruin III", prepull: true),
            G(36992, "Summon Solar Bahamut"),
            Potion("Intelligence"),
            G(36994, "Umbral Impulse"),
            A(25801, "Searing Light"),
            G(36994, "Umbral Impulse"),
            G(36994, "Umbral Impulse"),
            G(36994, "Umbral Impulse"),
            A(16508, "Energy Drain"),
            G(36994, "Umbral Impulse"),
            A(36998, "Enkindle Solar Bahamut"),
            A(36990, "Necrotize"),
            G(36994, "Umbral Impulse"),
            A(36990, "Necrotize"),
            A(36996, "Sunflare"),
            G(25839, "Summon Titan II"),
            A(36991, "Searing Flash"),
            G(25824, "Topaz Rite"),
            A(25836, "Mountain Buster"),
            G(25824, "Topaz Rite"),
            A(25836, "Mountain Buster"),
            G(25824, "Topaz Rite"),
            A(25836, "Mountain Buster"),
            G(25824, "Topaz Rite"),
            A(25836, "Mountain Buster"),
            G(25840, "Summon Garuda II"),
            A(7561, "Swiftcast"),
            G(25837, "Slipstream"),
        ],
        ["RedMage"] =
        [
            G(25856, "Veraero III", prepull: true),
            G(25855, "Verthunder III"),
            A(7561, "Swiftcast"),
            Potion("Intelligence"),
            G(25855, "Verthunder III"),
            A(7517, "Fleche"),
            G(25855, "Verthunder III"),
            A(7518, "Acceleration"),
            G(7527, "Enchanted Riposte"),
            A(7521, "Manafication"),
            A(7520, "Embolden"),
            G(7528, "Enchanted Zwerchhau"),
            A(7519, "Contre Sixte"),
            G(7529, "Enchanted Redoublement"),
            A(16527, "Engagement"),
            A(7506, "Corps-a-corps"),
            G(7526, "Verholy"),
            A(37005, "Vice of Thorns"),
            G(16530, "Scorch"),
            A(16527, "Engagement"),
            A(7506, "Corps-a-corps"),
            G(25858, "Resolution"),
            A(37007, "Prefulgence"),
            G(37006, "Grand Impact"),
            A(7518, "Acceleration"),
            G(7510, "Verfire"),
            G(37006, "Grand Impact"),
            G(25855, "Verthunder III"),
            A(7517, "Fleche"),
            G(25856, "Veraero III"),
            G(7510, "Verfire"),
            G(25855, "Verthunder III"),
            G(7511, "Verstone"),
            G(25856, "Veraero III"),
            A(7561, "Swiftcast"),
            G(25856, "Veraero III"),
            A(7519, "Contre Sixte"),
        ],
        ["Pictomancer"] =
        [
            G(34688, "Rainbow Drip", prepull: true),
            Potion("Intelligence"),
            A(34670, "Pom Muse"),
            A(34674, "Striking Muse"),
            G(34665, "Wing Motif"),
            G(34678, "Hammer Stamp"),
            A(34675, "Starry Muse"),
            G(34653, "Blizzard in Cyan"),
            A(34683, "Subtractive Palette"),
            G(34654, "Stone in Yellow"),
            G(34655, "Thunder in Magenta"),
            G(34663, "Comet in Black"),
            A(34671, "Winged Muse"),
            A(34676, "Mog of the Ages"),
            G(34681, "Star Prism"),
            G(34679, "Hammer Brush"),
            G(34680, "Polishing Hammer"),
            G(34688, "Rainbow Drip"),
            G(34650, "Fire in Red"),
            G(34651, "Aero in Green"),
            A(7561, "Swiftcast"),
        ],
    };

    private readonly record struct Press(string Name, uint ActionId, StepKind Kind, bool Prepull);
}
