namespace SeaMonkeys.Core.Models;

public sealed class Battle
{
    public required string MatchGroup { get; init; }
    public required string GameMode { get; init; }
    public required string GameType { get; init; }
    public required string Scenario { get; init; }
    public required string MapDisplayName { get; init; }
    public required int MapId { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required string ClientVersion { get; init; }
    public required string SourceFile { get; init; }
    public required IReadOnlyList<Participant> Participants { get; init; }

    public IEnumerable<Participant> Allies => Participants.Where(p => p.Relation != Relation.Enemy);
    public IEnumerable<Participant> Enemies => Participants.Where(p => p.Relation == Relation.Enemy);
}
