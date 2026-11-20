namespace Backend;

public class RoutePlanner
{
    private readonly IReadOnlyList<Package> _packages;
    private readonly string _start;
    private readonly Dictionary<string, ShortestPaths> _paths = [];
    private readonly double[,] _legs;
    private readonly int _startPoint;
    private readonly int[] _taskPoint;

    public RoutePlanner(Graph<string> city, IReadOnlyList<Package> packages, string start)
    {
        if (!city.Contains(start))
        {
            throw new ArgumentException($"La posición inicial {start} no está en el mapa.");
        }

        _packages = packages;
        _start = start;

        var taskLocations = packages.SelectMany(p => new[] { p.Pickup, p.Delivery }).Distinct().ToList();
        var points = taskLocations.Prepend(start).Distinct().ToList();
        foreach (var point in points)
        {
            _paths[point] = new ShortestPaths(city, point);
        }

        var errors = new List<string>();
        foreach (var from in points)
        {
            foreach (var to in taskLocations)
            {
                if (from != to && !_paths[from].CanReach(to))
                {
                    errors.Add($"No hay camino desde {from} hasta {to}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Hay puntos de la ruta que no se pueden alcanzar:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        _legs = new double[points.Count, points.Count];
        for (var a = 0; a < points.Count; a++)
        {
            for (var b = 0; b < points.Count; b++)
            {
                var paths = _paths[points[a]];
                _legs[a, b] = paths.CanReach(points[b]) ? paths.DistanceTo(points[b]) : double.PositiveInfinity;
            }
        }

        var n = packages.Count;
        _startPoint = points.IndexOf(start);
        _taskPoint = new int[2 * n];
        for (var p = 0; p < n; p++)
        {
            _taskPoint[p] = points.IndexOf(packages[p].Pickup);
            _taskPoint[p + n] = points.IndexOf(packages[p].Delivery);
        }
    }

    public List<Stop> PlanGreedy()
    {
        var n = _packages.Count;
        var picked = new bool[n];
        var delivered = new bool[n];
        var route = new List<int>(2 * n);
        var current = _startPoint;

        while (route.Count < 2 * n)
        {
            var best = -1;
            var bestDistance = double.PositiveInfinity;
            for (var p = 0; p < n; p++)
            {
                if (delivered[p])
                {
                    continue;
                }

                var task = picked[p] ? p + n : p;
                var distance = _legs[current, _taskPoint[task]];
                if (distance < bestDistance)
                {
                    best = task;
                    bestDistance = distance;
                }
            }

            if (best < n)
            {
                picked[best] = true;
            }
            else
            {
                delivered[best - n] = true;
            }

            route.Add(best);
            current = _taskPoint[best];
        }

        return ToStops(route);
    }

    public double TotalDistance(IReadOnlyList<Stop> route)
    {
        var total = 0.0;
        var current = _start;
        foreach (var stop in route)
        {
            total += DistanceBetween(current, stop.Location);
            current = stop.Location;
        }

        return total;
    }

    public bool IsValid(IReadOnlyList<Stop> route)
    {
        if (route.Count != 2 * _packages.Count)
        {
            return false;
        }

        var packagesById = _packages.ToDictionary(p => p.Id);
        var pickupAt = new Dictionary<int, int>();
        var deliveryAt = new Dictionary<int, int>();

        for (var i = 0; i < route.Count; i++)
        {
            var stop = route[i];
            if (!packagesById.TryGetValue(stop.PackageId, out var package))
            {
                return false;
            }

            var isPickup = stop.Kind == StopKind.Pickup;
            var expectedLocation = isPickup ? package.Pickup : package.Delivery;
            var seen = isPickup ? pickupAt : deliveryAt;
            if (stop.Location != expectedLocation || !seen.TryAdd(stop.PackageId, i))
            {
                return false;
            }
        }

        return _packages.All(p =>
            pickupAt.TryGetValue(p.Id, out var pickup)
            && deliveryAt.TryGetValue(p.Id, out var delivery)
            && pickup < delivery);
    }

    public double DistanceBetween(string from, string to) => PathsFrom(from).DistanceTo(to);

    public List<string> PathBetween(string from, string to) => PathsFrom(from).PathTo(to);

    private ShortestPaths PathsFrom(string from)
    {
        if (!_paths.TryGetValue(from, out var paths))
        {
            throw new ArgumentException($"La ubicación {from} no es un punto de la ruta.");
        }

        return paths;
    }

    private List<Stop> ToStops(List<int> route)
    {
        var n = _packages.Count;
        return route
            .Select(task => task < n
                ? new Stop(_packages[task].Pickup, StopKind.Pickup, _packages[task].Id)
                : new Stop(_packages[task - n].Delivery, StopKind.Delivery, _packages[task - n].Id))
            .ToList();
    }
}
