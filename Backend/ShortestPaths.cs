namespace Backend;

public class ShortestPaths
{
    private readonly Dictionary<string, double> _distances = [];
    private readonly Dictionary<string, string> _previous = [];

    public ShortestPaths(Graph<string> graph, string source)
    {
        if (!graph.Contains(source))
        {
            throw new ArgumentException($"La ubicación {source} no está en el mapa.");
        }

        Source = source;
        _distances[source] = 0;

        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(source, 0);

        // stale queue entries are skipped instead of updating priorities
        while (queue.TryDequeue(out var current, out var distance))
        {
            if (distance > _distances[current])
            {
                continue;
            }

            foreach (var edge in graph.Neighbors(current))
            {
                var candidate = distance + edge.Weight;
                if (_distances.TryGetValue(edge.To, out var known) && known <= candidate)
                {
                    continue;
                }

                _distances[edge.To] = candidate;
                _previous[edge.To] = current;
                queue.Enqueue(edge.To, candidate);
            }
        }
    }

    public string Source { get; }

    public bool CanReach(string target) => _distances.ContainsKey(target);

    public double DistanceTo(string target)
    {
        EnsureReachable(target);
        return _distances[target];
    }

    public List<string> PathTo(string target)
    {
        EnsureReachable(target);

        var path = new List<string> { target };
        var current = target;
        while (current != Source)
        {
            current = _previous[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private void EnsureReachable(string target)
    {
        if (!CanReach(target))
        {
            throw new InvalidOperationException($"No hay camino desde {Source} hasta {target}.");
        }
    }
}
