namespace NitroxModel.Networking
{

    public class NitroxDeliveryMethod
    {
        public enum DeliveryMethod
        {
            UNRELIABLE_SEQUENCED,
            RELIABLE_ORDERED
        }

        public static LiteNetLib.DeliveryMethod ToLiteNetLib(DeliveryMethod deliveryMethod)
        {
            return deliveryMethod switch
            {
                DeliveryMethod.UNRELIABLE_SEQUENCED => LiteNetLib.DeliveryMethod.Sequenced,
                DeliveryMethod.RELIABLE_ORDERED => LiteNetLib.DeliveryMethod.ReliableOrdered,
                _ => LiteNetLib.DeliveryMethod.ReliableOrdered,
            };
        }

        public static DeliveryMethod ToNitrox(LiteNetLib.DeliveryMethod deliveryMethod)
        {
            return deliveryMethod switch
            {
                LiteNetLib.DeliveryMethod.Sequenced => DeliveryMethod.UNRELIABLE_SEQUENCED,
                LiteNetLib.DeliveryMethod.ReliableOrdered => DeliveryMethod.UNRELIABLE_SEQUENCED,
                _ => DeliveryMethod.RELIABLE_ORDERED,
            };
        }
    }
}
