using System.Globalization;

namespace Backend;

public readonly record struct CityMap(Graph<string> Graph, int StreetCount, int OneWayCount);

public static class CityFile
{
    public static CityMap Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No se encontró el archivo del mapa: {path}", path);
        }

        var graph = new Graph<string>(directed: true);
        var errors = new List<string>();
        var firstLineOfStreet = new Dictionary<(string, string), int>();
        var streetCount = 0;
        var oneWayCount = 0;
        var lines = File.ReadAllLines(path);

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length != 4)
            {
                errors.Add($"Línea {lineNumber}: se esperaban 4 campos y hay {fields.Length}.");
                continue;
            }

            var from = fields[0];
            var to = fields[1];
            var distanceText = fields[2];
            var direction = fields[3];
            var errorsBefore = errors.Count;

            if (from.Length == 0 || to.Length == 0)
            {
                errors.Add($"Línea {lineNumber}: falta el nombre de una ubicación.");
            }
            else if (from == to)
            {
                errors.Add($"Línea {lineNumber}: el origen y el destino son la misma ubicación ({from}).");
            }
            else if (!firstLineOfStreet.TryAdd(StreetKey(from, to), lineNumber))
            {
                var firstLine = firstLineOfStreet[StreetKey(from, to)];
                errors.Add($"Línea {lineNumber}: la calle {from}-{to} está repetida, ya aparece en la línea {firstLine}.");
            }

            if (!double.TryParse(distanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var meters)
                || !double.IsFinite(meters) || meters <= 0)
            {
                errors.Add($"Línea {lineNumber}: la distancia '{distanceText}' no es un número mayor que 0.");
            }

            var isTwoWay = direction.Equals("TwoWay", StringComparison.OrdinalIgnoreCase);
            var isOneWay = direction.Equals("OneWay", StringComparison.OrdinalIgnoreCase);
            if (!isTwoWay && !isOneWay)
            {
                errors.Add($"Línea {lineNumber}: el sentido '{direction}' no es TwoWay ni OneWay.");
            }

            if (errors.Count > errorsBefore)
            {
                continue;
            }

            graph.AddEdge(from, to, meters);
            if (isTwoWay)
            {
                graph.AddEdge(to, from, meters);
            }
            else
            {
                oneWayCount++;
            }
            streetCount++;
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"El archivo del mapa {path} tiene errores:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return new CityMap(graph, streetCount, oneWayCount);
    }

    // a street is the same in both directions
    private static (string, string) StreetKey(string from, string to) =>
        string.CompareOrdinal(from, to) < 0 ? (from, to) : (to, from);
}
