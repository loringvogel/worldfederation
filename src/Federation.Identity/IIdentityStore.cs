using Federation.Protocol;

namespace Federation.Identity;

/// <summary>Stores device identities and peer records.</summary>
public interface IIdentityStore
{
    Task StoreDeviceIdentityAsync(DeviceIdentity identity, CancellationToken ct = default);
    Task<DeviceIdentity?> LoadDeviceIdentityAsync(DeviceId deviceId, CancellationToken ct = default);
    Task StorePeerRecordAsync(PeerRecord peerRecord, CancellationToken ct = default);
    Task<PeerRecord?> GetPeerRecordAsync(DeviceId deviceId, CancellationToken ct = default);
    Task<IReadOnlyList<PeerRecord>> GetPeersForRoomAsync(RoomId roomId, CancellationToken ct = default);
    Task StorePeerRecordForRoomAsync(RoomId roomId, PeerRecord record, CancellationToken ct = default);
}
