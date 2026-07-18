namespace Talabat.Domain.Common.Abstractions;

public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTime? DeletedAt { get; }

    string? DeletedBy { get; }

    void SoftDelete(DateTime deletedAt, string? deletedBy);

    void Restore(DateTime restoredAt, string? restoredBy);
}
