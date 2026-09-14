using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher;

public static class DeterministicDummyZoneSelector
{
    public const string AlgorithmVersion = "v1";
    public const string Domain = "UAEZP-DUMMY-ASSIGNMENT";
    private const byte VersionByte = 1;

    public static ValidatedDummyZone Select(
        DummyZoneMode mode,
        int seed,
        FormKey target,
        IReadOnlyList<ValidatedDummyZone> orderedZones)
    {
        ArgumentNullException.ThrowIfNull(orderedZones);

        if (orderedZones.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot assign an encounter zone because no validated dummy zones are available.");
        }

        return mode switch
        {
            DummyZoneMode.First => orderedZones[0],
            DummyZoneMode.DeterministicRandom =>
                orderedZones[GetV1Index(seed, target, orderedZones.Count)],
            _ => throw new InvalidOperationException(
                $"Unsupported dummy-zone mode value: {(int)mode}."),
        };
    }

    public static int GetV1Index(int seed, FormKey target, int candidateCount)
    {
        if (candidateCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateCount),
                candidateCount,
                "Candidate count must be positive.");
        }

        byte[] digest = ComputeV1Digest(seed, target);
        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(digest);
        return (int)(value % (ulong)candidateCount);
    }

    public static byte[] ComputeV1Digest(int seed, FormKey target)
    {
        byte[] domainBytes = Encoding.ASCII.GetBytes(Domain);
        string normalizedFileName = target.ModKey.FileName.String.ToLowerInvariant();
        byte[] fileNameBytes = Encoding.UTF8.GetBytes(normalizedFileName);
        int inputLength = domainBytes.Length + sizeof(byte) + sizeof(int) +
                          sizeof(uint) + fileNameBytes.Length + sizeof(uint);
        byte[] input = new byte[inputLength];
        int offset = 0;

        domainBytes.CopyTo(input, offset);
        offset += domainBytes.Length;
        input[offset++] = VersionByte;

        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(offset, sizeof(int)), seed);
        offset += sizeof(int);
        BinaryPrimitives.WriteUInt32LittleEndian(
            input.AsSpan(offset, sizeof(uint)),
            checked((uint)fileNameBytes.Length));
        offset += sizeof(uint);
        fileNameBytes.CopyTo(input, offset);
        offset += fileNameBytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(
            input.AsSpan(offset, sizeof(uint)),
            target.ID);

        return SHA256.HashData(input);
    }
}
