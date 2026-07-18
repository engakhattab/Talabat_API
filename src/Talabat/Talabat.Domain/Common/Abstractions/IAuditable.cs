namespace Talabat.Domain.Common.Abstractions;

public interface IAuditable
{
    DateTime CreatedAt { get; }

    string? CreatedBy { get; }

    DateTime? ModifiedAt { get; }

    string? ModifiedBy { get; }

    void SetCreatedAudit(DateTime createdAt, string? createdBy);

    void SetModifiedAudit(DateTime modifiedAt, string? modifiedBy);
}
