using System.Globalization;
using System.Text;

namespace Backend;

public static class RoutePrinter
{
    public static string Format(RoutePlanner planner, string start, IReadOnlyList<Stop> route)
    {
        var text = new StringBuilder();
        var current = start;
        var total = 0.0;
        var number = 0;

        foreach (var stops in GroupByLocation(route))
        {
            number++;
            var location = stops[0].Location;
            var tasks = DescribeTasks(stops);

            if (location == current)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"{number,2}. En {location}: {tasks}.");
                continue;
            }

            var meters = planner.DistanceBetween(current, location);
            total += meters;
            text.AppendLine(CultureInfo.InvariantCulture, $"{number,2}. Ir de {current} a {location} ({meters:0.##} m): {tasks}.");
            text.AppendLine($"      Camino: {string.Join(" -> ", planner.PathBetween(current, location))}");
            current = location;
        }

        text.AppendLine();
        text.AppendLine("Todos los paquetes fueron recogidos y entregados.");
        text.AppendLine(CultureInfo.InvariantCulture, $"Recorrido total: {total / 1000:0.00} km");
        return text.ToString();
    }

    private static List<List<Stop>> GroupByLocation(IReadOnlyList<Stop> route)
    {
        var groups = new List<List<Stop>>();
        foreach (var stop in route)
        {
            if (groups.Count > 0 && groups[^1][0].Location == stop.Location)
            {
                groups[^1].Add(stop);
            }
            else
            {
                groups.Add([stop]);
            }
        }

        return groups;
    }

    private static string DescribeTasks(List<Stop> stops)
    {
        var parts = new List<string>();
        var deliveries = PackageIds(stops, StopKind.Delivery);
        if (deliveries.Count > 0)
        {
            parts.Add($"entregar {DescribePackages(deliveries)}");
        }

        var pickups = PackageIds(stops, StopKind.Pickup);
        if (pickups.Count > 0)
        {
            parts.Add($"recoger {DescribePackages(pickups)}");
        }

        return string.Join(" y ", parts);
    }

    private static List<int> PackageIds(List<Stop> stops, StopKind kind) =>
        stops.Where(s => s.Kind == kind).Select(s => s.PackageId).Order().ToList();

    private static string DescribePackages(List<int> ids)
    {
        if (ids.Count == 1)
        {
            return $"paquete {ids[0]}";
        }

        return $"paquetes {string.Join(", ", ids[..^1])} y {ids[^1]}";
    }
}
