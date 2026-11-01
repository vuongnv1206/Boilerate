namespace Boilerate.Host.Configurations;

/// <summary>Separate one-shot operator arguments from ASP.NET configuration arguments.</summary>
public static class OperatorArguments
{
    private static readonly string[] Flags =
        ["--bootstrap-admin", "--register-capabilities", "--grant-admin-capabilities", "--preflight", "--seed-dev"];

    public static string[] ConfigurationArguments(string[] args)
    {
        if (args.Contains("--seed-dev") && args.Any(argument => argument != "--seed-dev" && Flags.Contains(argument)))
            throw new InvalidOperationException("Run --seed-dev separately from foundation bootstrap/preflight commands.");
        var filters = args.Select((argument, index) => (argument, index)).Where(x => x.argument == "--function").ToArray();
        if (filters.Length > 0 &&
            (filters.Length != 1 || filters[0].index + 1 >= args.Length || args[filters[0].index + 1].StartsWith("--", StringComparison.Ordinal) ||
             args.Contains("--bootstrap-admin") || args.Contains("--preflight") || args.Contains("--seed-dev") ||
             (!args.Contains("--register-capabilities") && !args.Contains("--grant-admin-capabilities"))))
            throw new InvalidOperationException("Use --function CODE only with capability registration/grant commands.");
        var filterIndex = filters.Length == 1 ? filters[0].index : -2;
        return args.Where((argument, index) => !Flags.Contains(argument) && index != filterIndex && index != filterIndex + 1).ToArray();
    }
}
