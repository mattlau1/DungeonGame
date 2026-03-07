using System.Buffers;
using DungeonGame.Core;
using Google.Protobuf;
using Grpc.Core;

namespace DungeonServer.Service.CustomMarshallers;

public static class RoomSnapshotMarshaller
{
    public sealed class SnapshotSource
    {
        public RoomSnapshot? Instance { get; set; }
        public ReadOnlyMemory<byte> Raw { get; set; }

        public void Update(ReadOnlyMemory<byte> raw)
        {
            Raw = raw;
            Instance = null;
        }
    }

    public static readonly Marshaller<SnapshotSource> Marshaller = Marshallers.Create<SnapshotSource>(
        (payload, context) =>
        {
            ReadOnlyMemory<byte> data = !payload.Raw.IsEmpty ? payload.Raw : payload.Instance!.ToByteArray();

            context.SetPayloadLength(data.Length);

            Span<byte> buffer = context.GetBufferWriter().GetSpan(data.Length);
            data.Span.CopyTo(buffer);
            context.GetBufferWriter().Advance(data.Length);

            context.Complete();
        },
        context =>
        {
            ReadOnlySequence<byte> sequence = context.PayloadAsReadOnlySequence();

            return new SnapshotSource { Instance = RoomSnapshot.Parser.ParseFrom(sequence) };
        });
}