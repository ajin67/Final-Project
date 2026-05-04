namespace Final_Project.Models;

/// <summary>
/// Represents contribution metrics and scoring for an individual GitHub contributor.
/// </summary>
public sealed class ContributionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContributionResult"/> class.
    /// </summary>
    /// <param name="contributorName">The GitHub login of the contributor.</param>
    /// <param name="additions">The total lines added by the contributor.</param>
    /// <param name="deletions">The total lines deleted by the contributor.</param>
    /// <param name="importanceScore">The weighted importance score based on changed files.</param>
    /// <param name="contributionPercent">The contributor percentage relative to repository totals.</param>
    /// <returns>A constructed <see cref="ContributionResult"/> object.</returns>
    public ContributionResult(string contributorName, int additions, int deletions, decimal importanceScore, decimal contributionPercent)
    {
        ContributorName = contributorName;
        Additions = additions;
        Deletions = deletions;
        ImportanceScore = importanceScore;
        ContributionPercent = contributionPercent;
    }

    /// <summary>
    /// Gets the contributor login name.
    /// </summary>
    public string ContributorName { get; }

    /// <summary>
    /// Gets the number of added lines.
    /// </summary>
    public int Additions { get; }

    /// <summary>
    /// Gets the number of deleted lines.
    /// </summary>
    public int Deletions { get; }

    /// <summary>
    /// Gets the calculated weighted importance score.
    /// </summary>
    public decimal ImportanceScore { get; }

    /// <summary>
    /// Gets the contribution percentage.
    /// </summary>
    public decimal ContributionPercent { get; }
}
