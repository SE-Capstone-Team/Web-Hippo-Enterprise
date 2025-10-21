using Google.Cloud.Firestore;

namespace Data.Firestore;

public sealed class FsRequests
{
    private const string CollectionName = "requests";
    private readonly FirestoreDb _db;
    private readonly CollectionReference _collection;

    public FsRequests(FirestoreDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _collection = _db.Collection(CollectionName);
    }

    public async Task<BorrowRequestEntity> CreateAsync(BorrowRequestEntity entity, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entity.RequestId))
            entity.RequestId = Guid.NewGuid().ToString();

        await _collection.Document(entity.RequestId)
            .SetAsync(entity, cancellationToken: ct);

        return entity;
    }

    public async Task<BorrowRequestEntity?> ReadAsync(string requestId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return null;
        var snap = await _collection.Document(requestId).GetSnapshotAsync(ct);
        return snap.Exists ? snap.ConvertTo<BorrowRequestEntity>() : null;
    }

    public async Task<IReadOnlyList<BorrowRequestEntity>> ListForOwnerAsync(string ownerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return Array.Empty<BorrowRequestEntity>();
        var q = _collection.WhereEqualTo("ownerId", ownerId).WhereEqualTo("status", "pending");
        var snapshot = await q.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<BorrowRequestEntity>()).ToList();
    }

    public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken ct = default)
    {
        await _collection.Document(id).UpdateAsync(
            new Dictionary<string, object> { { "status", status } },
            cancellationToken: ct
        );
        return true;
    }

}

[FirestoreData]
public sealed class BorrowRequestEntity
{
    [FirestoreDocumentId] public string RequestId { get; set; } = string.Empty;

    // who owns the item
    [FirestoreProperty("ownerId")] public string OwnerId { get; set; } = string.Empty;

    // who is asking
    [FirestoreProperty("borrowerId")] public string BorrowerId { get; set; } = string.Empty;

    // what item
    [FirestoreProperty("itemId")] public string ItemId { get; set; } = string.Empty;
    [FirestoreProperty("itemName")] public string ItemName { get; set; } = string.Empty;

    // requested due date
    [FirestoreProperty("dueAt")] public DateTime? DueAt { get; set; }

    // pending | accepted | denied
    [FirestoreProperty("status")] public string Status { get; set; } = "pending";

    // created
    [FirestoreProperty("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
