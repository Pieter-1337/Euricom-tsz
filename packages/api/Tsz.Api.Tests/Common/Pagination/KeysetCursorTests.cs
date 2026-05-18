using System.Text.Json;
using Shouldly;
using Tsz.Infrastructure.Common.Pagination;

namespace Tsz.Api.Tests.Common.Pagination;

public class KeysetCursorTests
{
    // ── Encode / decode round-trips ───────────────────────────────────────────

    [Fact]
    public void RoundTrip_StringSortValue()
    {
        var sortJson = JsonDocument.Parse("\"alice\"").RootElement.Clone();
        var id = Guid.NewGuid();
        var cursor = new KeysetCursor(1, sortJson, id);

        var encoded = cursor.Encode();

        KeysetCursor.TryDecode(encoded, out var decoded).ShouldBeTrue();
        decoded.ShouldNotBeNull();
        decoded!.Id.ShouldBe(id);
        decoded.V.ShouldBe(1);
        decoded.SortValue.GetString().ShouldBe("alice");
    }

    [Fact]
    public void RoundTrip_IntSortValue()
    {
        var sortJson = JsonDocument.Parse("42").RootElement.Clone();
        var id = Guid.NewGuid();
        var cursor = new KeysetCursor(1, sortJson, id);

        KeysetCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded!.SortValue.GetInt32().ShouldBe(42);
        decoded.Id.ShouldBe(id);
    }

    [Fact]
    public void RoundTrip_GuidSortValue()
    {
        var guidVal = Guid.NewGuid();
        var sortJson = JsonDocument.Parse($"\"{guidVal}\"").RootElement.Clone();
        var id = Guid.NewGuid();
        var cursor = new KeysetCursor(1, sortJson, id);

        KeysetCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded!.SortValue.GetGuid().ShouldBe(guidVal);
    }

    [Fact]
    public void RoundTrip_DateTimeSortValue()
    {
        var dt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var sortJson = JsonSerializer.SerializeToDocument(dt).RootElement.Clone();
        var id = Guid.NewGuid();
        var cursor = new KeysetCursor(1, sortJson, id);

        KeysetCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded!.SortValue.GetDateTime().ShouldBe(dt);
    }

    // ── TryDecode failure cases ───────────────────────────────────────────────

    [Fact]
    public void TryDecode_NullInput_ReturnsFalse()
    {
        KeysetCursor.TryDecode(null, out var cursor).ShouldBeFalse();
        cursor.ShouldBeNull();
    }

    [Fact]
    public void TryDecode_EmptyString_ReturnsFalse()
    {
        KeysetCursor.TryDecode(string.Empty, out var cursor).ShouldBeFalse();
        cursor.ShouldBeNull();
    }

    [Fact]
    public void TryDecode_GarbageInput_ReturnsFalse()
    {
        KeysetCursor.TryDecode("!!!not-base64!!!", out var cursor).ShouldBeFalse();
        cursor.ShouldBeNull();
    }

    [Fact]
    public void TryDecode_ValidBase64ButNotJson_ReturnsFalse()
    {
        var notJson = Convert.ToBase64String("hello world"u8.ToArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        KeysetCursor.TryDecode(notJson, out var cursor).ShouldBeFalse();
        cursor.ShouldBeNull();
    }

    [Fact]
    public void TryDecode_UnknownVersion_ReturnsFalse()
    {
        var sortJson = JsonDocument.Parse("\"x\"").RootElement.Clone();
        // Version 99 is unknown.
        var cursor = new KeysetCursor(99, sortJson, Guid.NewGuid());
        var encoded = cursor.Encode();

        KeysetCursor.TryDecode(encoded, out var decoded).ShouldBeFalse();
        decoded.ShouldBeNull();
    }

    [Fact]
    public void Encode_ProducesUrlSafeBase64()
    {
        var sortJson = JsonDocument.Parse("\"test\"").RootElement.Clone();
        var encoded = new KeysetCursor(1, sortJson, Guid.NewGuid()).Encode();

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
    }
}
