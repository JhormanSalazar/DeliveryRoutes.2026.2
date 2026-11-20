namespace Backend;

public class RoutePlanner
{
    private const int Seed = 2026;
    private const int Restarts = 400;
    private const double Epsilon = 1e-9;

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

    public List<Stop> Plan()
    {
        var n = _packages.Count;
        var random = new Random(Seed);
        var order = Enumerable.Range(0, n).ToArray();
        List<int> best = [];
        var bestCost = double.PositiveInfinity;

        for (var attempt = 0; attempt < Restarts; attempt++)
        {
            if (attempt > 0)
            {
                random.Shuffle(order);
            }

            var route = new List<int>(2 * n);
            foreach (var p in order)
            {
                CheapestInsertion(route, p);
            }

            Improve(route);
            var cost = Cost(route);
            if (cost < bestCost - Epsilon)
            {
                best = route;
                bestCost = cost;
            }
        }

        var stops = ToStops(best);
        if (!IsValid(stops))
        {
            throw new InvalidOperationException("La ruta calculada no es válida.");
        }

        return stops;
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

    private void CheapestInsertion(List<int> route, int p)
    {
        var m = route.Count;
        var pickup = _taskPoint[p];
        var delivery = _taskPoint[p + _packages.Count];
        var bestPickupGap = 0;
        var bestDeliveryGap = 0;
        var bestIncrease = double.PositiveInfinity;

        void Consider(int pickupGap, int deliveryGap, double increase)
        {
            if (increase < bestIncrease)
            {
                bestPickupGap = pickupGap;
                bestDeliveryGap = deliveryGap;
                bestIncrease = increase;
            }
        }

        for (var i = 0; i <= m; i++)
        {
            var a = PointBefore(route, i);
            if (i == m)
            {
                // both go after the last stop
                Consider(i, i, _legs[a, pickup] + _legs[pickup, delivery]);
                continue;
            }

            var b = _taskPoint[route[i]];
            // delivery right after the pickup splits a single leg
            Consider(i, i, _legs[a, pickup] + _legs[pickup, delivery] + _legs[delivery, b] - _legs[a, b]);

            var pickupIncrease = _legs[a, pickup] + _legs[pickup, b] - _legs[a, b];
            for (var j = i + 1; j < m; j++)
            {
                var c = _taskPoint[route[j - 1]];
                var e = _taskPoint[route[j]];
                // pickup and delivery each split their own leg
                Consider(i, j, pickupIncrease + _legs[c, delivery] + _legs[delivery, e] - _legs[c, e]);
            }

            // the pickup splits a leg and the delivery goes after the last stop
            Consider(i, m, pickupIncrease + _legs[_taskPoint[route[m - 1]], delivery]);
        }

        route.Insert(bestDeliveryGap, p + _packages.Count);
        route.Insert(bestPickupGap, p);
    }

    private void Improve(List<int> route)
    {
        bool improved;
        do
        {
            var movedPackages = RelocatePackages(route);
            var movedStops = RelocateStops(route);
            improved = movedPackages || movedStops;
        }
        while (improved);
    }

    private bool RelocatePackages(List<int> route)
    {
        var n = _packages.Count;
        var improvedAny = false;
        bool improved;
        do
        {
            improved = false;
            for (var p = 0; p < n; p++)
            {
                var candidate = new List<int>(route);
                candidate.Remove(p);
                candidate.Remove(p + n);
                CheapestInsertion(candidate, p);

                if (Cost(candidate) < Cost(route) - Epsilon)
                {
                    route.Clear();
                    route.AddRange(candidate);
                    improved = true;
                    improvedAny = true;
                }
            }
        }
        while (improved);

        return improvedAny;
    }

    private bool RelocateStops(List<int> route)
    {
        var n = _packages.Count;
        var improvedAny = false;
        bool improved;
        do
        {
            improved = false;
            for (var k = 0; k < route.Count; k++)
            {
                var task = route[k];
                var point = _taskPoint[task];
                route.RemoveAt(k);
                var removalGain = InsertionIncrease(route, k, point);

                // a pickup stays before its delivery and a delivery after its pickup
                var partnerAt = route.IndexOf(task < n ? task + n : task - n);
                var firstGap = task < n ? 0 : partnerAt + 1;
                var lastGap = task < n ? partnerAt : route.Count;

                var bestGap = k;
                var bestIncrease = removalGain - Epsilon;
                for (var gap = firstGap; gap <= lastGap; gap++)
                {
                    var increase = InsertionIncrease(route, gap, point);
                    if (increase < bestIncrease)
                    {
                        bestGap = gap;
                        bestIncrease = increase;
                    }
                }

                route.Insert(bestGap, task);
                if (bestGap != k)
                {
                    improved = true;
                    improvedAny = true;
                }
            }
        }
        while (improved);

        return improvedAny;
    }

    private double InsertionIncrease(List<int> route, int gap, int point)
    {
        var a = PointBefore(route, gap);
        if (gap == route.Count)
        {
            return _legs[a, point];
        }

        var b = _taskPoint[route[gap]];
        return _legs[a, point] + _legs[point, b] - _legs[a, b];
    }

    private int PointBefore(List<int> route, int gap) => gap == 0 ? _startPoint : _taskPoint[route[gap - 1]];

    private double Cost(List<int> route)
    {
        var total = 0.0;
        var current = _startPoint;
        foreach (var task in route)
        {
            total += _legs[current, _taskPoint[task]];
            current = _taskPoint[task];
        }

        return total;
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
