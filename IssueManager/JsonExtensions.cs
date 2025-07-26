using System.Text.Json;

namespace IssueManager.Services
{
    public static class JsonExtensions
    {
        public static string TryGetString(this JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
                ? value.GetString()
                : null;
        }
    }
}
