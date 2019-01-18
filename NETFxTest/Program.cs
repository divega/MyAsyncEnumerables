using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NETFxAsyncEnumerableTest
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
