using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IettFaultManagement.Api.Models.Database;

[Table("ai_suggestions", Schema = "fault_management")]
public sealed class AiSuggestion
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("fault_id")] public long FaultId { get; set; }
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    [Column("suggestion_type")] public string SuggestionType { get; set; } = "FAULT_ANALYSIS";
    [Column("model_name")] public string ModelName { get; set; } = null!;
    [Column("prompt_version")] public string PromptVersion { get; set; } = "v1";
    [Column("status")] public string Status { get; set; } = "GENERATED";
    [Column("probable_cause")] public string? ProbableCause { get; set; }
    [Column("suggested_category_id")] public long? SuggestedCategoryId { get; set; }
    [Column("recommended_intervention")] public string? RecommendedIntervention { get; set; }
    [Column("estimated_repair_minutes")] public int? EstimatedRepairMinutes { get; set; }
    [Column("estimated_out_of_service_minutes")] public int? EstimatedOutOfServiceMinutes { get; set; }
    [Column("confidence_score")] public decimal? ConfidenceScore { get; set; }
    [Column("response_json", TypeName = "jsonb")] public string ResponseJson { get; set; } = "{}";
    [Column("similar_fault_count")] public int SimilarFaultCount { get; set; }
    [Column("ai_available")] public bool AiAvailable { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("reviewed_by_user_id")] public long? ReviewedByUserId { get; set; }
    [Column("reviewed_at")] public DateTime? ReviewedAt { get; set; }
}

[Table("ai_suggestion_sources", Schema = "fault_management")]
public sealed class AiSuggestionSource
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("ai_suggestion_id")] public long AiSuggestionId { get; set; }
    [Column("source_type")] public string SourceType { get; set; } = null!;
    [Column("source_id")] public long SourceId { get; set; }
    [Column("relevance_score")] public decimal? RelevanceScore { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ai_feedback", Schema = "fault_management")]
public sealed class AiFeedback
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("ai_suggestion_id")] public long AiSuggestionId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("feedback_type")] public string FeedbackType { get; set; } = null!;
    [Column("comment")] public string? Comment { get; set; }
    [Column("actual_repair_minutes")] public int? ActualRepairMinutes { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
