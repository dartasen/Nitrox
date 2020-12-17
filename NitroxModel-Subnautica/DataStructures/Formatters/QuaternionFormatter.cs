using System;
using MessagePack;
using MessagePack.Formatters;
using UnityEngine;

namespace NitroxModel_Subnautica.DataStructures.Formatters
{
    public class QuaternionFormatter : IMessagePackFormatter<Quaternion>
    {
        public void Serialize(ref MessagePackWriter writer, Quaternion value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public Quaternion Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil)
            {
                throw new InvalidOperationException("typecode is null, struct not supported");
            }

            int length = reader.ReadArrayHeader();
            float x = default;
            float y = default;
            float z = default;
            float w = default;

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 0:
                        x = reader.ReadSingle();
                        break;
                    case 1:
                        y = reader.ReadSingle();
                        break;
                    case 2:
                        z = reader.ReadSingle();
                        break;
                    case 3:
                        w = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new Quaternion(x, y, z, w);
        }
    }
}
