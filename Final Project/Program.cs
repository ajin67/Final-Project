using Final_Project.Models;
using Final_Project.Services;
using Octokit;
using Spectre.Console;

namespace Final_Project;

/// <summary>
/// Entry point for the GitHub Contribution Analyzer.
///
/// WHAT THIS PROGRAM DOES:
///   1. Shows a decorative startup banner
///   2. Asks the user for a GitHub repository URL
///   3. Fetches all contributors (including people with 0 commits)
///   4. Generates a roast/praise phrase for each contributor based on their percentage tier
///   5. Sends that phrase to the local AI for a witty finishing sentence
///   6. Displays everything in a colour-coded table
///   7. Asks the user if they want to check another repo or exit
///   8. Loops back to step 2 if they want another, or shows a goodbye and quits
///
/// TIER SYSTEM (see PhraseDatabase for the full phrase lists):
///   0%            → MEGA BUM
///   0% &lt; x &lt; 1%  → bone spurs curse
///   1% – 9.99%    → super bum, call yo fricking uber
///   10% – 29.99%  → bum ahh insult
///   30% – 69.99%  → did alr ig
///   ≥ 70%         → goodest boy / girl / pal
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point. Sets up services, shows the banner,
    /// then runs the main analysis loop until the user chooses to exit.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    public static async Task Main(string[] args)
    {
        // ── Service setup ────────────────────────────────────────────────────
        // GitHubService  — talks to the GitHub API
        // PhraseDatabase — picks and formats the roast/praise phrase
        // AIService      — sends the phrase to the local AI for enhancement
        GitHubService gitHubService = new();
        PhraseDatabase phraseDatabase = new();
        AIService aiService = new();

        // Show the decorative startup banner (clears the screen first)
        PrintBanner();

        // ── Main loop ────────────────────────────────────────────────────────
        // Keeps running until the user chooses "Exit" at the end-of-run prompt
        bool keepRunning = true;
        while (keepRunning)
        {
            // Ask the user to paste a GitHub repo URL
            string repositoryUrl = AnsiConsole.Ask<string>("\n[bold green]Enter a GitHub repository URL:[/]");

            try
            {
                // Split the URL into owner ("ajin67") and repo name ("Final-Project")
                (string owner, string repository) = gitHubService.ParseRepositoryUrl(repositoryUrl);

                // Fetch and analyse contributions — shown with a spinner so it doesn't look frozen.
                // This hits the API for each commit (up to 200), so it can take a moment.
                IReadOnlyList<ContributionResult> results = await AnsiConsole
                    .Status()
                    .Spinner(Spinner.Known.Dots2)
                    .SpinnerStyle(Style.Parse("cyan bold"))
                    .StartAsync(
                        $"[cyan]Fetching commits for [bold]{owner}/{repository}[/]...[/]",
                        async _ => await gitHubService.AnalyzeContributionsAsync(owner, repository));

                // Handle the edge case of a repo with no commits at all
                if (results.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No contributors found to analyze.[/]");
                }
                else
                {
                    // Print a visual divider and section header above the table
                    PrintSectionDivider();
                    AnsiConsole.MarkupLine($"[bold cyan]Results for:[/] [bold white]{owner}/{repository}[/]");
                    PrintSectionDivider();

                    var resultTable = new Table()
                        .Border(TableBorder.Double)
                        .BorderStyle(Style.Parse("cyan"))
                        .Title($"[bold yellow]{repository.ToUpperInvariant()} CONTRIBUTION REPORT[/]")
                        .Caption("[dim]Roast-grade AI analysis -- no feelings were considered[/]");

                    resultTable.AddColumn(new TableColumn("[bold white]Contributor[/]").Centered());
                    resultTable.AddColumn(new TableColumn("[bold green]Additions[/]").RightAligned());
                    resultTable.AddColumn(new TableColumn("[bold red]Deletions[/]").RightAligned());
                    resultTable.AddColumn(new TableColumn("[bold magenta]Importance[/]").RightAligned());
                    resultTable.AddColumn(new TableColumn("[bold yellow]Contribution %[/]").RightAligned());
                    resultTable.AddColumn(new TableColumn("[bold white]Final Verdict[/]"));

                    // ── Add one row per contributor ──────────────────────────
                    foreach (ContributionResult result in results)
                    {
                        // Step 1: Build the base roast/praise phrase for this contributor's tier
                        string basePhrase = phraseDatabase.BuildPhrase(
                            result.ContributorName,
                            result.ContributionPercent,
                            repository);

                        // Step 2: Send the phrase to the local AI for a finishing sentence.
                        // We wrap this in a spinner so the user sees activity while waiting.
                        string aiPhrase = string.Empty;
                        await AnsiConsole.Status()
                            .Spinner(Spinner.Known.Star)
                            .SpinnerStyle(Style.Parse("yellow"))
                            .StartAsync(
                                $"[yellow]Roasting [bold]{result.ContributorName}[/]...[/]",
                                async _ =>
                                {
                                    aiPhrase = await aiService.EnhancePhraseAsync(
                                        basePhrase, repository, result);
                                });

                        // Step 3: Pick a colour for this contributor based on their tier
                        // so the table is visually colour-coded at a glance
                        string tierColor = GetTierColor(result.ContributionPercent);

                        // Markup.Escape() is important — it prevents contributor names
                        // or AI phrases that contain "[" from breaking Spectre's markup parser
                        resultTable.AddRow(
                            $"[{tierColor}]{Markup.Escape(result.ContributorName)}[/]",
                            $"[green]{result.Additions}[/]",
                            $"[red]{result.Deletions}[/]",
                            $"[magenta]{result.ImportanceScore:0.00}[/]",
                            $"[{tierColor}]{result.ContributionPercent:0.00}%[/]",
                            Markup.Escape(aiPhrase));
                    }

                    // Render the completed table to the console
                    AnsiConsole.Write(resultTable);

                    // Print the colour-coded tier legend below the table so users
                    // know what each colour means without having to guess
                    PrintTierLegend();
                }
            }
            catch (ArgumentException ex)
            {
                // User entered a bad URL — show the specific problem
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

            // ── End-of-run prompt ────────────────────────────────────────────
            // Ask the user whether to check another repo or quit.
            // We use plain Console.ReadLine() here instead of Spectre's SelectionPrompt
            // because SelectionPrompt relies on arrow keys + Enter, which is unreliable
            // across terminals and was causing the loop to always exit regardless of
            // what the user picked. A simple y/n read works everywhere.
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]Check another project? (y/n):[/] ");
            string input = Console.ReadLine()?.Trim().ToLower() ?? "n";

            // Keep looping only if the user typed y or yes
            keepRunning = input == "y" || input == "yes";
        }

        // Show a friendly goodbye message before the program closes
        PrintGoodbye();
    }

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPER METHODS
    // Each helper is responsible for one visual element of the UI.
    // Keeping them separate makes Main() easier to read.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clears the console and renders the startup banner.
    /// Uses a FigletText for the big ASCII-art title and a panel for the subtitle.
    /// </summary>
    private static void PrintBanner()
    {
        AnsiConsole.Clear();

        // FigletText renders "ContribRater" in big block letters using Spectre's built-in fonts
        AnsiConsole.Write(new FigletText("ContribRater").Color(Color.Cyan1));

        // A framed panel acts as a subtitle / description block under the title
        var panel = new Panel(
            "[bold white]GitHub Contribution Analyzer[/]\n" +
            "[dim] Exposing bums.[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = Style.Parse("cyan"),
            Padding = new Padding(2, 1)    // left/right = 2, top/bottom = 1
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine(); // breathing room before the URL prompt
    }

    /// <summary>
    /// Prints a horizontal divider line to visually separate sections.
    /// Uses Spectre's Rule widget which auto-sizes to the terminal width.
    /// </summary>
    private static void PrintSectionDivider()
    {
        AnsiConsole.Write(new Rule { Style = Style.Parse("dim cyan") });
    }

    /// <summary>
    /// Prints a small legend table below the results so the user understands
    /// what each tier colour means.
    ///
    /// Tier colours match what GetTierColor() returns:
    ///   gold1      → 70%+       goodest one
    ///   green      → 30–69.99%  did alr ig
    ///   yellow     → 10–29.99%  bum ahh
    ///   darkorange → 1–9.99%    super bum
    ///   red        → &lt; 1%    bone spurs
    ///   bold red   → 0%         MEGA BUM
    /// </summary>
    private static void PrintTierLegend()
    {
        AnsiConsole.WriteLine();

        var legend = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse("dim"))
            .Title("[dim]Tier Reference[/]")
            .AddColumn(new TableColumn("[dim]Range[/]").Centered())
            .AddColumn(new TableColumn("[dim]Verdict[/]"));

        // Rows go from best → worst so users scan top-down naturally
        legend.AddRow("[bold gold1]>= 70%[/]", "[bold gold1]Goodest boy / girl / pal[/]");
        legend.AddRow("[bold green]30 - 69.99%[/]", "[bold green]Did alr ig[/]");
        legend.AddRow("[bold yellow]10 - 29.99%[/]", "[bold yellow]Bum ahh[/]");
        legend.AddRow("[bold darkorange]1 - 9.99%[/]", "[bold darkorange]Super bum, call yo uber[/]");
        legend.AddRow("[bold red]0.01 - 0.99%[/]", "[bold red]Bone spurs curse[/]");
        legend.AddRow("[bold red]0%[/]", "[bold red]MEGA BUM[/]");

        AnsiConsole.Write(legend);
    }

    /// <summary>
    /// Prints a goodbye panel when the user chooses to exit the loop.
    /// </summary>
    private static void PrintGoodbye()
    {
        AnsiConsole.WriteLine();

        var panel = new Panel(
            "[bold cyan]Thanks for using ContribRater![/]\n" +
            "[dim]May your commits be many, and your bums be few.[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("cyan"),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Maps a contribution percentage to a Spectre.Console colour string.
    /// These colours are used in the results table to colour-code each row
    /// so you can tell a contributor's tier at a glance.
    ///
    /// Matches the tier system exactly:
    ///   0%            → bold red     (MEGA BUM)
    ///   0% to &lt;1%  → red          (bone spurs)
    ///   1–9.99%       → darkorange   (super bum)
    ///   10–29.99%     → yellow       (bum ahh)
    ///   30–69.99%     → green        (did alr ig)
    ///   ≥ 70%         → gold1        (goodest one)
    /// </summary>
    /// <param name="percent">The contributor's percentage (0–100).</param>
    /// <returns>A Spectre.Console colour/style string for use inside markup tags.</returns>
    private static string GetTierColor(decimal percent) =>
        percent switch
        {
            0m => "bold red",      // MEGA BUM — loudest colour
            > 0m and < 1m => "red",           // bone spurs
            >= 1m and < 10m => "darkorange",    // super bum
            >= 10m and < 30m => "yellow",        // bum ahh
            >= 30m and < 70m => "green",         // did alr ig
            _ => "gold1"          // goodest one — shiniest colour
        };
}