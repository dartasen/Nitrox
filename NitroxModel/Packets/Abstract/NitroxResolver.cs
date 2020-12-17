using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using NitroxModel.Logger;

namespace NitroxModel.Packets
{
    public class NitroxResolver : IFormatterResolver
    {
        public static readonly NitroxResolver Instance;

        private static readonly Dictionary<Type, object> formatterMap;

        static NitroxResolver()
        {
            Instance = new NitroxResolver();
            formatterMap = new Dictionary<Type, object>();

            IEnumerable<Type> types = AppDomain.CurrentDomain
                                    .GetAssemblies()
                                    .Where(assembly => assembly.GetName().Name.Contains("NitroxModel-Subnautica"))
                                    .SelectMany(a => a.GetTypes()
                                    .Where(t =>
                                          t.BaseType != null
                                          && t.BaseType.IsGenericType
                                          && t.BaseType.GetGenericTypeDefinition() == typeof(IMessagePackFormatter<>)
                                          && t.IsClass
                                          && !t.IsAbstract)
                                    );

            foreach (Type type in types)
            {
                IMessagePackFormatter surrogate = Activator.CreateInstance<IMessagePackFormatter>();
                Type surrogatedType = type.BaseType.GetGenericArguments()[0];

                formatterMap.Add(surrogatedType, surrogate);
                Log.Debug($"Added surrogate {surrogate.GetType().Name} for type {surrogatedType}");
            }
        }

        private NitroxResolver()
        {

        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (formatterMap.TryGetValue(typeof(T), out object formatter))
            {
                return (IMessagePackFormatter<T>) formatter;
            }

            return null;
        }
    }
}
