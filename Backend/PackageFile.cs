using System.Globalization;

namespace Backend;

public static class PackageFile
{
    public static List<Package> Load(string path, Graph<string> city)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No se encontró el archivo de paquetes: {path}", path);
        }

        var errors = new List<string>();
        var pickups = new Dictionary<int, (string Location, int Line)>();
        var deliveries = new Dictionary<int, (string Location, int Line)>();
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
            if (fields.Length != 3)
            {
                errors.Add($"Línea {lineNumber}: se esperaban 3 campos y hay {fields.Length}.");
                continue;
            }

            var location = fields[0];
            var action = fields[1];
            var packageText = fields[2];
            var errorsBefore = errors.Count;

            if (location.Length == 0)
            {
                errors.Add($"Línea {lineNumber}: falta la ubicación.");
            }
            else if (!city.Contains(location))
            {
                errors.Add($"Línea {lineNumber}: la ubicación {location} no está en el mapa.");
            }

            var isPickup = action.Equals("Pickup", StringComparison.OrdinalIgnoreCase);
            var isDelivery = action.Equals("Delivery", StringComparison.OrdinalIgnoreCase);
            if (!isPickup && !isDelivery)
            {
                errors.Add($"Línea {lineNumber}: la acción '{action}' no es Pickup ni Delivery.");
            }

            if (!int.TryParse(packageText, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            {
                errors.Add($"Línea {lineNumber}: el paquete '{packageText}' no es un número entero mayor que 0.");
            }

            if (errors.Count > errorsBefore)
            {
                continue;
            }

            var tasks = isPickup ? pickups : deliveries;
            if (!tasks.TryAdd(id, (location, lineNumber)))
            {
                var kind = isPickup ? "recogidas" : "entregas";
                errors.Add($"Línea {lineNumber}: el paquete {id} tiene dos {kind}, la otra está en la línea {tasks[id].Line}.");
            }
        }

        var ids = pickups.Keys.Union(deliveries.Keys).Order().ToList();
        foreach (var id in ids)
        {
            if (!pickups.ContainsKey(id))
            {
                errors.Add($"El paquete {id} no tiene recogida.");
            }
            if (!deliveries.ContainsKey(id))
            {
                errors.Add($"El paquete {id} no tiene entrega.");
            }
        }

        if (ids.Count == 0 && errors.Count == 0)
        {
            errors.Add("El archivo no tiene paquetes.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"El archivo de paquetes {path} tiene errores:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return ids.Select(id => new Package(id, pickups[id].Location, deliveries[id].Location)).ToList();
    }
}
