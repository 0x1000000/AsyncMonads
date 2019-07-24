using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncMonads.Test
{
    [TestFixture]
    public class MaybeExTest
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task Main(bool async)
        {
            string[] inputs = { null, " 7 , 3", "100", "11,14", "8,a" };
            int?[] expected = {null, 10, null, 25, null};

            var acc = new List<int?>();
            foreach (var input in inputs)
            {
                MockDisposable.DisposingCounter = 0;
                var res = await GetResult(input, async).GetMaybeResult();
                acc.Add(res.IsNothing ? (int?)null : res.GetValue());
                Assert.AreEqual(1, MockDisposable.DisposingCounter);
            }
            CollectionAssert.AreEqual(expected, acc);
        }

        private static async MaybeEx<int> GetResult(string input, bool async)
        {
            using (new MockDisposable())
            {
                if (async)
                {
                    await Task.Delay(10);
                }

                var args = await SplitString(input, async);
                var a1 = await TryParse(args.arg1);
                var a2 = await TryParse(args.arg2);
                return a1 + a2;
            }
        }

        private static async MaybeEx<(string arg1, string arg2)> SplitString(string str, bool async)
        {
            if (async)
            {
                await Task.Delay(10);
            }

            str = await ValidateString(str);

            var arr = str?.Split(',');
            if (arr == null || arr.Length != 2)
            {
                return await MaybeEx<(string arg1, string arg2)>.Nothing();
            }

            return (arr[0].Trim(), arr[1].Trim());
        }

        public static MaybeEx<string> ValidateString(string s)
            => string.IsNullOrWhiteSpace(s) || !s.Contains(',')
                ? MaybeEx<string>.Nothing()
                : s;

        public static MaybeEx<int> TryParse(string argStr)
            => int.TryParse(argStr, out var result)
                ? result
                : MaybeEx<int>.Nothing();


        private class MockDisposable : IDisposable
        {
            public static int DisposingCounter;

            public void Dispose()
            {
                DisposingCounter++;
            }
        }
    }
}
