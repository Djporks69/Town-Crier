using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace DiscordBot.Modules.ChatCraft
{
	class SlotDictionaryConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(Dictionary<Slot, ItemCount>).IsAssignableFrom(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			Dictionary<Slot, ItemCount> dictionary = new Dictionary<Slot, ItemCount>();

			JArray array = JToken.Load(reader) as JArray;
			
			if (array == null)
			{
				return dictionary;
			}

			foreach (JObject jObject in array)
			{
				JToken result;

				if (jObject.TryGetValue("Key", out result))
				{
					Slot slot = result.ToObject<Slot>(serializer);
					
					if (slot != null)
					{
						dictionary.Add(slot, jObject.GetValue("Value").ToObject<ItemCount>(serializer));
					}
				}
			}

			return dictionary;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			IDictionary<Slot, ItemCount> dictionary = (IDictionary<Slot, ItemCount>)value;

			JArray array = new JArray();

			foreach (var kvp in dictionary)
			{
				JObject child = new JObject();
				child.Add("Key", JToken.FromObject(kvp.Key, serializer));
				child.Add("Value", kvp.Value == null ? null : JToken.FromObject(kvp.Value, serializer));

				array.Add(child);
			}

			array.WriteTo(writer);
		}
	}
}
