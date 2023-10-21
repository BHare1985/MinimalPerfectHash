using System;
using System.Collections;
using System.Linq;
using System.Text;
using MinimalPerfectHash;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public class UnitTest1
{
    private readonly ITestOutputHelper _testOutputHelper;

    public UnitTest1(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Test1()
    {
        const double loadFactor = 1.0d;

        // Create a unique string generator
        const int keyCount = 2_000_000;

        // Derive a minimum perfect hash function
        _testOutputHelper.WriteLine("Generating minimum perfect hash function for {0} keys", keyCount);
        var start = DateTime.Now;
        var hashFunction = new MphFunction(Enumerable.Range(0, keyCount).Select(GetKeyBytes), keyCount, loadFactor);

        _testOutputHelper.WriteLine("Completed in {0:0.000000} s", DateTime.Now.Subtract(start).TotalMilliseconds / 1000.0);

        // Show the extra hash space necessary
        _testOutputHelper.WriteLine("Hash function map {0} keys to {1} hashes (load factor: {2:0.000000}%)",
            keyCount, hashFunction.MaxValue,
            keyCount * 100 / (double)hashFunction.MaxValue);

        // Check for any collision
        var used = new BitArray((int)hashFunction.MaxValue);

        start = DateTime.Now;
        for (var test = 0U; test < keyCount; test++)
        {
            var hash = (int)hashFunction.GetHash(GetKeyBytes((int)test));
            if (used[hash]) Assert.True(false, $"FAILED - Collision detected at {test}");
            used[hash] = true;
        }

        var end = DateTime.Now.Subtract(start).TotalMilliseconds;
        _testOutputHelper.WriteLine("PASS - No collision detected");

        _testOutputHelper.WriteLine("Total scan time : {0:0.000000} s", end / 1000.0);
        _testOutputHelper.WriteLine("Average key hash time : {0} ms", end / keyCount);
    }

    private static byte[] GetKeyBytes(int i)
    {
        return Encoding.UTF8.GetBytes($"KEY-{i}");
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

        for (var i = 0; i < keyCount; i++)
        {
            var key = $"KEY-{i}";
            var hash = hashFunction2.GetHash(Encoding.UTF8.GetBytes(key));
            Assert.Equal(key, table[hash]);
        }
    }
}