using System.Text.Json;
using JobMatch.Api.Resumes;

namespace JobMatch.Api.Applications;

internal static class ResumeSnapshotJson
{
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static JsonDocument Serialize(ResumeResponse snapshot) =>
        JsonSerializer.SerializeToDocument(snapshot, SerializerOptions);

    public static ResumeResponse Deserialize(JsonDocument snapshot) =>
        snapshot.RootElement.Deserialize<ResumeResponse>(SerializerOptions)
        ?? throw new JsonException("Снимок резюме не содержит данных.");
}
