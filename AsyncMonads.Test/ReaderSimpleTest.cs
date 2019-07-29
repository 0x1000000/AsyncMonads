using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncMonads.Test
{
    [TestFixture]
    public class ReaderSimpleTest
    {
        class Config { public string Template; }

        [Test]
        public static async Task Main()
        {
            Console.WriteLine(await GreetGuys().Apply(new Config {Template = "Hi, {0}!"}));
            //(Hi, John!, Hi, Jose!)

            Console.WriteLine(await GreetGuys().Apply(new Config {Template = "¡Hola, {0}!" }));
            //(¡Hola, John!, ¡Hola, Jose!)
        }

        //These functions do not have any link to any instance of the Config class.
        public static async Reader<(string gJohn, string gJose)> GreetGuys() 
            => (await Greet("John"), await Greet("Jose"));

        static async Reader<string> Greet(string name) 
            => string.Format(await ExtractTemplate(), name);

        static async Reader<string> ExtractTemplate() 
            => await Reader<string>.Read<Config>(c => c.Template);
    }
}