using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncMonads.Test
{
    [TestFixture]
    public class MaybeTest
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
                var res = await GetResult(input, async).GetMaybeResult();
                acc.Add(res.IsNothing ? (int?)null : res.GetValue());
            }
            CollectionAssert.AreEqual(expected, acc);
        }

        private static async Maybe<int> GetResult(string input, bool async)
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

        private static async Maybe<(string arg1, string arg2)> SplitString(string str, bool async)
        {
            if (async)
            {
                await Task.Delay(10);
            }

            str = await ValidateString(str);

            var arr = str?.Split(',');
            if (arr == null || arr.Length != 2)
            {
                return await Maybe<(string arg1, string arg2)>.Nothing();
            }

            return (arr[0].Trim(), arr[1].Trim());
        }

        public static Maybe<string> ValidateString(string s)
            => string.IsNullOrWhiteSpace(s) || !s.Contains(',')
                ? Maybe<string>.Nothing()
                : s;

        public static Maybe<int> TryParse(string argStr)
            => int.TryParse(argStr, out var result)
                ? result
                : Maybe<int>.Nothing();
    }
}
