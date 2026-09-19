using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services.Classroom
{
    public static class ClassroomProtocol
    {
        public const int DefaultPort = 5066;
        public const int MaxPayloadBytes = 2 * 1024 * 1024;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static byte[] Serialize(ClassroomSignalModel signal)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(signal, JsonOptions);
            if (payload.Length > MaxPayloadBytes)
            {
                throw new InvalidOperationException($"Classroom packet is too large: {payload.Length} bytes.");
            }

            byte[] packet = new byte[sizeof(int) + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(0, sizeof(int)), payload.Length);
            payload.CopyTo(packet.AsSpan(sizeof(int)));
            return packet;
        }

        public static async Task WriteMessageAsync(NetworkStream stream, ClassroomSignalModel signal, CancellationToken cancellationToken = default)
        {
            byte[] packet = Serialize(signal);
            await stream.WriteAsync(packet, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        public static async Task<ClassroomSignalModel?> ReadMessageAsync(NetworkStream stream, CancellationToken cancellationToken = default)
        {
            byte[] lengthBuffer = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);

            int payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            if (payloadLength <= 0 || payloadLength > MaxPayloadBytes)
            {
                throw new InvalidDataException($"Invalid classroom payload length: {payloadLength}.");
            }

            byte[] payload = new byte[payloadLength];
            await stream.ReadExactlyAsync(payload, cancellationToken);
            return JsonSerializer.Deserialize<ClassroomSignalModel>(payload, JsonOptions);
        }
    }
}
