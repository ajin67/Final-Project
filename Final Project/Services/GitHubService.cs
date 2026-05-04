using Final_Project.Models;
using Octokit;

namespace Final_Project.Services;

/// <summary>
/// Provides GitHub data retrieval and contribution analysis.
/// </summary>
public sealed class GitHubService
{
    private readonly GitHubClient _gitHubClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubService"/> class.
    /// </summary>
    /// <returns>A constructed <see cref="GitHubService"/> object.</returns>
    public GitHubService()
    {
        _gitHubClient = new GitHubClient(new ProductHeaderValue("GitHubContributionAnalyzer"));
    }

    /// <summary>
    /// Parses a GitHub repository URL and returns owner and repository names.
    /// </summary>
    /// <param name="repositoryUrl">The repository URL to parse.</param>
    /// <returns>A tuple containing owner and repository names.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is invalid.</exception>
    public (string Owner, string Repository) ParseRepositoryUrl(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("The provided URL is not valid.", nameof(repositoryUrl));
        }

        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            throw new ArgumentException("GitHub URL must contain owner and repository name.", nameof(repositoryUrl));
        }

        return (segments[0], segments[1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Computes contribution data by scanning recent commits and changed file stats.
    /// </summary>
    /// <param name="owner">The GitHub repository owner.</param>
    /// <param name="repository">The GitHub repository name.</param>
    /// <returns>A list of contribution results.</returns>
    public async Task<IReadOnlyList<ContributionResult>> AnalyzeContributionsAsync(string owner, string repository)
    {
        IReadOnlyList<GitHubCommit> commits = await _gitHubClient.Repository.Commit.GetAll(owner, repository);

        var contributorMetrics = new Dictionary<string, (int Additions, int Deletions, decimal Importance)>();
        decimal totalWeightedScore = 0;

        foreach (GitHubCommit commit in commits.Take(200))
        {
            string contributorName = commit.Author?.Login ?? commit.Commit.Author.Name;
            GitHubCommit detailedCommit = await _gitHubClient.Repository.Commit.Get(owner, repository, commit.Sha);

            int commitAdditions = detailedCommit.Files.Sum(file => file.Additions);
            int commitDeletions = detailedCommit.Files.Sum(file => file.Deletions);
            decimal commitImportance = detailedCommit.Files.Sum(CalculateFileImportance);

            if (!contributorMetrics.TryGetValue(contributorName, out var currentMetrics))
            {
                currentMetrics = (0, 0, 0);
            }

            contributorMetrics[contributorName] =
            (
                currentMetrics.Additions + commitAdditions,
                currentMetrics.Deletions + commitDeletions,
                currentMetrics.Importance + commitImportance
            );

            totalWeightedScore += commitAdditions + commitDeletions + commitImportance;
        }

        var results = new List<ContributionResult>();
        foreach (KeyValuePair<string, (int Additions, int Deletions, decimal Importance)> entry in contributorMetrics)
        {
            decimal userScore = entry.Value.Additions + entry.Value.Deletions + entry.Value.Importance;
            decimal contributionPercent = totalWeightedScore == 0 ? 0 : (userScore / totalWeightedScore) * 100;

            results.Add(new ContributionResult(entry.Key, entry.Value.Additions, entry.Value.Deletions, entry.Value.Importance, contributionPercent));
        }

        return results.OrderByDescending(result => result.ContributionPercent).ToList();
    }

    private static decimal CalculateFileImportance(GitHubCommitFile file)
    {
        string fileName = file.FileName.ToLowerInvariant();

        if (fileName.Contains("program.cs") || fileName.Contains("startup") || fileName.Contains(".csproj"))
        {
            return file.Changes * 1.5m;
        }

        if (fileName.Contains("service") || fileName.Contains("controller") || fileName.Contains("repository"))
        {
            return file.Changes * 1.3m;
        }

        if (fileName.Contains("readme") || fileName.Contains(".md") || fileName.Contains("test"))
        {
            return file.Changes * 0.7m;
        }

        return file.Changes;
    }
}
