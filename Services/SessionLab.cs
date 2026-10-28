// The C# equivalent of src/web.ts, src/session.ts, src/setup.ts and src/history.ts:
// one session override, the stored vs. effective snapshot, the live stream and the record.
using Anthropic;
using Anthropic.Models.Beta.Agents;
using Anthropic.Models.Beta.Environments;
using Anthropic.Models.Beta.Sessions;
using Anthropic.Models.Beta.Sessions.Events;
using SessionModel = Anthropic.Models.Beta.Sessions.Model;

namespace LessonC03.Services;

public sealed record SessionSnapshot(string Id, string AgentId, long Version, int Tools, int Mcp, int Skills);
public sealed record StoredSnapshot(long Version, int Tools, int Mcp, int Skills);
public sealed record HistoryEntry(string Id, string Type);

// Callbacks the UI (or the console) receives while a run progresses.
public sealed class RunObserver
{
    public Action<SessionSnapshot> Session { get; init; } = _ => { };
    public Action<StoredSnapshot> Stored { get; init; } = _ => { };
    public Action<HistoryEntry> Event { get; init; } = _ => { };
    public Action<string> Answer { get; init; } = _ => { };
    public Action<string> Stop { get; init; } = _ => { };
    public Action<string> Error { get; init; } = _ => { };
    public Action<IReadOnlyList<HistoryEntry>> History { get; init; } = _ => { };
}

public sealed class SessionLab(AnthropicClient client)
{
    public const string LastSessionFile = ".last-session";

    static string? Env(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : null;

    public static bool Configured =>
        Env("ANTHROPIC_API_KEY") is not null && Env("CLAUDE_MANAGED_AGENT_ID") is not null && Env("CLAUDE_MANAGED_ENVIRONMENT_ID") is not null;

    // ---- Slide 07 · Code 1 · A text-only session override ----------------------
    public Task<BetaManagedAgentsSession> CreateOverrideSession(CancellationToken ct = default) =>
        client.Beta.Sessions.Create(new SessionCreateParams
        {
            Agent = new BetaManagedAgentsAgentWithOverridesParams
            {
                ID = Env("CLAUDE_MANAGED_AGENT_ID")!,
                Model = Env("CLAUDE_SESSION_MODEL") is { } model ? (SessionModel)new BetaManagedAgentsModelConfigParams { ID = model } : null,
                System = "Answer in one sentence.",
                Tools = [],
                McpServers = [],
                Skills = [],
            },
            EnvironmentID = Env("CLAUDE_MANAGED_ENVIRONMENT_ID")!,
            Budget = new BetaManagedAgentsBudgetLimit
            {
                MaxListCost = new() { Currency = "USD", Amount = "50" },
            },
        }, ct);

    // One full run: create → inspect → watch live → read the record.
    public async Task Run(string prompt, RunObserver observe, CancellationToken ct = default)
    {
        try
        {
            var session = await CreateOverrideSession(ct);
            await File.WriteAllTextAsync(LastSessionFile, session.ID, ct);

            // ---- Slide 08 · Code 2 · Inspect the resolved snapshot ---------------------
            observe.Session(new(session.ID, session.Agent.ID, session.Agent.Version,
                session.Agent.Tools.Count, session.Agent.McpServers.Count, session.Agent.Skills.Count));

            // The stored agent is unchanged: it still has its own model, system and tools.
            var stored = await client.Beta.Agents.Retrieve(session.Agent.ID, cancellationToken: ct);
            observe.Stored(new(stored.Version, stored.Tools.Count, stored.McpServers.Count, stored.Skills.Count));

            // ---- Slide 09 · Code 3 · Watch live --------------------------------------
            // Open before sending work. A live stream does not replay prior events.
            // The raw-response call returns once the SSE connection is established.
            using var live = await client.Beta.Sessions.Events.WithRawResponse.StreamStreaming(session.ID, cancellationToken: ct);

            await client.Beta.Sessions.Events.Send(session.ID, new EventSendParams
            {
                Events = [new BetaManagedAgentsUserMessageEventParams
                {
                    Content = [new BetaManagedAgentsTextBlock { Text = prompt.Trim() }],
                }],
            }, ct);

            await foreach (var ev in live.Enumerate(ct))
            {
                observe.Event(new(EventId(ev), EventType(ev)));

                if (ev.TryPickAgentMessageEvent(out var message))
                    foreach (var block in message.Content)
                        if (block.TryPickBetaManagedAgentsTextBlock(out var text)) observe.Answer(text.Text);

                if (ev.TryPickSessionStatusIdleEvent(out var idle))
                {
                    // end_turn = done; budget_reached = paused at the budget cap.
                    var reason = StopReasonType(idle);
                    if (reason != "requires_action") { observe.Stop(reason); break; }
                }
                if (ev.TryPickSessionStatusTerminatedEvent(out _)) { observe.Stop("terminated"); break; }
                if (ev.TryPickSessionErrorEvent(out _))
                {
                    observe.Error("The session reported an error. Inspect its history for details.");
                    break;
                }
            }

            // ---- Slide 09 · Code 3 · Read the record ---------------------------------
            observe.History(await ReadHistory(session.ID, ct));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { observe.Error(error.Message); }
    }

    // List recorded events to inspect previous activity in a session (all pages).
    public async Task<IReadOnlyList<HistoryEntry>> ReadHistory(string sessionId, CancellationToken ct = default)
    {
        var history = new List<HistoryEntry>();
        var page = await client.Beta.Sessions.Events.List(sessionId, cancellationToken: ct);
        await foreach (var ev in page.Paginate(ct))
            history.Add(new(ev.ID, ev.Type));
        return history;
    }

    // One-time setup: an Environment and a base Agent that DOES have capabilities
    // (the built-in agent toolset), so the session override has something to clear.
    public async Task<(BetaManagedAgentsAgent Agent, string EnvironmentId)> Setup(CancellationToken ct = default)
    {
        var environment = await client.Beta.Environments.Create(new EnvironmentCreateParams
        {
            Name = "lesson-c03-env",
            Config = new BetaCloudConfigParams { Networking = new BetaLimitedNetworkParams() },
        }, ct);

        var agent = await client.Beta.Agents.Create(new AgentCreateParams
        {
            Name = "Lesson C03 Base Agent",
            Model = new BetaManagedAgentsModelConfigParams { ID = Env("CLAUDE_MODEL") ?? "claude-opus-5-5" },
            System = "You are a thorough research assistant. Explain your reasoning in detail.",
            Tools = [new BetaManagedAgentsAgentToolset20260401Params()],
        }, ct);

        return (agent, environment.ID);
    }

    static string EventId(BetaManagedAgentsStreamSessionEvents ev) => ev.ID ?? "stream update";
    static string EventType(BetaManagedAgentsStreamSessionEvents ev) => ev.Type;
    static string StopReasonType(BetaManagedAgentsSessionStatusIdleEvent idle) => idle.StopReason.Type;
}
