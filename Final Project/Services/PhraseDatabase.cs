namespace Final_Project.Services;

/// <summary>
/// Stores reusable phrase templates for contribution messaging.
/// </summary>
public sealed class PhraseDatabase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhraseDatabase"/> class.
    /// </summary>
    /// <returns>A constructed <see cref="PhraseDatabase"/> object.</returns>
    public PhraseDatabase()
    {
        LowContributionPhrases =
        [
            "{name}'s bum ahh only did {percent}% of this {project} project."
        ];

        HighContributionPhrases =
        [
            "Lil Smarty Pants {name} did {percent}% of this like a good boy.",
            "{name} is essentially the entire engineering department of {project}. Contribution: {percent}%.",
            "The back doctor called; they are worried about {name} carrying the entire weight of this repo ({percent}%)."
        ];
    }

    /// <summary>
    /// Gets the roast templates for low contributors.
    /// </summary>
    /// <returns>A read-only list of low contribution phrase templates.</returns>
    public IReadOnlyList<string> LowContributionPhrases { get; }

    /// <summary>
    /// Gets the praise templates for high contributors.
    /// </summary>
    /// <returns>A read-only list of high contribution phrase templates.</returns>
    public IReadOnlyList<string> HighContributionPhrases { get; }

    /// <summary>
    /// Selects a phrase based on contribution level and fills placeholders.
    /// </summary>
    /// <param name="name">The contributor name.</param>
    /// <param name="percent">The contribution percentage.</param>
    /// <param name="project">The repository project name.</param>
    /// <returns>A formatted phrase ready for AI enhancement.</returns>
    public string BuildPhrase(string name, decimal percent, string project)
    {
        string phraseTemplate;

        if (percent < 15)
        {
            phraseTemplate = LowContributionPhrases[Random.Shared.Next(LowContributionPhrases.Count)];
        }
        else if (percent > 40)
        {
            phraseTemplate = HighContributionPhrases[Random.Shared.Next(HighContributionPhrases.Count)];
        }
        else
        {
            return $"{name} did alr ig";
        }

        return phraseTemplate
            .Replace("{name}", name)
            .Replace("{percent}", percent.ToString("0.00"))
            .Replace("{project}", project);
    }
}
