namespace Final_Project.Services;

/// <summary>
/// Stores all phrase templates used to rate contributors, organized by contribution tier.
///
/// HOW TIERS WORK:
///   0%           → MEGA BUM
///   below 1%     → bone spurs curse
///   1% – 9.99%   → super bum / call yo uber
///   10% – 29.99% → bum ahh insult (the original low-tier roast)
///   30% – 69.99% → "did alr ig" (the original mid-tier praise)
///   70% and up   → goodest boy / girl / pal (inclusive top praise)
///
/// Each tier can hold multiple phrase variants so the output doesn't feel repetitive.
/// Phrases use {name}, {percent}, and {project} as placeholders that get filled in
/// by BuildPhrase() before being sent to the AI for enhancement.
/// </summary>
public sealed class PhraseDatabase
{
    // ──────────────────────────────────────────────
    // TIER 1 — Exactly 0 % contribution
    // Verdict: MEGA BUM
    // ──────────────────────────────────────────────

    /// <summary>
    /// Phrases for contributors who did absolutely nothing — 0% contribution.
    /// The verdict is always "MEGA BUM", exactly as specified.
    /// </summary>
    public IReadOnlyList<string> MegaBumPhrases { get; }

    // ──────────────────────────────────────────────
    // TIER 2 — Greater than 0% but less than 1%
    // Verdict: bone spurs curse
    // ──────────────────────────────────────────────

    /// <summary>
    /// Phrases for contributors who technically did something, but barely.
    /// Range: > 0% and &lt; 1%.
    /// The verdict always ends with the exact bone-spurs line.
    /// </summary>
    public IReadOnlyList<string> BelowOnePercentPhrases { get; }

    // ──────────────────────────────────────────────
    // TIER 3 — 1% to 9.99%
    // Verdict: super bum, call yo fricking uber
    // ──────────────────────────────────────────────

    /// <summary>
    /// Phrases for contributors who squeezed out between 1% and 9.99%.
    /// The verdict always ends with the exact uber line.
    /// </summary>
    public IReadOnlyList<string> SuperBumPhrases { get; }

    // ──────────────────────────────────────────────
    // TIER 4 — 10% to 29.99%
    // Verdict: bum ahh insult (original low-tier roast from the codebase)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Phrases for contributors in the 10%–29.99% range.
    /// Uses the original "bum ahh" insult format from the codebase, with extras added.
    /// </summary>
    public IReadOnlyList<string> LowContributionPhrases { get; }

    // ──────────────────────────────────────────────
    // TIER 5 — 30% to 69.99%
    // Verdict: "did alr ig" (original mid-tier praise from the codebase)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Phrases for contributors in the 30%–69.99% range.
    /// Uses the original "did alr ig" format from the codebase, with extras added.
    /// </summary>
    public IReadOnlyList<string> MidContributionPhrases { get; }

