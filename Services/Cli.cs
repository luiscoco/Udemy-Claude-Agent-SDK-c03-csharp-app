using System.Text.Json;
using Anthropic;

namespace LessonC03.Services;

// Console equivalents of `npm run setup`, `npm run session` and `npm run history`.
public static class Cli
{
    // One-time setup: creates the environment and base agent, then saves their IDs.
    // Agents are persistent - run once, reuse the IDs.
    public static async Task Setup()
    {
        var (agent, environmentId) = await new SessionLab(new AnthropicClient()).Setup();

        var ids = new { agentId = agent.ID, agentVersion = agent.Version, environmentId };
        await File.WriteAllTextAsync(".managed-ids.json", JsonSerializer.Serialize(ids, new JsonSerializerOptions { WriteIndented = true }));

        // The lab reads the IDs from the environment, so write them into .env too.
        DotEnv.Upsert(".env", "CLAUDE_MANAGED_AGENT_ID", agent.ID);
        DotEnv.Upsert(".env", "CLAUDE_MANAGED_ENVIRONMENT_ID", environmentId);

        Console.WriteLine($"Base agent: {agent.ID} · version {agent.Version} · tools {agent.Tools.Count}");
        Console.WriteLine($"Saved .managed-ids.json and updated .env: {JsonSerializer.Serialize(ids)}");
    }

    // Slides 07-09 in one run: override, resolved snapshot, live stream, recorded history.
    public static async Task Session(string prompt)
    {
        if (!SessionLab.Configured)
            throw new InvalidOperationException("CLAUDE_MANAGED_AGENT_ID / CLAUDE_MANAGED_ENVIRONMENT_ID missing - run `dotnet run -- setup` first.");
        if (string.IsNullOrWhiteSpace(prompt)) prompt = "Why is the sky blue?";

        var lab = new SessionLab(new AnthropicClient());
        string? sessionId = null;
        await lab.Run(prompt, new RunObserver
        {
            Session = s =>
            {
                sessionId = s.Id;
                Console.WriteLine("\n== Slide 08 · resolved snapshot ==");
                Console.WriteLine($"{s.Id} · agent {s.AgentId} v{s.Version}");
                Console.WriteLine($"{{ tools: {s.Tools}, mcp: {s.Mcp}, skills: {s.Skills} }}");
            },
            Stored = s => Console.WriteLine($"stored agent (unchanged): {{ version: {s.Version}, tools: {s.Tools}, mcp: {s.Mcp}, skills: {s.Skills} }}\n\n== Slide 09 · watch live ==\nuser> {prompt}"),
            Event = e => Console.WriteLine(e.Type),
            Answer = text => Console.WriteLine($"  claude> {text}"),
            Stop = reason => Console.WriteLine($"stopped: {reason}"),
            Error = message => Console.Error.WriteLine($"  session error: {message}"),
            History = history =>
            {
                Console.WriteLine("\n== Slide 09 · read the record ==");
                foreach (var e in history) Console.WriteLine($"{e.Id} {e.Type}");
            },
        });
        if (sessionId is not null)
            Console.WriteLine($"\nRe-read this record any time: dotnet run -- history  (or: dotnet run -- history {sessionId})");
    }

    // Read the record - for any existing session, no live connection needed.
    public static async Task History(string? sessionId)
    {
        sessionId ??= File.Exists(SessionLab.LastSessionFile) ? File.ReadAllText(SessionLab.LastSessionFile).Trim() : null;
        if (string.IsNullOrEmpty(sessionId))
            throw new InvalidOperationException("Usage: dotnet run -- history sesn_...  (or run `dotnet run -- session` first)");

        Console.WriteLine($"session {sessionId}");
        foreach (var e in await new SessionLab(new AnthropicClient()).ReadHistory(sessionId))
            Console.WriteLine($"{e.Id} {e.Type}");
    }
}
