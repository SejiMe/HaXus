using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Net.Http.Json;
using Npgsql;

if (args.Length < 2) throw new ArgumentException("Supply command and secret-directory path.");
var command = args[0];
var root = Path.GetFullPath(args[1]);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
string Read(string name) => File.ReadAllText(Path.Combine(root, name)).Trim();
void Write(string name, string value)
{
    var path = Path.Combine(root, name);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, value);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}
void Record(string dependency, string status, string? code = null) => Console.WriteLine(JsonSerializer.Serialize(new
{
    dependency, status, code, correlationId = Guid.NewGuid().ToString("N")
}));
void Check(bool result, string reason) { if (!result) throw new InvalidOperationException(reason); }

if (command == "prepare")
{
    Directory.CreateDirectory(root);
    if (OperatingSystem.IsWindows())
    {
        var account = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        var acl = new ProcessStartInfo("icacls") { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { root, "/inheritance:r", "/grant:r", account + ":(OI)(CI)F", "SYSTEM:(OI)(CI)F" }) acl.ArgumentList.Add(value);
        using var process = Process.Start(acl)!;
        process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit();
        Check(process.ExitCode == 0, "Could not restrict development material access.");
    }
    else File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    Dictionary<string, string> material;
    if (File.Exists(Path.Combine(root, "material.json"))) material = JsonSerializer.Deserialize<Dictionary<string, string>>(Read("material.json"))!;
    else
    {
        material = new[] { "app-admin", "support-admin", "app", "keycloak-db", "keycloak-admin", "kusa-db", "kusa-key", "dashboard-token", "otlp-key" }
            .ToDictionary(name => name, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant());
        Write("material.json", JsonSerializer.Serialize(material, jsonOptions));
    }
    foreach (var (name, value) in material) Check(value.Length == 64 && value.All(Uri.IsHexDigit), "Invalid local bootstrap material format.");
    Write("app-db/admin", material["app-admin"]);
    Write("support-db/admin", material["support-admin"]);
    Write("app-db/init/01-runtime.sql", $"CREATE ROLE haxus LOGIN PASSWORD '{material["app"]}';\nCREATE DATABASE haxus OWNER haxus;\n");
    Write("support-db/init/01-runtime.sql", $"CREATE ROLE keycloak LOGIN PASSWORD '{material["keycloak-db"]}';\nCREATE DATABASE keycloak OWNER keycloak;\nCREATE ROLE kusa LOGIN PASSWORD '{material["kusa-db"]}';\nCREATE DATABASE kusa OWNER kusa;\n");
    Write("keycloak/keycloak.conf", $"db=postgres\ndb-url=${{KC_DB_URL}}\ndb-username=keycloak\ndb-password={material["keycloak-db"]}\nbootstrap-admin-username=haxus-bootstrap\nbootstrap-admin-password={material["keycloak-admin"]}\nhttp-enabled=true\nhostname-strict=false\nhealth-enabled=true\n");
    Write("kusa/password", material["kusa-db"]);
    Write("kusa/encryption-key", material["kusa-key"]);
    Write("ugnay/ConnectionStrings__haxus", $"Host=application-db;Port=5432;Username=haxus;Password={material["app"]};Database=haxus;Timeout=3;Command Timeout=3");
    Write("dashboard/config.json", JsonSerializer.Serialize(new { Dashboard = new { Frontend = new { AuthMode = "BrowserToken", BrowserToken = material["dashboard-token"] }, Otlp = new { AuthMode = "ApiKey", PrimaryApiKey = material["otlp-key"] } } }, jsonOptions));
    if (!File.Exists(Path.Combine(root, "openbao/server.pem")))
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=haxus-local-openbao", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddDnsName("openbao"); san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        Write("openbao/server.pem", certificate.ExportCertificatePem());
        Write("openbao/server-key.pem", rsa.ExportPkcs8PrivateKeyPem());
    }
    Write("openbao/server.hcl", "ui = false\napi_addr = \"https://openbao:8200\"\ncluster_addr = \"https://openbao:8201\"\nstorage \"raft\" {\n path = \"/openbao/file\"\n node_id = \"haxus-local\"\n}\nlistener \"tcp\" {\n address = \"0.0.0.0:8200\"\n tls_cert_file = \"/run/secrets/server.pem\"\n tls_key_file = \"/run/secrets/server-key.pem\"\n}\n");
    Write("ugnay/Telemetry__OtlpHeaders", "x-otlp-api-key=" + material["otlp-key"]);
    foreach (var service in new[] { "ugnay", "application-db", "support-db", "keycloak", "openbao", "kusa", "compose-dashboard" })
        if (!File.Exists(Path.Combine(root, "env", service + ".env"))) Write("env/" + service + ".env", "# Non-secret service settings only.\n");
    Record("bootstrap", "prepared");
    return;
}

