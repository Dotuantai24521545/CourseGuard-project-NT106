using System;
using System.Security.Cryptography;
using System.Text;

namespace CourseGuard.Backend.Security
{
    /// <summary>
    /// Simple Bloom filter implementation for fast probabilistic membership checks.
    /// </summary>
    public sealed class BloomFilter
    {
        private readonly byte[] _bits;
        private readonly int _bitCount;
        private readonly int _hashCount;

        public BloomFilter(int bitCount, int hashCount)
        {
            if (bitCount <= 0) throw new ArgumentOutOfRangeException(nameof(bitCount));
            if (hashCount <= 0) throw new ArgumentOutOfRangeException(nameof(hashCount));

            _bitCount = bitCount;
            _hashCount = hashCount;
            _bits = new byte[(bitCount + 7) / 8];
        }

        public void Add(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            foreach (int position in GetHashPositions(value))
            {
                SetBit(position);
            }
        }

        public bool MightContain(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (int position in GetHashPositions(value))
            {
                if (!GetBit(position))
                {
                    return false;
                }
            }
            return true;
        }

        private int[] GetHashPositions(string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant());
            byte[] hash = SHA256.HashData(data);

            int h1 = Math.Abs(BitConverter.ToInt32(hash, 0));
            int h2 = Math.Abs(BitConverter.ToInt32(hash, 4));
            if (h2 == 0) h2 = 1;

            int[] positions = new int[_hashCount];
            for (int i = 0; i < _hashCount; i++)
            {
                long combined = h1 + (long)i * h2;
                positions[i] = (int)(Math.Abs(combined) % _bitCount);
            }
            return positions;
        }

        private void SetBit(int bitIndex)
        {
            int byteIndex = bitIndex / 8;
            int offset = bitIndex % 8;
            _bits[byteIndex] |= (byte)(1 << offset);
        }

        private bool GetBit(int bitIndex)
        {
            int byteIndex = bitIndex / 8;
            int offset = bitIndex % 8;
            return (_bits[byteIndex] & (1 << offset)) != 0;
        }
    }
}
