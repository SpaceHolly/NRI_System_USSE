using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

static void Print(object value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
static string Canonical(BsonDocument document) => document.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson });

if (args.Length < 3) throw new ArgumentException("Usage: <mode> <connectionString> <databaseName> [arguments]");
var mode = args[0];
var connectionString = args[1];
var databaseName = args[2];
var mongoUrl = new MongoUrl(connectionString);
if (!string.Equals(databaseName, "nri_system", StringComparison.Ordinal)) throw new InvalidOperationException("REFUSE RESET: database name is not the known local development database.");
if (mongoUrl.Server == null || !(string.Equals(mongoUrl.Server.Host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(mongoUrl.Server.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("REFUSE RESET: MongoDB host is not local.");

var db = new MongoClient(connectionString).GetDatabase(databaseName);
var names = db.ListCollectionNames().ToList().OrderBy(x => x, StringComparer.Ordinal).ToArray();

if (mode == "inventory")
{
    var rows = names.Select(name => new
    {
        collectionName = name,
        documentCount = db.GetCollection<BsonDocument>(name).CountDocuments(FilterDefinition<BsonDocument>.Empty),
        indexes = db.GetCollection<BsonDocument>(name).Indexes.List().ToList().Select(Canonical).ToArray()
    }).ToArray();
    Print(new { databaseName, connectionString, collections = rows });
}
else if (mode == "account-snapshot")
{
    var accounts = db.GetCollection<BsonDocument>("accounts").Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToList();
    var profiles = db.GetCollection<BsonDocument>("profiles").Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToList();
    static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    var rows = accounts.Select(document => new
    {
        id = document.GetValue("_id", "").ToString(),
        login = document.GetValue("Login", document.GetValue("login", "")).ToString(),
        passwordHashDigest = Digest(document.GetValue("PasswordHash", "").ToString()),
        passwordSaltDigest = Digest(document.GetValue("PasswordSalt", "").ToString()),
        roles = document.GetValue("Roles", new BsonArray()).ToJson(),
        status = document.GetValue("Status", "").ToString(),
        profileId = document.GetValue("ProfileId", "").ToString()
    }).ToArray();
    var profileRows = profiles.Select(document => new
    {
        id = document.GetValue("_id", "").ToString(),
        userAccountId = document.GetValue("UserAccountId", "").ToString(),
        digest = Digest(Canonical(document))
    }).ToArray();
    Print(new { accountCount = rows.LongLength, profileCount = profileRows.LongLength, accounts = rows, profiles = profileRows });
}
else if (mode == "backup")
{
    if (args.Length < 4) throw new ArgumentException("Backup directory is required.");
    var root = Path.GetFullPath(args[3]);
    Directory.CreateDirectory(root);
    var manifestRows = new List<object>();
    foreach (var name in names)
    {
        var collection = db.GetCollection<BsonDocument>(name);
        var documents = collection.Find(FilterDefinition<BsonDocument>.Empty).ToList();
        var path = Path.Combine(root, name + ".jsonl");
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            foreach (var document in documents) writer.WriteLine(Canonical(document));
        var indexPath = Path.Combine(root, name + ".indexes.json");
        File.WriteAllText(indexPath, "[" + string.Join(",", collection.Indexes.List().ToList().Select(Canonical)) + "]", new UTF8Encoding(false));
        manifestRows.Add(new { collectionName = name, documentCount = documents.Count, dataFile = Path.GetFileName(path), indexesFile = Path.GetFileName(indexPath), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
    }
    var manifestPath = Path.Combine(root, "manifest.json");
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(new { databaseName, connectionString, createdAtLocal = DateTimeOffset.Now, collections = manifestRows }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    Print(new { status = "PASS", backupDirectory = root, manifestPath, collectionCount = names.Length });
}
else if (mode == "verify-backup")
{
    if (args.Length < 4) throw new ArgumentException("Backup directory is required.");
    var root = Path.GetFullPath(args[3]);
    var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
    var results = new List<object>();
    foreach (var item in manifest.RootElement.GetProperty("collections").EnumerateArray())
    {
        var name = item.GetProperty("collectionName").GetString()!;
        var expected = item.GetProperty("documentCount").GetInt64();
        var path = Path.Combine(root, item.GetProperty("dataFile").GetString()!);
        long parsed = 0;
        foreach (var line in File.ReadLines(path)) { if (line.Length > 0) { BsonDocument.Parse(line); parsed++; } }
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var pass = parsed == expected && string.Equals(hash, item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase);
        results.Add(new { collectionName = name, expected, parsed, pass });
        if (!pass) throw new InvalidDataException("Backup verification failed for " + name);
    }
    Print(new { status = "PASS", backupDirectory = root, verifiedCollections = results.Count, results });
}
else if (mode == "purge")
{
    if (args.Length < 4) throw new ArgumentException("Comma-separated purge allowlist is required.");
    var requested = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var unknownRequested = requested.Except(names, StringComparer.Ordinal).ToArray();
    var deleted = new List<object>();
    foreach (var name in requested.Intersect(names, StringComparer.Ordinal))
    {
        var result = db.GetCollection<BsonDocument>(name).DeleteMany(FilterDefinition<BsonDocument>.Empty);
        deleted.Add(new { collectionName = name, deletedCount = result.DeletedCount });
    }
    Print(new { status = "PASS", deleted, unknownRequested });
}
else if (mode == "index-audit")
{
    var rows = names.Select(name => new
    {
        collectionName = name,
        indexes = db.GetCollection<BsonDocument>(name).Indexes.List().ToList().Select(document => new
        {
            name = document.GetValue("name", "").ToString(),
            unique = document.GetValue("unique", false).ToBoolean(),
            key = Canonical(document.GetValue("key", new BsonDocument()).AsBsonDocument)
        }).ToArray()
    }).ToArray();
    Print(new { status = "PASS", collections = rows });
}
else throw new InvalidOperationException("Unknown mode: " + mode);
