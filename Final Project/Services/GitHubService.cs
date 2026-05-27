using Final_Project.Models;
using Octokit;

namespace Final_Project.Services;

/// <summary>
/// Handles all communication with the GitHub API.
///
/// Responsibilities:
///   1. Parse a GitHub URL into (owner, repository) parts
///   2. Fetch ALL collaborators on the repo — even people who never committed —
///      so they show up in results with 0% (and get called MEGA BUM accordingly)
///   3. Walk the last 200 commits, accumulate per-contributor stats
///   4. Compute each contributor's weighted percentage of the total work
/// </summary>
public sealed class GitHubService
{
    // The Octokit client that talks to GitHub's REST API.
    // Octokit is the official .NET GitHub SDK.
    private readonly GitHubClient _gitHubClient;

    /// <summary>
    /// Creates the service and sets up the GitHub API client.
    /// ProductHeaderValue is required by GitHub's API — it identifies your app.
    /// </summary>
    public GitHubService()
    {
        _gitHubClient = new GitHubClient(new ProductHeaderValue("GitHubContributionAnalyzer"));
    }

    /// <summary>
    /// Breaks a full GitHub URL like "https://github.com/owner/repo" into
    /// its owner and repository name parts.
    ///
    /// Examples:
    ///   "https://github.com/ajin67/Final-Project"  →  ("ajin67", "Final-Project")
    ///   "https://github.com/ajin67/Final-Project.git"  →  ("ajin67", "Final-Project")
    /// </summary>
    /// <param name="repositoryUrl">The full GitHub URL entered by the user.</param>
    /// <returns>A tuple with Owner and Repository name.</returns>
    /// <exception cref="ArgumentException">Thrown if the URL can't be parsed or is missing parts.</exception>
    public (string Owner, string Repository) ParseRepositoryUrl(string repositoryUrl)
    {
        // Make sure the string is actually a valid URL before doing anything else
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("The provided URL is not valid.", nameof(repositoryUrl));
        }

        // Split the URL path — e.g. "/ajin67/Final-Project" → ["ajin67", "Final-Project"]
        // RemoveEmptyEntries handles any trailing slashes cleanly
        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // We need at least two segments: owner and repo name
        if (segments.Length < 2)
        {
            throw new ArgumentException("GitHub URL must contain owner and repository name.", nameof(repositoryUrl));
        }

