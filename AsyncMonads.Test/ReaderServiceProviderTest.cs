using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncMonads.Test
{
    [TestFixture]
    public class ReaderServiceProviderTest
    {
        [Test]
        public static async Task BasicTest()
        {
            var sp = new MockServiceProvider();
            sp.RegisterInstance<IGreater>(new Greater());

            var result = await Greet("John").Apply(sp);

            Assert.AreEqual("Hello, John!", result);
        }

        private static async Reader<string> Greet(string userName)
        {
            var service = await Reader.GetService<IGreater>();
            return service.GreetUser(userName);
        }
    }

    public interface IGreater
    {
        string GreetUser(string userName);
    }

    public class Greater : IGreater
    {
        public string GreetUser(string userName)
            => $"Hello, {userName}!";
    }

    public class MockServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _storage = new Dictionary<Type, object>();

        public void RegisterInstance<T>(T instance)
            => this._storage.Add(typeof(T), instance);

        public object GetService(Type t) 
            => this._storage.TryGetValue(t, out var result)
                ? result
                : throw new Exception($"Type '{t.Name}' is not registered");
    }

}