using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MinimalPerfectHash;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Test;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        const double loadFactor = 1.0d;

        // Create a unique string generator
        const int KeyCount = 2_000_000;

        // Derivate a minimum perfect hash function
        Console.WriteLine("Generating minimum perfect hash function for {0} keys", KeyCount);
        var start = DateTime.Now;
        var hashFunction = new MphFunction(Enumerable.Range(0, KeyCount).Select(GetKeyBytes), KeyCount, loadFactor);

        Console.WriteLine("Completed in {0:0.000000} s", DateTime.Now.Subtract(start).TotalMilliseconds / 1000.0);

        // Show the extra hash space necessary
        Console.WriteLine("Hash function map {0} keys to {1} hashes (load factor: {2:0.000000}%)",
            KeyCount, hashFunction.MaxValue,
            KeyCount * 100 / (double)hashFunction.MaxValue);

        // Check for any collision
        var used = new BitArray((int)hashFunction.MaxValue);

        start = DateTime.Now;
        for (var test = 0U; test < KeyCount; test++)
        {
            var hash = (int)hashFunction.GetHash(GetKeyBytes((int)test));
            if (used[hash]) Assert.True(false, $"FAILED - Collision detected at {test}");
            used[hash] = true;
        }

        var end = DateTime.Now.Subtract(start).TotalMilliseconds;
        Console.WriteLine("PASS - No collision detected");

        Console.WriteLine("Total scan time : {0:0.000000} s", end / 1000.0);
        Console.WriteLine("Average key hash time : {0} ms", end / KeyCount);

        var dic = new Dictionary<int, string>(KeyCount);
        for (var i = 0; i < KeyCount; i++) dic.Add(i, i.ToString());
        var mphrod = new MphReadOnlyDictionary<int, string>(dic, GetKeyBytes, loadFactor);
        for (var i = 0; i < KeyCount; i++) Assert.Equal(dic[i], mphrod[i]);
        Assert.Equal(dic.OrderBy(x => x.Key), mphrod.OrderBy(x => x.Key));
        Assert.Equal(dic.Keys.OrderBy(x => x), mphrod.Keys.OrderBy(x => x));
        Assert.Equal(dic.Values.OrderBy(x => x), mphrod.Values.OrderBy(x => x));
    }

    private byte[] GetKeyBytes(int i)
    {
        return Encoding.UTF8.GetBytes($"KEY-{i}");
    }

    [Fact]
    public void OutOfRangeKeysThatCollideOnHashFunctionFailEqualityCheck()
    {
        const int keyCount = 2_000_000;
        var dic = new Dictionary<int, string>(keyCount);
        for (var i = 0; i < keyCount; i++) dic.Add(i, i.ToString());
        var mphrod = new MphReadOnlyDictionary<int, string>(dic, GetKeyBytes);
        for (var i = 0; i < keyCount; i++) Assert.False(mphrod.TryGetValue(i + keyCount, out var x));
    }

    [Fact]
    public void HashFunctionSerialization()
    {
        const int keyCount = 20_000;
        var hashFunction = new MphFunction(Enumerable.Range(0, keyCount).Select(GetKeyBytes), keyCount, 1);
        var table = new string[hashFunction.MaxValue];
        for (var i = 0; i < keyCount; i++)
        {
            var key = $"KEY-{i}";
            var hash = hashFunction.GetHash(Encoding.UTF8.GetBytes(key));
            table[hash] = key;
        }

        var bytes = hashFunction.Dump();
        var hashFunction2 = MphFunction.Load(bytes);

        var settings = new JsonSerializerSettings
        {
            ContractResolver = NonPublicFieldContractResolver.Instance,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };
        var json = JsonConvert.SerializeObject(hashFunction, settings);
        //var hashFunction2 = JsonConvert.DeserializeObject<MphFunction>(json, settings);
        for (var i = 0; i < keyCount; i++)
        {
            var key = $"KEY-{i}";
            var hash = hashFunction2.GetHash(Encoding.UTF8.GetBytes(key));
            Assert.Equal(key, table[hash]);
        }
    }

    [Fact]
    public void MphReadOnlyDictionarySerialization()
    {
        const int keyCount = 20_000;
        var enumerable = Enumerable.Range(0, keyCount).Select(x => new KeyValuePair<int, string>(x, $"KEY-{x}"));
        var dictionary = new MphReadOnlyDictionary<int, string>(enumerable, GetKeyBytes);

        var settings = new JsonSerializerSettings
        {
            ContractResolver = NonPublicFieldContractResolver.Instance,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };
        var json = JsonConvert.SerializeObject(dictionary, MphReadOnlyDictionaryConverter<int, string>.Instance);
        var dictionary2 =
            JsonConvert.DeserializeObject<MphReadOnlyDictionary<int, string>>(json,
                MphReadOnlyDictionaryConverter<int, string>.Instance);
        for (var i = 0; i < keyCount; i++)
            Assert.Equal(dictionary[i], dictionary2[i]);
    }

    public class MphReadOnlyDictionaryConverter<TKey, TValue> : JsonConverter<MphReadOnlyDictionary<TKey, TValue>>
    {
        private MphReadOnlyDictionaryConverter()
        {
        }

        public static MphReadOnlyDictionaryConverter<TKey, TValue> Instance { get; } = new();

        public override void WriteJson(JsonWriter writer, MphReadOnlyDictionary<TKey, TValue> value,
            JsonSerializer serializer)
        {
            var jsonSerializer = new JsonSerializer
            {
                ContractResolver = NonPublicFieldContractResolver.Instance,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            };
            var table = JToken.FromObject(value);
            var mphFunction = JToken.FromObject(value.MphFunction, jsonSerializer);

            writer.WriteStartObject();
            {
                writer.WritePropertyName(value.GetType().Name);
                writer.WriteStartObject();
                {
                    writer.WritePropertyName("Table");
                    table.WriteTo(writer);
                    writer.WritePropertyName("MphFunction");
                    mphFunction.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        public override MphReadOnlyDictionary<TKey, TValue> ReadJson(JsonReader reader, Type objectType,
            MphReadOnlyDictionary<TKey, TValue> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return null;
        }
    }

    public class NonPublicFieldContractResolver : DefaultContractResolver
    {
        private NonPublicFieldContractResolver()
        {
            IgnoreSerializableAttribute = true;
            IgnoreSerializableInterface = true;
        }

        public static NonPublicFieldContractResolver Instance { get; } = new();

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var types = GetInheritanceChain(type);
            const BindingFlags BindingAttr = BindingFlags.NonPublic | BindingFlags.Instance;
            var fieldInfos = types.SelectMany(x => x.GetFields(BindingAttr));
            return fieldInfos.Select(x => Selector(x, memberSerialization)).ToList();
        }

        private static IEnumerable<Type> GetInheritanceChain(Type type, Type terminator = null)
        {
            if (terminator?.IsAssignableFrom(type) == false)
                throw new ArgumentOutOfRangeException(nameof(terminator),
                    "Terminator type must be in the inheritance chain");
            do
            {
                yield return type;
            } while ((type = type.BaseType) != terminator);
        }

        private JsonProperty Selector(MemberInfo p, MemberSerialization memberSerialization)
        {
            var jsonProperty = base.CreateProperty(p, memberSerialization);
            jsonProperty.Writable = true;
            jsonProperty.Readable = true;
            return jsonProperty;
        }
    }
}