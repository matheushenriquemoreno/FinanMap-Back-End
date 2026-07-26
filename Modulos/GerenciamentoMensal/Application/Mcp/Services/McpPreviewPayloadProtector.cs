using System.Security.Cryptography;

namespace Application.Mcp.Services;

public interface IMcpPreviewPayloadProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(ReadOnlySpan<byte> protectedPayload);
}

public sealed class McpPreviewPayloadProtector : IMcpPreviewPayloadProtector
{
    private const byte FormatVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public McpPreviewPayloadProtector(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("A chave AES-GCM deve ter 32 bytes.", nameof(key));
        _key = key.ToArray();
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        var output = new byte[1 + NonceSize + TagSize + plaintext.Length];
        output[0] = FormatVersion;
        var nonce = output.AsSpan(1, NonceSize);
        var tag = output.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = output.AsSpan(1 + NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return output;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload)
    {
        if (protectedPayload.Length < 1 + NonceSize + TagSize ||
            protectedPayload[0] != FormatVersion)
        {
            throw new CryptographicException("Payload MCP protegido inválido.");
        }

        var nonce = protectedPayload.Slice(1, NonceSize);
        var tag = protectedPayload.Slice(1 + NonceSize, TagSize);
        var ciphertext = protectedPayload.Slice(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
