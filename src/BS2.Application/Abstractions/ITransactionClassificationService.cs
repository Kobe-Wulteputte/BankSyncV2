using BS2.Domain.Entities;
using BS2.Domain.Enums;

namespace BS2.Application.Abstractions;

/// <summary>
/// Classifies one transaction, persists the <see cref="ClassificationRun"/> and applies the
/// category when accepted. A manually classified transaction is never overwritten unless
/// <paramref name="force"/> is set. Does not call SaveChanges; the caller owns the unit of work.
/// </summary>
public interface ITransactionClassificationService
{
    Task<ClassificationRun> ClassifyAsync(Transaction transaction, ClassificationTrigger trigger, bool force, CancellationToken ct);
}
