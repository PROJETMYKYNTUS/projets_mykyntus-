using System.Text.Json;
using Xunit;

namespace Kyntus.Characterization.Common;

/// <summary>
/// Assertions JSON pour figer les contrats de réponse (golden tests).
/// </summary>
public static class GoldenJson
{
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    public static void AssertPropertyExists(JsonElement root, string propertyName)
    {
        Assert.True(
            root.TryGetProperty(propertyName, out _),
            $"Propriété JSON attendue absente : '{propertyName}'. Corps : {root}");
    }

    public static void AssertStringProperty(JsonElement root, string propertyName, string expected)
    {
        AssertPropertyExists(root, propertyName);
        var value = root.GetProperty(propertyName).GetString();
        Assert.Equal(expected, value);
    }

    public static void AssertArrayEmpty(JsonElement root)
    {
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Empty(root.EnumerateArray());
    }
}
