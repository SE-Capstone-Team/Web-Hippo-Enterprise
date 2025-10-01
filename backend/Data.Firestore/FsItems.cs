using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
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
            PricePerDay = item.PricePerDay,
            Picture = item.Picture,
            Location = item.Location,
            Status = item.Status,
            Condition = item.Condition,
            OwnerId = item.OwnerId,
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

    public async Task<IReadOnlyList<InventoryItem>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Array.Empty<InventoryItem>();
        }

        var query = _collection.WhereEqualTo("ownerId", ownerId);
        var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return snapshot.Documents
            .Select(doc => doc.ConvertTo<InventoryItem>())
            .ToList();
    }

}

[FirestoreData]
public sealed class InventoryItem
{
    [FirestoreDocumentId]
    public string ItemId { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("pricePerDay")]
    public double PricePerDay { get; set; }

    [FirestoreProperty("picture")]
    public string Picture { get; set; } = string.Empty;

    [FirestoreProperty("location")]
    public string Location { get; set; } = string.Empty;

    [FirestoreProperty("status")]
    public string Status { get; set; } = string.Empty;

    [FirestoreProperty("condition")]
    public string Condition { get; set; } = string.Empty;

    [FirestoreProperty("ownerId")]
    public string OwnerId { get; set; } = string.Empty;
}
