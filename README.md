# C03 · Session Configuration and History (Blazor / .NET 10)

C# port of [`../lesson-c03-app`](../lesson-c03-app) using the official [`Anthropic`](https://www.nuget.org/packages/Anthropic) NuGet package (13.3.0) instead of `@anthropic-ai/sdk`.

Creates a real text-only override session, compares configurations, opens the stream before sending, and reads recorded history. Closing the browser stops observation; it does not interrupt remote work.

## Configure and run

Requires the .NET 10 SDK. In PowerShell, from this folder:

```powershell
dotnet --version
dotnet restore
Copy-Item .env.example .env
notepad .env
dotnet run -- setup     # once: creates the environment + base agent, writes their IDs into .env
dotnet run              # web app on http://127.0.0.1:3003 (or PORT)
```

Console equivalents of the Node scripts:

| Node (`lesson-c03-app`) | .NET (`lesson-c03-csharp-app`) |
|---|---|
| `npm run setup` | `dotnet run -- setup` |
| `npm start` / `npm run dev` | `dotnet run` |
| `npm run session -- <prompt>` | `dotnet run -- session <prompt>` |
| `npm run history -- [sesn_...]` | `dotnet run -- history [sesn_...]` |
| `npm run typecheck` | `dotnet build` |

## Source map

| Node source | C# source |
|---|---|
| `src/web.ts` (HTTP server + NDJSON stream) | `Program.cs`, `Components/Pages/Home.razor`, `Components/Pages/SessionRunner.razor` (interactive server component; updates flow over the Blazor circuit instead of NDJSON) |
| `src/session.ts`, `src/setup.ts`, `src/history.ts` | `Services/SessionLab.cs` (API calls), `Services/Cli.cs` (console output) |
| `--env-file-if-exists=.env` | `Services/DotEnv.cs` |
| `public/guide.html`, `public/architecture.html` | `Components/Pages/Guide.razor` (`/guide`), `Components/Pages/Architecture.razor` (`/architecture`) |
| `public/*.css`, `navigation.js`, images | `wwwroot/` |

## SDK mapping (TypeScript → C#)

| TypeScript | C# |
|---|---|
| `client.beta.sessions.create({ agent: { type: "agent_with_overrides", ... } })` | `client.Beta.Sessions.Create(new SessionCreateParams { Agent = new BetaManagedAgentsAgentWithOverridesParams { ... } })` |
| `budget: { type: "limit", max_list_cost: {...} }` | `Budget = new BetaManagedAgentsBudgetLimit { MaxListCost = new() { Currency = "USD", Amount = "50" } }` |
| `client.beta.agents.retrieve(id)` | `client.Beta.Agents.Retrieve(id)` |
| `await client.beta.sessions.events.stream(id)` | `await client.Beta.Sessions.Events.WithRawResponse.StreamStreaming(id)` then `.Enumerate(ct)` — awaiting the raw response guarantees the stream is open before sending |
| `client.beta.sessions.events.send(id, { events: [...] })` | `client.Beta.Sessions.Events.Send(id, new EventSendParams { Events = [new BetaManagedAgentsUserMessageEventParams { ... }] })` |
| `event.type === "agent.message"` | `ev.TryPickAgentMessageEvent(out var message)` |
| `for await (const e of client.beta.sessions.events.list(id))` | `await foreach (var e in (await client.Beta.Sessions.Events.List(id)).Paginate())` |
| `client.beta.environments.create(...)` / `client.beta.agents.create(...)` | `client.Beta.Environments.Create(...)` / `client.Beta.Agents.Create(...)` |

## .env

```dotenv
ANTHROPIC_API_KEY=your_api_key
CLAUDE_MODEL=claude-opus-5-5
CLAUDE_SESSION_MODEL=
CLAUDE_MANAGED_AGENT_ID=your_agent_id
CLAUDE_MANAGED_ENVIRONMENT_ID=your_environment_id
PORT=3003
```

Real environment variables take precedence over `.env`. Restart the server after editing it. The existing IDs from `../lesson-c03-app/.env` can be reused — agents and environments are persistent.