    // ──────────────────────────────────────────────
    // TIER 6 — 70% and above
    // Verdict: goodest boy / girl / pal (inclusive, gender-varied)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Phrases for top contributors at 70% or above.
    /// Rotates between "goodest boy", "goodest girl", and "goodest pal"
    /// so we're inclusive round here.
    /// </summary>
    public IReadOnlyList<string> HighContributionPhrases { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PhraseDatabase"/> and populates
    /// all phrase lists with their templates.
    ///
    /// Placeholder tokens used in templates:
    ///   {name}    → replaced with the contributor's GitHub login
    ///   {percent} → replaced with their contribution percentage (e.g. "4.20")
    ///   {project} → replaced with the repository name
    /// </summary>
    public PhraseDatabase()
    {
        // ── MEGA BUM (0%) ───────────────────────────────────────────────────
        // The phrase "MEGA BUM" must be present and prominent for 0% contributors.
        MegaBumPhrases =
        [
            "MEGA BUM. {name} contributed 0% to {project}. Zero. Zilch. Nothing. MEGA BUM.",
            "{name} on {project}: MEGA BUM. The repo doesn't know your name. The commits don't know your name. MEGA BUM.",
            "MEGA BUM alert — {name} watched {project} get built and decided their hands were too precious to type. MEGA BUM."
        ];

        // ── BONE SPURS CURSE (> 0% and < 1%) ───────────────────────────────
        // Exact required ending: "do not come to my town. i've cursed you with bone spurs"
        BelowOnePercentPhrases =
        [
            "{name} somehow only scraped {percent}% on {project}. Do not come to my town. I've cursed you with bone spurs.",
            "{percent}% from {name} on {project}. That is not a number. That is an insult. Do not come to my town. I've cursed you with bone spurs.",
            "{name} showed up to {project} and contributed {percent}%. Do not come to my town. I've cursed you with bone spurs."
        ];

        // ── SUPER BUM / CALL YO UBER (1% – 9.99%) ──────────────────────────
        // Exact required ending: "you a super bum, call yo fricking uber"
        SuperBumPhrases =
        [
            "{name} put in {percent}% on {project}. You a super bum, call yo fricking uber.",
            "{percent}% — that's what {name} brought to {project}. You a super bum, call yo fricking uber.",
            "{name} technically participated in {project} with {percent}%. You a super bum, call yo fricking uber."
        ];

        // ── BUM AHH INSULT (10% – 29.99%) ──────────────────────────────────
        // Keeps the original phrase format from the codebase: "{name}'s bum ahh only did {percent}%..."
        LowContributionPhrases =
        [
            "{name}'s bum ahh only did {percent}% of this {project} project.",
            "{name}'s bum ahh contribution of {percent}% to {project} is noted and judged.",
            "Bum ahh energy detected: {name} clocked {percent}% on {project} and called it a day."
        ];

        // ── DID ALR IG (30% – 69.99%) ──────────────────────────────────────
        // Keeps the original "did alr ig" phrasing from the codebase
        MidContributionPhrases =
        [
            "{name} did {percent}% on {project}. Did alr ig.",
            "{name} pulled {percent}% on {project}. Not bad, not great. Did alr ig.",
            "{percent}% from {name} on {project}. We see you. Did alr ig."
        ];

        // ── GOODEST BOY / GIRL / PAL (70%+) ────────────────────────────────
        // Rotates gender-inclusive variations of "you're the goodest boy"
        // so everyone feels represented
        HighContributionPhrases =
        [
            "You're the goodest boy, {name}! {percent}% on {project}. Absolute legend.",
            "You're the goodest boy, {name}! {percent}% — you basically ARE {project}.",
            "You're the goodest pal, {name}! {percent}% on {project}. We are not worthy.",
            "Goodest boy energy: {name} carried {project} with {percent}%. Someone get them a treat.",
            "Goodest girl status confirmed: {name} did {percent}% on {project}. Icon.",
            "You're the goodest one, {name}. {percent}% on {project}. The repo thanks you."
        ];
    }

    /// <summary>
    /// Picks the correct phrase tier based on the contributor's percentage,
    /// then randomly selects one phrase from that tier and fills in
    /// the {name}, {percent}, and {project} placeholders.
    ///
    /// Tier breakdown:
    ///   percent == 0              → MEGA BUM
    ///   0 &lt; percent &lt; 1     → bone spurs curse
    ///   1 ≤ percent &lt; 10      → super bum / call yo uber
    ///   10 ≤ percent &lt; 30     → bum ahh insult
    ///   30 ≤ percent &lt; 70     → did alr ig
    ///   percent ≥ 70             → goodest boy/girl/pal/idk
    /// </summary>
    /// <param name="name">The contributor's GitHub login name.</param>
    /// <param name="percent">Their contribution percentage (0–100).</param>
    /// <param name="project">The repository name.</param>
    /// <returns>
    /// A fully formatted phrase string, ready to be passed to the AI for enhancement.
    /// </returns>
    public string BuildPhrase(string name, decimal percent, string project)
    {
        // Pick the right phrase pool based on which tier this percentage falls into
        IReadOnlyList<string> pool = percent switch
        {
            // Exactly zero — the worst category
            0m => MegaBumPhrases,

            // Technically did something, but barely (exclusive 0 to exclusive 1)
            > 0m and < 1m => BelowOnePercentPhrases,

            // Low but at least hit 1%
            >= 1m and < 10m => SuperBumPhrases,

            // Did something but still in bum territory
            >= 10m and < 30m => LowContributionPhrases,

            // Middle of the road — not bad, not great
            >= 30m and < 70m => MidContributionPhrases,

            // Top tier — 70% and above
            _ => HighContributionPhrases
        };

        // Randomly pick one phrase from the chosen pool so results vary between runs
        string template = pool[Random.Shared.Next(pool.Count)];

        // Replace all placeholder tokens with the actual values
        return template
            .Replace("{name}", name)
            .Replace("{percent}", percent.ToString("0.00"))
            .Replace("{project}", project);
    }
}