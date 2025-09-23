using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Data.Firestore;

public sealed class FsItems
{
    private const string CollectionName = "items";
    private readonly FirestoreDb _db;
    private readonly CollectionReference _collection;

    public FsItems(string projectId)
        : this(CreateDb(projectId))
    {
    }

    public FsItems(FirestoreDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _collection = _db.Collection(CollectionName);
    }

    public async Task<InventoryItem> CreateAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var itemId = string.IsNullOrWhiteSpace(item.ItemId)
            ? Guid.NewGuid().ToString()
            : item.ItemId;

        var payload = new InventoryItem
        {
            ItemId = itemId,
            Name = item.Name,
            Description = item.Description,
            Quantity = item.Quantity,
            OwnerUserId = item.OwnerUserId,
        };

        await _collection.Document(itemId)
            .SetAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return payload;
    }

    public async Task<InventoryItem?> ReadAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        var snapshot = await _collection.Document(itemId)
            .GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);

        return snapshot.Exists ? snapshot.ConvertTo<InventoryItem>() : null;
    }

    public async Task<bool> UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.ItemId))
        {
            return false;
        }

        await _collection.Document(item.ItemId)
            .SetAsync(item, SetOptions.MergeAll, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeleteAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await _collection.Document(itemId)
            .DeleteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static FirestoreDb CreateDb(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project ID must be provided.", nameof(projectId));
        }

        return FirestoreDb.Create(projectId);
    }
}

[FirestoreData]
public sealed class InventoryItem
{
    [FirestoreDocumentId]
    public string ItemId { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("description")]
    public string Description { get; set; } = string.Empty;

    [FirestoreProperty("quantity")]
    public int Quantity { get; set; }

    [FirestoreProperty("ownerUserId")]
    public string OwnerUserId { get; set; } = string.Empty;
}
