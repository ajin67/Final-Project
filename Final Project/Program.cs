using Final_Project.Models;
using Final_Project.Services;
using Spectre.Console;

namespace Final_Project;

/// <summary>
/// Entry point for the GitHub contribution analyzer console app.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the application workflow.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing async execution.</returns>
    public static async Task Main(string[] args)
    {
        GitHubService gitHubService = new();
        PhraseDatabase phraseDatabase = new();
        AIService aiService = new();

        AnsiConsole.MarkupLine("[bold cyan]GitHub Contribution Analyzer[/]");
        string repositoryUrl = AnsiConsole.Ask<string>("Enter a [green]GitHub repository URL[/]:");

        try
        {
            (string owner, string repository) = gitHubService.ParseRepositoryUrl(repositoryUrl);
            IReadOnlyList<ContributionResult> results = await gitHubService.AnalyzeContributionsAsync(owner, repository);

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No commits found to analyze.[/]");
                return;
            }

            var resultTable = new Table().Border(TableBorder.Rounded).Title($"[bold]{repository} Contributions[/]");
            resultTable.AddColumn("Contributor");
            resultTable.AddColumn("Additions");
            resultTable.AddColumn("Deletions");
            resultTable.AddColumn("Importance");
            resultTable.AddColumn("Contribution %");
            resultTable.AddColumn("Final Verdict");

            foreach (ContributionResult result in results)
            {
                string basePhrase = phraseDatabase.BuildPhrase(result.ContributorName, result.ContributionPercent, repository);
                string aiPhrase = string.Empty;

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Asking local AI for {result.ContributorName}...", async _ =>
                    {
                        aiPhrase = await aiService.EnhancePhraseAsync(basePhrase, repository, result);
                    });

                resultTable.AddRow(
                    result.ContributorName,
                    result.Additions.ToString(),
                    result.Deletions.ToString(),
                    result.ImportanceScore.ToString("0.00"),
                    result.ContributionPercent.ToString("0.00"),
                    aiPhrase);
            }

            AnsiConsole.Write(resultTable);
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]Invalid URL:[/] {Markup.Escape(ex.Message)}");
        }
        catch (ApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]GitHub API error:[/] {Markup.Escape(ex.Message)}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Unexpected error:[/] {Markup.Escape(ex.Message)}");
        }
    }
}
