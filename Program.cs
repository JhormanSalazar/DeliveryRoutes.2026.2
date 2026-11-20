using Backend;

namespace DeliveryRoutes;

internal static class Program
{
    private static int Main(string[] args)
    {
        // console needs utf8 for the accented output
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        try
        {
            var input = ReadArguments(args);
            if (input is null)
            {
                Console.Error.WriteLine("Uso: dotnet run -- <archivo del mapa> <archivo de paquetes> <posición inicial>");
                Console.Error.WriteLine("Ejemplo: dotnet run -- city.txt packages.txt D");
                return 1;
            }

            var cityPath = input[0];
            var packagesPath = input[1];
            var start = input[2];

            var map = CityFile.Load(cityPath);
            if (!map.Graph.Contains(start))
            {
                Console.Error.WriteLine($"La posición inicial {start} no está en el mapa.");
                return 1;
            }

            var packages = PackageFile.Load(packagesPath, map.Graph);
            var planner = new RoutePlanner(map.Graph, packages, start);

            PrintHeader(map, start, packages.Count);
            Console.WriteLine();

            var route = planner.Plan();
            Console.Write(RoutePrinter.Format(planner, start, route));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                       or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string[]? ReadArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return [Ask("Archivo del mapa: "), Ask("Archivo de paquetes: "), Ask("Posición inicial: ")];
        }

        return args.Length == 3 ? args : null;
    }

    private static string Ask(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static void PrintHeader(CityMap map, string start, int packageCount)
    {
        Console.WriteLine($"Posición inicial: {start}");
        Console.WriteLine($"Paquetes: {packageCount}   Ubicaciones: {map.Graph.VertexCount}   Calles: {map.StreetCount} ({map.OneWayCount} de un solo sentido)");
    }
}