        // segments[0] = owner login, segments[1] = repo name
        // Strip ".git" suffix if someone pasted a clone URL instead of a browser URL
        return (segments[0], segments[1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The main analysis method. Returns a ranked list of every contributor
    /// (including 0-contribution collaborators) with their stats.
    ///
    /// HOW IT WORKS:
    ///   Step 1 — Fetch all collaborators and seed them in the dictionary with
    ///            (0 additions, 0 deletions, 0 importance). This guarantees that
    ///            even someone who never committed still appears in the final results
    ///            with 0%, so they get correctly labelled as MEGA BUM.
    ///
    ///   Step 2 — Fetch the list of commits (up to 200).
    ///
    ///   Step 3 — For each commit, fetch its detailed file-level stats, then add
    ///            those stats to that contributor's running totals.
    ///
    ///   Step 4 — Compute each contributor's percentage of the total weighted work.
    ///
    ///   Step 5 — Return the list sorted highest → lowest contribution.
    /// </summary>
    /// <param name="owner">The repository owner login (e.g. "ajin67").</param>
    /// <param name="repository">The repository name (e.g. "Final-Project").</param>
    /// <returns>
    /// A read-only list of <see cref="ContributionResult"/>, ordered by contribution descending.
    /// Zero-contribution collaborators are included at the bottom.
    /// </returns>
    public async Task<IReadOnlyList<ContributionResult>> AnalyzeContributionsAsync(string owner, string repository)
    {
        // ── STEP 1: Seed all collaborators with zero stats ──────────────────
        // We use a case-insensitive dictionary because GitHub logins are case-insensitive.
        var contributorMetrics = new Dictionary<string, (int Additions, int Deletions, decimal Importance)>(
            StringComparer.OrdinalIgnoreCase);

        // Try to fetch the full collaborator list from the repo.
        // This requires that the authenticated user (or the public API) has access.
        // If the token doesn't have enough permission (e.g. a public repo where
        // we're not authenticated), GitHub returns 403 — we catch it and just skip
        // the seed step. Contributors will still be built up from the commit loop below.
        try
        {
            IReadOnlyList<Collaborator> collaboratorsRaw =
                await _gitHubClient.Repository.Collaborator.GetAll(owner, repository);
            foreach (Collaborator collaborator in collaboratorsRaw)
            {
                contributorMetrics[collaborator.Login] = (0, 0, 0m);
            }
        }
        catch (AuthorizationException)
        {
            // We don't have permission to list collaborators.
            // That's okay — we'll still capture everyone who shows up in commits.
            // This is a non-fatal fallback; the dictionary just starts empty.
        }

        // ── STEP 2 & 3: Walk commits and accumulate stats ───────────────────

        // Fetch the full list of commits for the repo (lightweight, no file details yet)
        IReadOnlyList<GitHubCommit> commits =
            await _gitHubClient.Repository.Commit.GetAll(owner, repository);

        // totalWeightedScore tracks the repo-wide sum of all additions + deletions + importance.
        // We use it later to calculate each contributor's percentage share.
        decimal totalWeightedScore = 0;

        // Only process up to 200 commits to avoid hammering the API rate limit
        foreach (GitHubCommit commit in commits.Take(200))
        {
            // Prefer the GitHub login (linked account); fall back to the Git author name
            // for commits made without a linked GitHub account
            string contributorName = commit.Author?.Login ?? commit.Commit.Author.Name;

            // Fetch the detailed commit, which includes per-file additions/deletions
            GitHubCommit detailedCommit =
                await _gitHubClient.Repository.Commit.Get(owner, repository, commit.Sha);

            // Sum up lines added and deleted across all files in this commit
            int commitAdditions = detailedCommit.Files.Sum(file => file.Additions);
            int commitDeletions = detailedCommit.Files.Sum(file => file.Deletions);

            // Calculate a weighted importance score for this commit.
            // Core files (Program.cs, services, etc.) count more than docs/tests.
            decimal commitImportance = detailedCommit.Files.Sum(CalculateFileImportance);

            // Get the contributor's existing totals (or start at zero if new)
            if (!contributorMetrics.TryGetValue(contributorName, out var currentMetrics))
            {
                currentMetrics = (0, 0, 0m);
            }

            // Add this commit's numbers to their running total
            contributorMetrics[contributorName] =
            (
                currentMetrics.Additions + commitAdditions,
                currentMetrics.Deletions + commitDeletions,
                currentMetrics.Importance + commitImportance
            );

            // Track the repo-wide total so we can compute percentages later
            totalWeightedScore += commitAdditions + commitDeletions + commitImportance;
        }

        // ── STEP 4 & 5: Compute percentages and build result objects ─────────

        var results = new List<ContributionResult>();

        foreach (KeyValuePair<string, (int Additions, int Deletions, decimal Importance)> entry in contributorMetrics)
        {
            // A contributor's "score" is their personal additions + deletions + importance
            decimal userScore = entry.Value.Additions + entry.Value.Deletions + entry.Value.Importance;

            // Percentage = (their score / total repo score) × 100
            // Guard against divide-by-zero on empty repos
            decimal contributionPercent = totalWeightedScore == 0
                ? 0
                : (userScore / totalWeightedScore) * 100;

            results.Add(new ContributionResult(
                entry.Key,
                entry.Value.Additions,
                entry.Value.Deletions,
                entry.Value.Importance,
                contributionPercent));
        }

        // Return sorted highest → lowest so the table is ranked naturally
        return results.OrderByDescending(result => result.ContributionPercent).ToList();
    }

    /// <summary>
    /// Assigns an importance multiplier to a single changed file based on its name.
    ///
    /// The idea: changing an entry point or a service class takes more thought than
    /// editing a README or a test file, so we weight those changes higher.
    ///
    /// Multipliers:
    ///   ×1.5  — core files: program.cs, startup, .csproj
    ///   ×1.3  — service/controller/repository files
    ///   ×0.7  — docs and tests (readme, .md, test)
    ///   ×1.0  — everything else (default)
    /// </summary>
    /// <param name="file">A single file entry from a GitHub commit's file list.</param>
    /// <returns>
    /// A decimal representing the weighted change count for this file.
    /// </returns>
    private static decimal CalculateFileImportance(GitHubCommitFile file)
    {
        // Work with a lowercase filename so the comparisons are case-insensitive
        string fileName = file.Filename.ToLowerInvariant();

        // Entry points and project config files — highest weight
        if (fileName.Contains("program.cs") || fileName.Contains("startup") || fileName.Contains(".csproj"))
        {
            return file.Changes * 1.5m;
        }

        // Business logic files — elevated weight
        if (fileName.Contains("service") || fileName.Contains("controller") || fileName.Contains("repository"))
        {
            return file.Changes * 1.3m;
        }

        // Docs and tests — reduced weight (easier to churn, lower cognitive cost)
        if (fileName.Contains("readme") || fileName.Contains(".md") || fileName.Contains("test"))
        {
            return file.Changes * 0.7m;
        }

        // Default: count changes at face value
        return file.Changes;
    }
}