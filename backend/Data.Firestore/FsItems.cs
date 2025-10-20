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

    public async Task<InventoryItem> CreateAsync(InventoryItemRequest item, CancellationToken cancellationToken = default)
    {
        // Persists a new inventory item. Validation is assumed to happen upstream
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var itemId = string.IsNullOrWhiteSpace(item.ItemId)
            ? Guid.NewGuid().ToString()
            : item.ItemId.Trim();

        var ownerRef = CreateProfileReference(item.OwnerId);
        if (ownerRef is null)
        {
            throw new InvalidOperationException("OwnerId is required for an inventory item.");
        }

        var borrowerRef = CreateProfileReference(item.BorrowerId);

        var payload = new InventoryItem
        {
            ItemId = itemId,
            Name = item.Name ?? string.Empty,
            PricePerDay = item.PricePerDay,
            Picture = item.Picture ?? string.Empty,
            Location = item.Location ?? string.Empty,
            IsLent = item.IsLent,
            Condition = item.Condition ?? string.Empty,
            OwnerRef = ownerRef,
            BorrowerRef = borrowerRef,
            BorrowedOn = item.BorrowedOn,
            DueAt = item.DueAt
        };

        payload.IsLent = payload.IsLent && payload.BorrowerRef is not null;

        if (!payload.IsLent)
        {
            payload.BorrowerRef = null;
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

        return await ConvertSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
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

    private DocumentReference? CreateProfileReference(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        return _db.Collection("profiles").Document(profileId.Trim());
    }

    public async Task<IReadOnlyList<InventoryItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Loads the entire collection
        var snapshot = await _collection.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<InventoryItem>(snapshot.Count);

        foreach (var doc in snapshot.Documents)
        {
            var item = await ConvertSnapshotAsync(doc, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<InventoryItem>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        // Filtered load by owner
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Array.Empty<InventoryItem>();
        }

        var ownerRef = CreateProfileReference(ownerId);
        var results = new Dictionary<string, InventoryItem>(StringComparer.Ordinal);

        if (ownerRef is not null)
        {
            var query = _collection.WhereEqualTo("ownerId", ownerRef);
            var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in snapshot.Documents)
            {
                var item = await ConvertSnapshotAsync(doc, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    results[item.ItemId] = item;
                }
            }
        }

        var legacyQuery = _collection.WhereEqualTo("ownerId", ownerId);
        var legacySnapshot = await legacyQuery.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        foreach (var doc in legacySnapshot.Documents)
        {
            var item = await ConvertSnapshotAsync(doc, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                results[item.ItemId] = item;
            }
        }

        return results.Values.ToList();
    }

    public async Task<IReadOnlyList<InventoryItem>> ListByBorrowerAsync(string borrowerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(borrowerId))
        {
            return Array.Empty<InventoryItem>();
        }

        var borrowerRef = CreateProfileReference(borrowerId);
        var results = new Dictionary<string, InventoryItem>(StringComparer.Ordinal);

        if (borrowerRef is not null)
        {
            var query = _collection.WhereEqualTo("borrowerId", borrowerRef);
            var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in snapshot.Documents)
            {
                var item = await ConvertSnapshotAsync(doc, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    results[item.ItemId] = item;
                }
            }
        }

        var legacyQuery = _collection.WhereEqualTo("borrowerId", borrowerId);
        var legacySnapshot = await legacyQuery.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        foreach (var doc in legacySnapshot.Documents)
        {
            var item = await ConvertSnapshotAsync(doc, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                results[item.ItemId] = item;
            }
        }

        return results.Values.ToList();
    }

        public async Task<bool> ReturnItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        // Marks an item as returned (available again)
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        var docRef = _collection.Document(itemId);
        var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!snapshot.Exists)
            return false;

        // Clear borrowing fields
        var updates = new Dictionary<string, object>
        {
            { "isLent", false },
            { "borrowerId", FieldValue.Delete },
            { "borrowedOn", FieldValue.Delete },
            { "dueAt", FieldValue.Delete }
        };

        await docRef.UpdateAsync(updates, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }


    private async Task<InventoryItem?> ConvertSnapshotAsync(DocumentSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot is null || !snapshot.Exists)
        {
            return null;
        }

        try
        {
            return snapshot.ConvertTo<InventoryItem>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return await UpgradeLegacyDocumentAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<InventoryItem?> UpgradeLegacyDocumentAsync(DocumentSnapshot snapshot, CancellationToken cancellationToken)
    {
        LegacyInventoryItem? legacy;
        try
        {
            legacy = snapshot.ConvertTo<LegacyInventoryItem>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (legacy is null)
        {
            return null;
        }

        var ownerRef = CreateProfileReference(legacy.OwnerId);
        if (ownerRef is null)
        {
            return null;
        }

        var borrowerRef = CreateProfileReference(legacy.BorrowerId);

        var item = new InventoryItem
        {
            ItemId = string.IsNullOrWhiteSpace(legacy.ItemId) ? snapshot.Id : legacy.ItemId,
            Name = legacy.Name ?? string.Empty,
            PricePerDay = legacy.PricePerDay,
            Picture = legacy.Picture ?? string.Empty,
            Location = legacy.Location ?? string.Empty,
            IsLent = legacy.IsLent,
            Condition = legacy.Condition ?? string.Empty,
            OwnerRef = ownerRef,
            BorrowerRef = borrowerRef,
            BorrowedOn = legacy.BorrowedOn,
            DueAt = legacy.DueAt
        };

        item.IsLent = item.IsLent && borrowerRef is not null;
        if (!item.IsLent)
        {
            item.BorrowerRef = null;
            item.BorrowedOn = null;
            item.DueAt = null;
        }

        var updates = new Dictionary<string, object>
        {
            ["ownerId"] = ownerRef
        };

        if (borrowerRef is null)
        {
            updates["borrowerId"] = FieldValue.Delete;
        }
        else
        {
            updates["borrowerId"] = borrowerRef;
        }

        await snapshot.Reference.UpdateAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);

        return item;
    }

    [FirestoreData]
    private sealed class LegacyInventoryItem
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

        [FirestoreProperty("ownerId")]
        public string OwnerId { get; set; } = string.Empty;

        [FirestoreProperty("borrowerId")]
        public string BorrowerId { get; set; } = string.Empty;

        [FirestoreProperty("borrowedOn")]
        public DateTime? BorrowedOn { get; set; }

        [FirestoreProperty("dueAt")]
        public DateTime? DueAt { get; set; }
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

    [FirestoreProperty("ownerId")]
    public DocumentReference? OwnerRef { get; set; }

    [FirestoreProperty("borrowerId")]
    public DocumentReference? BorrowerRef { get; set; }

    [FirestoreProperty("borrowedOn")]
    public DateTime? BorrowedOn { get; set; }

    [FirestoreProperty("dueAt")]
    public DateTime? DueAt { get; set; }
}

