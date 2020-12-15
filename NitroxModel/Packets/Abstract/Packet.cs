using System;
using NitroxModel.Networking;
using MessagePack;
using MessagePack.Resolvers;

namespace NitroxModel.Packets
{
    [Serializable]
    public abstract class Packet
    {
        private static readonly MessagePackSerializerOptions options;

        static Packet()
        {
            IFormatterResolver resolver = CompositeResolver.Create(
                // Enable surrogates first
                //MessagePack.Unity.Extension.UnityBlitResolver.Instance,
                MessagePack.Unity.UnityResolver.Instance,

                // finally use standard (default) resolver
                StandardResolver.Instance
            );

            options = MessagePackSerializerOptions.Standard
                .WithResolver(resolver)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
        }

        public NitroxDeliveryMethod.DeliveryMethod DeliveryMethod { get; protected set; } = NitroxDeliveryMethod.DeliveryMethod.RELIABLE_ORDERED;

        public byte[] Serialize()
        {
            return MessagePackSerializer.Serialize(this, options, default);
        }

        public static Packet Deserialize(byte[] data)
        {
            return MessagePackSerializer.Deserialize<Packet>(data, options, default);
        }

        public static bool IsTypeSerializable(Type type)
        {
            return true;
        }

        public WrapperPacket ToWrapperPacket()
        {
            return new WrapperPacket(Serialize());
        }
    }
}
