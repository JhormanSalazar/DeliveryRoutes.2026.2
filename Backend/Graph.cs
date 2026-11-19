namespace Backend;

public class Graph<T> where T : notnull
{
    private readonly Dictionary<T, List<Edge<T>>> _adjacency = [];

    public Graph(bool directed = false)
    {
        Directed = directed;
    }

    public bool Directed { get; }

    public int VertexCount => _adjacency.Count;

    public int EdgeCount { get; private set; }

    public IReadOnlyCollection<T> Vertices => _adjacency.Keys;

    public bool Contains(T vertex) => _adjacency.ContainsKey(vertex);

    public void AddVertex(T vertex) => _adjacency.TryAdd(vertex, []);

    public void AddEdge(T from, T to, double weight)
    {
        AddVertex(from);
        AddVertex(to);

        _adjacency[from].Add(new Edge<T>(to, weight));
        EdgeCount++;

        if (!Directed)
        {
            _adjacency[to].Add(new Edge<T>(from, weight));
            EdgeCount++;
        }
    }

    public IReadOnlyList<Edge<T>> Neighbors(T vertex) =>
        _adjacency.TryGetValue(vertex, out var edges) ? edges : [];
}
