using System.Collections;

namespace ZeroPlus.Oms
{
    public class ServiceLocator
    {
        private static readonly Hashtable _services = new();

        private class Nested
        {
            internal static readonly ServiceLocator Instance = new();
        }

        public static ServiceLocator Instance => Nested.Instance;

        private ServiceLocator()
        {
        }

        public ServiceLocator AddService<T>(T t)
        {
            _services.Add(typeof(T).Name, t);
            return this;
        }

        public ServiceLocator AddService<T>(string name, T t)
        {
            _services.Add(name, t);
            return this;
        }

        public ServiceLocator AddService<T>() where T : new()
        {
            _services.Add(typeof(T).Name, new T());
            return this;
        }

        public static T GetService<T>()
        {
            return (T)_services[typeof(T).Name];
        }

        public static T GetService<T>(string serviceName)
        {
            return (T)_services[serviceName];
        }
    }
}