Check(args.Length >= 3, "Supply a local service URL.");
var endpoint = new Uri(args[2]);
Check(endpoint.IsLoopback || endpoint.Host == "openbao", "Probes require an explicitly local endpoint.");
using var handler = new HttpClientHandler();
if (endpoint.Scheme == "https")
{
    var expected = X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root, "openbao/server.pem"));
    handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
        certificate is not null && certificate.RawData.AsSpan().SequenceEqual(expected.RawData) &&
        (errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) == 0;
}
using var client = new HttpClient(handler) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(command is "bao-bootstrap" or "keycloak-bootstrap" ? 120 : 4) };
var materialValues = JsonSerializer.Deserialize<Dictionary<string, string>>(Read("material.json"))!;
async Task<JsonElement> Body(HttpResponseMessage response)
{
    Check(response.IsSuccessStatusCode, "Local bootstrap request failed (response omitted).");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return document.RootElement.Clone();
}
void RootToken() => client.DefaultRequestHeaders.Add("X-Vault-Token", JsonDocument.Parse(Read("openbao/bootstrap.json")).RootElement.GetProperty("root_token").GetString());

if (command == "bao-bootstrap")
{
    JsonElement state = default;
    for (var attempt = 0; attempt < 120; attempt++)
    {
        try { state = await Body(await client.GetAsync("/v1/sys/init")); break; }
        catch when (attempt < 119) { await Task.Delay(1000); }
    }
    if (!state.GetProperty("initialized").GetBoolean())
    {
        Check(!File.Exists(Path.Combine(root, "openbao/bootstrap.json")), "Existing bootstrap material with an uninitialized store: refusing automatic reinitialization.");
        var initialized = await Body(await client.PutAsJsonAsync("/v1/sys/init", new { secret_shares = 1, secret_threshold = 1 }));
        Write("openbao/bootstrap.json", initialized.GetRawText());
    }
    Check(File.Exists(Path.Combine(root, "openbao/bootstrap.json")), "Initialized OpenBao requires its original development bootstrap material; refusing reset.");
    var key = JsonDocument.Parse(Read("openbao/bootstrap.json")).RootElement.GetProperty("keys")[0].GetString();
    var unsealed = await Body(await client.PutAsJsonAsync("/v1/sys/unseal", new { key }));
    Check(!unsealed.GetProperty("sealed").GetBoolean(), "Development OpenBao remains sealed.");
    Record("openbao", "unsealed-development-instance");
    return;
}
if (command == "bao-fixture")
{
    RootToken();
    var mounts = await Body(await client.GetAsync("/v1/sys/mounts"));
    if (!mounts.GetProperty("data").TryGetProperty("transit/", out _))
        Check((await client.PostAsJsonAsync("/v1/sys/mounts/transit", new { type = "transit" })).IsSuccessStatusCode, "Cannot enable fixture Transit engine.");
    Check((await client.PostAsJsonAsync("/v1/transit/keys/haxus-probe", new { type = "aes256-gcm96" })).IsSuccessStatusCode, "Cannot create fixture key.");
    Check((await client.PutAsJsonAsync("/v1/sys/policies/acl/haxus-probe", new { policy = "path \"transit/keys/haxus-probe\" { capabilities = [\"read\"] }" })).IsSuccessStatusCode, "Cannot create fixture policy.");
    var token = await Body(await client.PostAsJsonAsync("/v1/auth/token/create", new { policies = new[] { "haxus-probe" }, no_default_policy = true, ttl = "30m", renewable = false }));
    Write("openbao/probe-token", token.GetProperty("auth").GetProperty("client_token").GetString()!);
    Record("openbao", "fixture-created"); return;
}
if (command is "bao-remove-key" or "bao-seal")
{
    RootToken();
    if (command == "bao-remove-key")
    {
        Check((await client.PostAsJsonAsync("/v1/transit/keys/haxus-probe/config", new { deletion_allowed = true })).IsSuccessStatusCode, "Cannot configure fixture key deletion.");
        Check((await client.DeleteAsync("/v1/transit/keys/haxus-probe")).IsSuccessStatusCode, "Cannot delete fixture key.");
    }
    else Check((await client.PutAsJsonAsync("/v1/sys/seal", new { })).IsSuccessStatusCode, "Cannot seal fixture instance.");
    Record("openbao", command); return;
}
if (command == "keycloak-bootstrap")
{
    var token = await Body(await client.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = "admin-cli", ["grant_type"] = "password", ["username"] = "haxus-bootstrap", ["password"] = materialValues["keycloak-admin"]
    })));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
    var response = await client.PostAsJsonAsync("/admin/realms", new { realm = "haxus-probe", enabled = true });
    Check(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict, "Cannot create isolated probe realm.");
    Record("keycloak", "fixture-created"); return;
}
if (command == "database-role")
{
    var user = args[3];
    var password = materialValues[user == "keycloak" ? "keycloak-db" : user == "kusa" ? "kusa-db" : "app"];
    var connectionString = new NpgsqlConnectionStringBuilder { Host = endpoint.Host, Port = endpoint.Port, Database = user, Username = user, Password = password, Timeout = 3 };
    await using var connection = new NpgsqlConnection(connectionString.ConnectionString); await connection.OpenAsync();
    await using var sql = new NpgsqlCommand("SELECT NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole FROM pg_roles WHERE rolname = current_user", connection);
    Check((bool)(await sql.ExecuteScalarAsync())!, "Runtime role has administrative privileges.");
    Record("postgres", "restricted-role-verified"); return;
}
var expectedStatus = args[3];
var actual = "unavailable";
string? code = null;
try
{
    if (command == "bao-check")
    {
        client.DefaultRequestHeaders.Add("X-Vault-Token", expectedStatus == "forbidden" ? "invalid-test-token" : Read("openbao/probe-token"));
        using var response = await client.GetAsync("/v1/transit/keys/haxus-probe");
        actual = (int)response.StatusCode switch { 200 => "present", 404 => "missing", 503 => "unavailable", 403 => "forbidden", _ => "unexpected" };
        code = actual == "missing" ? "HXI - MK01" : actual == "unavailable" ? "HXI - KU01" : null;
    }
    else if (command == "identity")
    {
        using var response = await client.GetAsync("/realms/haxus-probe/.well-known/openid-configuration");
        actual = response.IsSuccessStatusCode ? "available" : response.StatusCode == HttpStatusCode.ServiceUnavailable ? "unavailable" : "unexpected";
    }
    else throw new ArgumentException("Unknown probe command.");
}
catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
{
    actual = "unavailable"; code = command == "bao-check" ? "HXI - KU01" : null;
}
Record(command == "bao-check" ? "openbao" : "keycloak", actual, code);
Check(actual == expectedStatus, "Dependency probe did not match expected status.");
