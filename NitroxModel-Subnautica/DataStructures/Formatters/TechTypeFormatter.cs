using System;
using MessagePack;
using MessagePack.Formatters;
using NitroxModel.DataStructures.GameLogic;

namespace NitroxModel_Subnautica.DataStructures.Formatters
{
    public class TechTypeFormatter : IMessagePackFormatter<NitroxTechType>
    {
        public void Serialize(ref MessagePackWriter writer, NitroxTechType value, MessagePackSerializerOptions options)
        {
            writer.Write(value.Name);
        }
        public NitroxTechType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil)
            {
                throw new InvalidOperationException("typecode is null, struct not supported");
            }

            string name = reader.ReadString();

            return new NitroxTechType(name);
        }


    }
}
