using System.ComponentModel.DataAnnotations;

namespace IettFaultManagement.Api.Dtos;

/// <summary>Adminin yeni üst veya alt arıza kategorisi oluştururken gönderdiği alanları taşır.</summary>
public sealed record CreateFaultCategoryRequest(
    [property: Required, StringLength(120, MinimumLength = 2)] string Name,
    long? ParentCategoryId);

/// <summary>Kategorinin tarihsel ilişkisini bozmadan değiştirilebilen ad ve durum alanlarını taşır.</summary>
public sealed record UpdateFaultCategoryRequest(
    [property: Required, StringLength(120, MinimumLength = 2)] string Name,
    bool IsActive);
