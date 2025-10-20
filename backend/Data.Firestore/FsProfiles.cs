using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Data.Firestore;

public sealed class FsProfiles
{
    // Firestore repository for managing user profile documents
    private const string CollectionName = "profiles";
    private readonly FirestoreDb _db;
    private readonly CollectionReference _collection;

    public FsProfiles(string projectId)
        : this(CreateDb(projectId))
    {
    }

    public FsProfiles(FirestoreDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _collection = _db.Collection(CollectionName);
    }

    public async Task<UserProfile> CreateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        // Inserts a new profile document
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var ownerId = string.IsNullOrWhiteSpace(profile.OwnerId)
            ? Guid.NewGuid().ToString()
            : profile.OwnerId;

        var payload = new UserProfile
        {
            OwnerId = ownerId,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            Email = profile.Email ?? string.Empty,
            Address = profile.Address ?? string.Empty,
            Role = string.IsNullOrWhiteSpace(profile.Role) ? "owner" : profile.Role,
            Pfp = (profile.Pfp ?? string.Empty).Trim(),
            Password = profile.Password ?? string.Empty
        };


        await _collection.Document(ownerId)
            .SetAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return payload;
    }

    public async Task<UserProfile?> ReadAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        // Retrieves a profile by document ID. Returns null instead of throwing to simplify callers.
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return null;
        }

        var snapshot = await _collection.Document(ownerId)
            .GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);

        return snapshot.Exists ? snapshot.ConvertTo<UserProfile>() : null;
    }

    public async Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Firestore query that finds the first profile with a matching email. Emails are stored as-is
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var query = _collection.WhereEqualTo("email", email);
        var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var document = snapshot.Documents.FirstOrDefault();

        return document?.ConvertTo<UserProfile>();
    }

    public async Task<IReadOnlyDictionary<string, UserProfile>> GetProfilesByIdsAsync(IEnumerable<string> ownerIds, CancellationToken cancellationToken = default)
    {
        if (ownerIds is null)
        {
            return new Dictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);
        }

        var ids = ownerIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);
        }

        var snapshots = await Task.WhenAll(ids
                .Select(id => _collection.Document(id).GetSnapshotAsync(cancellationToken)))
            .ConfigureAwait(false);

        var profiles = new Dictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ids.Length; i++)
        {
            var snapshot = snapshots[i];
            if (!snapshot.Exists)
            {
                continue;
            }

            var profile = snapshot.ConvertTo<UserProfile>();
            profiles[ids[i]] = profile;
        }

        return profiles;
    }

    public async Task<bool> UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        // Full-document update
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.OwnerId))
        {
            return false;
        }

        await _collection.Document(profile.OwnerId)
            .SetAsync(profile, SetOptions.MergeAll, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeleteAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        // Thin wrapper around Firestore delete
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return false;
        }

        await _collection.Document(ownerId)
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
public sealed class UserProfile
{
    // Firestore DTO for a user's profile data.
    [FirestoreDocumentId]
    public string OwnerId { get; set; } = string.Empty;

    [FirestoreProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [FirestoreProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("address")]
    public string Address { get; set; } = string.Empty;

    [FirestoreProperty("role")]
    public string Role { get; set; } = "owner";

    [FirestoreProperty("pfp")]
    public string Pfp { get; set; } = string.Empty;

    [FirestoreProperty("password")]
    public string Password { get; set; } = string.Empty;
}
