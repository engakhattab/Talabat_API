namespace Talabat.Domain.Common.Abstractions;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; protected set; }

    public string? CreatedBy { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public void SetCreatedAudit(DateTime createdAt, string? createdBy)
    {
        createdAt = Guard.Utc(createdAt, nameof(createdAt));

        if (CreatedAt == default)
        {
            CreatedAt = createdAt;
        }

        CreatedBy = Guard.OptionalText(createdBy);
    }

    public void SetModifiedAudit(DateTime modifiedAt, string? modifiedBy)
    {
        ModifiedAt = Guard.Utc(modifiedAt, nameof(modifiedAt));
        ModifiedBy = Guard.OptionalText(modifiedBy);
    }

    public void SoftDelete(DateTime deletedAt, string? deletedBy)
    {
        if (IsDeleted)
        {
            return;
        }

        deletedAt = Guard.Utc(deletedAt, nameof(deletedAt));
        var normalizedDeletedBy = Guard.OptionalText(deletedBy);

        IsDeleted = true;
        DeletedAt = deletedAt;
        DeletedBy = normalizedDeletedBy;
        SetModifiedAudit(deletedAt, normalizedDeletedBy);
    }

    public void Restore(DateTime restoredAt, string? restoredBy)
    {
        if (!IsDeleted)
        {
            return;
        }

        restoredAt = Guard.Utc(restoredAt, nameof(restoredAt));
        var normalizedRestoredBy = Guard.OptionalText(restoredBy);

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        SetModifiedAudit(restoredAt, normalizedRestoredBy);
    }
}
