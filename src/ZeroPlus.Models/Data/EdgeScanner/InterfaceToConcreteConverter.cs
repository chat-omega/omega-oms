using Newtonsoft.Json;
using System;

namespace ZeroPlus.Models.Data.EdgeScanner;

public class InterfaceToConcreteConverter<TInterface, TConcrete> : JsonConverter where TConcrete : TInterface
{
    static InterfaceToConcreteConverter()
    {
        if (typeof(TInterface) == typeof(TConcrete))
        {
            throw new InvalidOperationException($"typeof({typeof(TInterface)}) == typeof({typeof(TConcrete)})");
        }
    }

    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType) => objectType == typeof(TInterface);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        return serializer.Deserialize(reader, typeof(TConcrete));
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}