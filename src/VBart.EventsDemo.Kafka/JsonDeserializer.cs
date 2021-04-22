using System;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json.Linq;

namespace VBart.EventsDemo.Kafka
{
    public class JsonDeserializer : IDeserializer<JObject>
    {
        public JObject Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
            {
                return new JObject();
            }

            var str = Encoding.UTF8.GetString(data.ToArray());
            return string.IsNullOrWhiteSpace(str) ? new JObject(): JObject.Parse(str);
        }

    }
}