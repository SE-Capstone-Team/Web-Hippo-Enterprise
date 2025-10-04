using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Data.Firestore;

public sealed class FsProfiles
{
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
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var userId = string.IsNullOrWhiteSpace(profile.UserId)
            ? Guid.NewGuid().ToString()
            : profile.UserId;

        var payload = new UserProfile
        {
            UserId = userId,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Email = profile.Email,
            Role = profile.Role,
        };

        await _collection.Document(userId)
            .SetAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return payload;
    }

    public async Task<UserProfile?> ReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var snapshot = await _collection.Document(userId)
            .GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);

        return snapshot.Exists ? snapshot.ConvertTo<UserProfile>() : null;
    }

    public async Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var query = _collection.WhereEqualTo("email", email);
        var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var document = snapshot.Documents.FirstOrDefault();

        return document?.ConvertTo<UserProfile>();
    }

    public async Task<bool> UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.UserId))
        {
            return false;
        }

        await _collection.Document(profile.UserId)
            .SetAsync(profile, SetOptions.MergeAll, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await _collection.Document(userId)
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
    [FirestoreDocumentId]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [FirestoreProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("role")]
    public string Role { get; set; } = string.Empty;
}
