using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace MPSkins;

/// <summary>Sent when a player changes their skin selection.</summary>
public struct SkinChangedMessage : INetMessage, IPacketSerializable
{
    public string skinName;
    public ulong playerId;

    public bool ShouldBroadcast => true;
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer) { writer.WriteString(skinName); writer.WriteULong(playerId); }
    public void Deserialize(PacketReader reader) { skinName = reader.ReadString(); playerId = reader.ReadULong(); }
}
