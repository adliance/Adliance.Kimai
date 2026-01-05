using System.Text.Json;

namespace Adliance.Kimai.KimaiClient;

public class LenientJsonOptions
{
    public static readonly JsonSerializerOptions Instance;

#pragma warning disable CA1810
    static LenientJsonOptions()
#pragma warning restore CA1810
    {
        Instance = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        Instance.Converters.Add(LenientDateTimeConverter.Instance);
    }
}
