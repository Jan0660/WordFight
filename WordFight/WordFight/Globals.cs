using System.Text.Json;

namespace WordFight;

public static class Globals
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    public static DataFile Data { get; set; }
}