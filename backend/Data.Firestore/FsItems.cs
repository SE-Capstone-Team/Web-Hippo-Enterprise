using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Google.Cloud.Firestore;

namespace Data.Firestore;

public sealed class FsItems
{
    // Firestore-backed repository for the `items` collection
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
        // Persists a new inventory item. Validation is assumed to happen upstream
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var itemId = string.IsNullOrWhiteSpace(item.ItemId)
            ? Guid.NewGuid().ToString()
            : item.ItemId;

        var borrowerId = string.IsNullOrWhiteSpace(item.BorrowerId)
            ? string.Empty
            : item.BorrowerId;

        var payload = new InventoryItem
        {
            ItemId = itemId,
            Name = item.Name,
            PricePerDay = item.PricePerDay,
            Picture = item.Picture,
            Location = item.Location,
            IsLent = item.IsLent,
            Condition = item.Condition,
            OwnerId = item.OwnerId,
            BorrowerId = borrowerId,
            BorrowedOn = item.BorrowedOn,
            DueAt = item.DueAt
        };

        payload.IsLent = payload.IsLent && !string.IsNullOrWhiteSpace(payload.BorrowerId);

        if (!payload.IsLent)
        {
            payload.BorrowerId = string.Empty;
            payload.BorrowedOn = null;
            payload.DueAt = null;
        }

        await _collection.Document(itemId)
            .SetAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return payload;
    }

    public async Task<InventoryItem?> ReadAsync(string itemId, CancellationToken cancellationToken = default)
    {
        // Fetches a single item document. Returns null for blank IDs to keep Firestore calls low.
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
        // Uses Firestore's MergeAll to patch fields
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
        // Deletes an item document by ID. Returns false for invalid IDs to avoid unnecessary Firestore calls
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
        // Factory helper mirrors FsProfiles
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project ID must be provided.", nameof(projectId));
        }

        return FirestoreDb.Create(projectId);
    }

    public async Task<IReadOnlyList<InventoryItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Loads the entire collection
        var snapshot = await _collection.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Documents
            .Select(doc => doc.ConvertTo<InventoryItem>())
            .ToList();
    }

    public async Task<IReadOnlyList<InventoryItem>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        // Filtered load by owner
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

    public async Task<IReadOnlyList<InventoryItem>> ListByBorrowerAsync(string borrowerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(borrowerId))
        {
            return Array.Empty<InventoryItem>();
        }

        var query = _collection.WhereEqualTo("borrowerId", borrowerId);
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

    [FirestoreProperty("isLent")]
    public bool IsLent { get; set; } = false;

    [FirestoreProperty("condition")]
    public string Condition { get; set; } = string.Empty;

    [FirestoreProperty("ownerId", ConverterType = typeof(FirestoreReferenceStringConverter))]
    public string OwnerId { get; set; } = string.Empty;

    [FirestoreProperty("borrowerId", ConverterType = typeof(FirestoreReferenceStringConverter))]
    public string BorrowerId { get; set; } = string.Empty;

    [FirestoreProperty("borrowedOn")]
    public DateTime? BorrowedOn { get; set; }

    [FirestoreProperty("dueAt")]
    public DateTime? DueAt { get; set; }
}

internal sealed class FirestoreReferenceStringConverter : IFirestoreConverter<string>
{
    public string FromFirestore(object value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            DocumentReference reference => reference.Id,
            _ => throw new InvalidOperationException($"Unable to convert Firestore value of type {value.GetType()} to string.")
        };
    }

    public object ToFirestore(string value)
    {
        return value ?? string.Empty;
    }
}
