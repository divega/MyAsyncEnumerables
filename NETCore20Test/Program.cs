using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dev16Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await foreach (var x in MyIterator())
            {
                Console.WriteLine($"The answer is {x}");
            }
        }

        public static async IAsyncEnumerable<int> MyIterator()
        {
            await Task.Delay(1000);
            yield return 42;
        }
    }
}


