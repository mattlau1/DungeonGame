using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using Google.Protobuf;
using PlayerInfo = DungeonGame.Core.PlayerInfo;
using RoomSnapshot = DungeonGame.Core.RoomSnapshot;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class InMemoryRoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed class RoomChannel
    {
        public ConcurrentDictionary<Guid, Channel<ReadOnlyMemory<byte>>> SubscriberChannels { get; } = new();

        public ReadOnlyMemory<byte> CurrentState { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> SubscribeAsync(
        int subscriberPlayerId,
        int roomId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());

        Guid connectionId = Guid.NewGuid();

        var subscriberChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        room.SubscriberChannels.TryAdd(connectionId, subscriberChannel);

        if (!room.CurrentState.IsEmpty)
        {
            subscriberChannel.Writer.TryWrite(room.CurrentState);
        }

        try
        {
            await foreach (ReadOnlyMemory<byte> update in subscriberChannel.Reader.ReadAllAsync(ct))
            {
                yield return update;
            }
        }
        finally
        {
            room.SubscriberChannels.TryRemove(connectionId, out _);
            if (room.SubscriberChannels.IsEmpty)
            {
                _rooms.TryRemove(roomId, out _);
            }
        }
    }

    public Task PublishUpdateAsync(int roomId, RoomPlayerUpdate roomUpdate, CancellationToken ct)
    {
        var protoSnapshot = new RoomSnapshot { RoomId = roomId };
        foreach (PlayerSnapshot player in roomUpdate.Players)
        {
            protoSnapshot.Players.Add(
                new PlayerInfo
                {
                    Id = player.PlayerId,
                    Location = new DungeonGame.Shared.Location { X = player.Location.X, Y = player.Location.Y }
                });
        }

        byte[] data = protoSnapshot.ToByteArray();
        ReadOnlyMemory<byte> bytes = data;

        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.CurrentState = bytes;

        if (room.SubscriberChannels.IsEmpty)
        {
            return Task.CompletedTask;
        }

        foreach (Channel<ReadOnlyMemory<byte>> channel in room.SubscriberChannels.Values)
        {
            channel.Writer.TryWrite(bytes);
        }

        return Task.CompletedTask;
    }
}