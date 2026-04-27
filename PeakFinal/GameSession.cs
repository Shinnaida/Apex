namespace Peak;

public class GameSession
{
    readonly Random _rng = new();

    public Queue<GameDefinition> Queue { get; }

    public GameSession(GameCategory category, int gamesPerSession = 5)
    {
        var pool = GameCatalog.ForCategory(category)
                              .OrderBy(_ => _rng.Next())
                              .ToList();

        var picked = pool.Take(Math.Min(gamesPerSession, pool.Count)).ToList();
        Queue = new Queue<GameDefinition>(picked);
    }

    public bool TryNext(out GameDefinition? next)
    {
        if (Queue.Count == 0)
        {
            next = null;
            return false;
        }

        next = Queue.Dequeue();
        return true;
    }
}
