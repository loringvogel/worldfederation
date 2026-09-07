using Microsoft.Data.Sqlite;

namespace Federation.Storage;

/// <summary>
/// Owns the SqliteConnection, runs schema migrations (CREATE TABLE IF NOT EXISTS),
/// and is registered as a singleton.
/// Production should use SQLCipher; for Phase 2, the application-layer encryption
/// in Federation.Cryptography provides the confidentiality layer.
/// </summary>
public sealed class FederationDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public FederationDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        RunMigrations();
    }

    public SqliteConnection Connection => _connection;

    private void RunMigrations()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS message_envelopes (
                message_id TEXT PRIMARY KEY,
                room_id TEXT NOT NULL,
                discussion_id TEXT NOT NULL,
                epoch INTEGER NOT NULL,
                sender_device_id TEXT NOT NULL,
                message_type TEXT NOT NULL,
                round INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                cipher_suite TEXT NOT NULL,
                ciphertext BLOB NOT NULL,
                signature BLOB NOT NULL,
                inserted_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS rooms (
                room_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                member_count INTEGER NOT NULL DEFAULT 0,
                active_discussion_count INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS discussions (
                discussion_id TEXT PRIMARY KEY,
                room_id TEXT NOT NULL,
                topic TEXT NOT NULL,
                phase TEXT NOT NULL,
                current_round INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                current_deadline TEXT,
                total_submissions INTEGER NOT NULL DEFAULT 0,
                expected_submissions INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS rounds (
                round_id TEXT PRIMARY KEY,
                discussion_id TEXT NOT NULL,
                round_number INTEGER NOT NULL,
                phase TEXT NOT NULL,
                deadline TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS memberships (
                membership_id TEXT PRIMARY KEY,
                room_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                role TEXT NOT NULL,
                joined_at TEXT NOT NULL,
                revoked_at TEXT
            );

            CREATE TABLE IF NOT EXISTS membership_devices (
                device_id TEXT PRIMARY KEY,
                membership_id TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS security_events (
                event_id TEXT PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                description TEXT NOT NULL,
                device_id TEXT,
                room_id TEXT
            );

            CREATE TABLE IF NOT EXISTS device_identities (
                device_id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                public_signing_key BLOB NOT NULL,
                public_key_agreement_key BLOB NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS peer_records (
                device_id TEXT PRIMARY KEY,
                human_id TEXT NOT NULL,
                addresses TEXT NOT NULL,
                supported_protocol_versions TEXT NOT NULL,
                issued_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                capabilities TEXT NOT NULL,
                signature BLOB NOT NULL
            );

            CREATE TABLE IF NOT EXISTS federation_events (
                event_id TEXT PRIMARY KEY,
                event_type TEXT NOT NULL,
                occurred_at TEXT NOT NULL,
                parent_event_id TEXT,
                payload TEXT,
                signature BLOB NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