public sealed class InventoryItemRequest
{
    public string? ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double PricePerDay { get; set; }
    public string Picture { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsLent { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string BorrowerId { get; set; } = string.Empty;
    public DateTime? BorrowedOn { get; set; }
    public DateTime? DueAt { get; set; }
}

public sealed class InventoryItemView
{
    public string ItemId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double PricePerDay { get; init; }
    public string Picture { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public bool IsLent { get; init; }
    public string Condition { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public string BorrowerId { get; init; } = string.Empty;
    public string BorrowerName { get; init; } = string.Empty;
    public DateTime? BorrowedOn { get; init; }
    public DateTime? DueAt { get; init; }
}

public static class InventoryItemMapper
{
    public static async Task<IReadOnlyList<InventoryItemView>> ToViewListAsync(IEnumerable<InventoryItem> items, FsProfiles profiles, CancellationToken cancellationToken = default)
    {
        if (profiles is null)
        {
            throw new ArgumentNullException(nameof(profiles));
        }

        var materialized = (items ?? Array.Empty<InventoryItem>())
            .Where(item => item is not null)
            .ToList();

        if (materialized.Count == 0)
        {
            return Array.Empty<InventoryItemView>();
        }

        var profileIds = materialized
            .SelectMany(item => new[] { item.OwnerRef?.Id, item.BorrowerRef?.Id })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();

        var profileMap = await profiles.GetProfilesByIdsAsync(profileIds, cancellationToken)
            .ConfigureAwait(false);

        return materialized
            .Select(item => CreateView(item, profileMap))
            .ToList();
    }

    public static async Task<InventoryItemView> ToViewAsync(InventoryItem item, FsProfiles profiles, CancellationToken cancellationToken = default)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var profileIds = new[] { item.OwnerRef?.Id, item.BorrowerRef?.Id }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!);
        var profileMap = await profiles.GetProfilesByIdsAsync(profileIds, cancellationToken)
            .ConfigureAwait(false);

        return CreateView(item, profileMap);
    }

    private static InventoryItemView CreateView(InventoryItem item, IReadOnlyDictionary<string, UserProfile> profiles)
    {
        var ownerId = item.OwnerRef?.Id ?? string.Empty;
        var borrowerId = item.BorrowerRef?.Id ?? string.Empty;

        profiles.TryGetValue(ownerId, out var ownerProfile);
        profiles.TryGetValue(borrowerId, out var borrowerProfile);

        var ownerName = ResolveName(ownerProfile, ownerId);
        var borrowerName = string.IsNullOrWhiteSpace(borrowerId)
            ? string.Empty
            : ResolveName(borrowerProfile, borrowerId);

        return new InventoryItemView
        {
            ItemId = item.ItemId,
            Name = item.Name,
            PricePerDay = item.PricePerDay,
            Picture = item.Picture,
            Location = item.Location,
            IsLent = item.IsLent,
            Condition = item.Condition,
            OwnerId = ownerId,
            OwnerName = ownerName,
            BorrowerId = borrowerId,
            BorrowerName = borrowerName,
            BorrowedOn = item.BorrowedOn,
            DueAt = item.DueAt
        };
    }

    private static string ResolveName(UserProfile? profile, string fallback)
    {
        if (profile is null)
        {
            return fallback;
        }

        var first = profile.FirstName?.Trim() ?? string.Empty;
        var last = profile.LastName?.Trim() ?? string.Empty;

        if (first.Length == 0 && last.Length == 0)
        {
            return fallback;
        }

        if (first.Length == 0)
        {
            return last;
        }

        if (last.Length == 0)
        {
            return first;
        }

        return $"{first} {last}";
    }
}
