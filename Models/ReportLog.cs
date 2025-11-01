using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class ReportLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Format { get; set; } = "generated";
        public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }
}