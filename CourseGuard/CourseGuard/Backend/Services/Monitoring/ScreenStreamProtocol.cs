using System;
using System.Buffers.Binary;
using System.IO;

namespace CourseGuard.Backend.Services.Monitoring
{
    public static class ScreenStreamProtocol
    {
        public const int DefaultPort = 5055;
        public const int MaxFrameBytes = 5 * 1024 * 1024;
        public static readonly byte[] Magic = { (byte)'C', (byte)'G', (byte)'S', (byte)'F' };
        public const byte Version = 1;
        public const int HeaderLength = 30; // Updated from 29 to 30 to include FrameType

        public static byte[] BuildHeader(byte frameType, int examId, int studentId, int attemptId, long unixMillis, int jpegLength)
        {
            var header = new byte[HeaderLength];
            Magic.CopyTo(header, 0);
            header[4] = Version;
            header[5] = frameType;
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(6, 4), examId);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(10, 4), studentId);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(14, 4), attemptId);
            BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(18, 8), unixMillis);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(26, 4), jpegLength);
            return header;
        }

        public static bool TryReadHeader(byte[] header, out byte frameType, out int examId, out int studentId, out int attemptId, out long unixMillis, out int jpegLength)
        {
            frameType = 0;
            examId = studentId = attemptId = jpegLength = 0;
            unixMillis = 0;
            if (header.Length != HeaderLength)
                return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (header[i] != Magic[i])
                    return false;
            }
            if (header[4] != Version)
                return false;
            
            frameType = header[5];
            examId = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(6, 4));
            studentId = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(10, 4));
            attemptId = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(14, 4));
            unixMillis = BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(18, 8));
            jpegLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(26, 4));
            return jpegLength > 0 && jpegLength <= MaxFrameBytes;
        }

        public static void ReadExactly(Stream stream, byte[] buffer, int length)
        {
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(buffer, offset, length - offset);
                if (read <= 0)
                    throw new EndOfStreamException();
                offset += read;
            }
        }
    }
}
