using System.Text.Json;
using System.Text.Json.Serialization;

namespace WordFight;

public class DataFile
{
    public required Word[] Words { get; init; }
}

public record Word(string Text, VerbClass Class)
{
    public static readonly string[] VerbClasses = Enum.GetNames(typeof(VerbClass)).Select(c => c.ToLowerInvariant()).Take(4).ToArray();
    
    // public required string Text { get; init; }
    // public required VerbClass Class { get; init; }
    public string? Irregular { get; init; }
    public string[]? IncorrectIrregular { get; init; }

    public string[] GetOptions()
    {
        var irregularCount = (Irregular == null ? 0 : 1) + (IncorrectIrregular?.Length ?? 0);
        var options = new string[irregularCount + VerbClasses.Length];
        Array.Copy(VerbClasses, options, VerbClasses.Length);
        // add and randomize the irregular options
        if (irregularCount > 0)
        {
            var irregularOptions = new string[irregularCount];
            var index = 0;
            if (Irregular != null)
            {
                irregularOptions[index] = Irregular;
                index++;
            }
            if (IncorrectIrregular != null)
            {
                Array.Copy(IncorrectIrregular, 0, irregularOptions, index, IncorrectIrregular.Length);
            }
            irregularOptions = irregularOptions.OrderBy(_ => Random.Shared.Next()).ToArray();
            Array.Copy(irregularOptions, 0, options, VerbClasses.Length, irregularOptions.Length);
        }
        return options;
    }
}

[JsonConverter(typeof(JsonLowercaseStringEnumConverter<VerbClass>))]
public enum VerbClass
{
    Eingekauft,
    Verkauft,
    Gekauft,
    Diskutiert,
    Irregular,
}

public class JsonLowercaseStringEnumConverter<T> : JsonConverter<T>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value == null)
        {
            return default;
        }
        return (T)Enum.Parse(typeToConvert, value, true);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value!.ToString()!.ToLowerInvariant());
    }

    // public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    // {
    //     return (JsonConverter)Activator.CreateInstance(
    //         GetEnumConverterType(typeToConvert))!;
    // }
    //
    // private static Type GetEnumConverterType(Type enumType)
    // {
    //     return typeof(JsonLowercaseStringEnumConverter<>).MakeGenericType(enumType);
    // }
}