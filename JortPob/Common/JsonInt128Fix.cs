using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JortPob.Common
{
    using System;
    using System.Numerics;
    using Newtonsoft.Json;

    public class Int128Converter : JsonConverter<Int128>
    {
        public override void WriteJson(JsonWriter writer, Int128 value, JsonSerializer serializer)
        {
            // Output as a raw string or number representation
            writer.WriteValue(value.ToString());
        }

        public override Int128 ReadJson(JsonReader reader, Type objectType, Int128 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is null)
            {
                return default;
            }

            // Handle when Newtonsoft parses the token as BigInteger
            if (reader.Value is BigInteger bigInt)
            {
                return (Int128)bigInt;
            }

            // Handle string or standard numeric tokens
            if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.String)
            {
                return Int128.Parse(reader.Value.ToString()!);
            }

            throw new JsonSerializationException($"Cannot convert {reader.TokenType} ({reader.Value}) to System.Int128.");
        }
    }
}
