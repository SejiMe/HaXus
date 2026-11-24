using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var repository = Path.GetFullPath(builder.Configuration["HAXUS_REPOSITORY_ROOT"] ?? Path.Combine(builder.AppHostDirectory, "../.."));
var publish = builder.ExecutionContext.IsPublishMode;
var environment = builder.Configuration["HAXUS_ENVIRONMENT"] ?? "dev";
if (!System.Text.RegularExpressions.Regex.IsMatch(environment, "^[a-z][a-z0-9-]{0,35}$"))
    throw new InvalidOperationException("Invalid HAXUS_ENVIRONMENT.");
var secretRoot = builder.Configuration["HAXUS_SECRET_ROOT"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HaXus", "secrets", environment);
var locks = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "deploy/images.lock.json"))).RootElement;
string Secret(string name) => publish ? "publish-placeholder" : File.ReadAllText(Path.Combine(secretRoot, name)).Trim();
string Material(string name) => publish ? "publish-placeholder" : JsonDocument.Parse(File.ReadAllText(Path.Combine(secretRoot, "material.json"))).RootElement.GetProperty(name).GetString()!;
IResourceBuilder<T> Pin<T>(IResourceBuilder<T> resource, string name) where T : ContainerResource
{
    var image = locks.GetProperty(name);
    var reference = image.GetProperty("image").GetString()!;
    var separator = reference.IndexOf('/');
    return resource.WithImageRegistry(reference[..separator]).WithImage(reference[(separator + 1)..])
        .WithImageTag(image.GetProperty("tag").GetString()!)
        .WithImageSHA256(image.GetProperty("digest").GetString()!.Replace("sha256:", ""))
        .WithContainerName($"haxus-{environment}-{resource.Resource.Name}");
}
var applicationPassword = builder.AddParameter("application-password", () => Material("app"), secret: true);
var appAdmin = builder.AddParameter("DatabasePassword", () => Secret("app-db/admin"), secret: true);
var supportAdmin = builder.AddParameter("support-password", () => Secret("support-db/admin"), secret: true);
var applicationDatabase = Pin(builder.AddPostgres("application-db", password: appAdmin), "postgres")
    .WithVolume($"haxus-{environment}-application-db", "/var/lib/postgresql")
    .WithEnvironment("PGDATA", "/var/lib/postgresql/18/docker")
    .WithBindMount(Path.Combine(secretRoot, "app-db/admin"), "/run/secrets/admin", isReadOnly: true)
    .WithBindMount(Path.Combine(secretRoot, "app-db/init"), "/docker-entrypoint-initdb.d", isReadOnly: true)
    .WithEnvironment(context =>
    {
        context.EnvironmentVariables.Remove("POSTGRES_PASSWORD");
        context.EnvironmentVariables["POSTGRES_PASSWORD_FILE"] = "/run/secrets/admin";
    });
var supportDatabase = Pin(builder.AddPostgres("support-db", password: supportAdmin), "postgres")
    .WithVolume($"haxus-{environment}-support-db", "/var/lib/postgresql")
    .WithEnvironment("PGDATA", "/var/lib/postgresql/18/docker")
    .WithBindMount(Path.Combine(secretRoot, "support-db/admin"), "/run/secrets/admin", isReadOnly: true)
    .WithBindMount(Path.Combine(secretRoot, "support-db/init"), "/docker-entrypoint-initdb.d", isReadOnly: true)
    .WithEnvironment(context =>
    {
        context.EnvironmentVariables.Remove("POSTGRES_PASSWORD");
        context.EnvironmentVariables["POSTGRES_PASSWORD_FILE"] = "/run/secrets/admin";
    });
var supportEndpoint = supportDatabase.GetEndpoint("tcp");
var keycloak = Pin(builder.AddContainer("keycloak", "quay.io/keycloak/keycloak"), "keycloak")
    .WithArgs("--config-file=/run/secrets/keycloak.conf", "start")
    .WithBindMount(Path.Combine(secretRoot, "keycloak/keycloak.conf"), "/run/secrets/keycloak.conf", isReadOnly: true)
    .WithEnvironment("KC_DB_URL", ReferenceExpression.Create($"jdbc:postgresql://{supportEndpoint.Property(EndpointProperty.Host)}:{supportEndpoint.Property(EndpointProperty.Port)}/keycloak"))
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpEndpoint(targetPort: 9000, name: "management")
    .WithHttpHealthCheck("/health/ready", endpointName: "management")
    .WaitFor(supportDatabase);
var openbao = Pin(builder.AddContainer("openbao", "ghcr.io/openbao/openbao"), "openbao")
    .WithArgs("server", "-config=/run/secrets/server.hcl")
    .WithBindMount(Path.Combine(secretRoot, "openbao/server.hcl"), "/run/secrets/server.hcl", isReadOnly: true)
    .WithBindMount(Path.Combine(secretRoot, "openbao/server.pem"), "/run/secrets/server.pem", isReadOnly: true)
    .WithBindMount(Path.Combine(secretRoot, "openbao/server-key.pem"), "/run/secrets/server-key.pem", isReadOnly: true)
    .WithVolume($"haxus-{environment}-openbao", "/openbao/file")
    .WithEndpoint(targetPort: 8200, scheme: "https", name: "https");
var kusa = Pin(builder.AddContainer("kusa", "docker.n8n.io/n8nio/n8n"), "kusa")
    .WithBindMount(Path.Combine(secretRoot, "kusa/password"), "/run/secrets/database-password", isReadOnly: true)
    .WithBindMount(Path.Combine(secretRoot, "kusa/encryption-key"), "/run/secrets/encryption-key", isReadOnly: true)
    .WithVolume($"haxus-{environment}-kusa", "/home/node/.n8n")
    .WithEnvironment("DB_TYPE", "postgresdb")
    .WithEnvironment("DB_POSTGRESDB_HOST", supportEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("DB_POSTGRESDB_PORT", supportEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("DB_POSTGRESDB_DATABASE", "kusa")
    .WithEnvironment("DB_POSTGRESDB_USER", "kusa")
    .WithEnvironment("DB_POSTGRESDB_PASSWORD_FILE", "/run/secrets/database-password")
    .WithEnvironment("N8N_ENCRYPTION_KEY_FILE", "/run/secrets/encryption-key")
    .WithEnvironment("N8N_DIAGNOSTICS_ENABLED", "false")
    .WithEnvironment("N8N_VERSION_NOTIFICATIONS_ENABLED", "false")
    .WithEnvironment("N8N_TEMPLATES_ENABLED", "false")
    .WithEnvironment("N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS", "true")
    .WithHttpEndpoint(targetPort: 5678, name: "http")
    .WithHttpHealthCheck("/healthz/readiness")
    .WaitFor(supportDatabase);
var applicationEndpoint = applicationDatabase.GetEndpoint("tcp");
var ugnay = builder.AddDotnetProject("ugnay", Path.Combine(repository, "src/HaXus.Ugnay/HaXus.Ugnay.csproj"))
    .WaitFor(applicationDatabase);
if (!publish)
{
    if (int.TryParse(builder.Configuration["HAXUS_APP_PORT"], out var isolatedPort))
        ugnay.WithEndpoint("http", endpoint => { endpoint.Port = isolatedPort; endpoint.TargetPort = null; });
    ugnay.WithEnvironment("ConnectionStrings__haxus", ReferenceExpression.Create($"Host={applicationEndpoint.Property(EndpointProperty.Host)};Port={applicationEndpoint.Property(EndpointProperty.Port)};Username=haxus;Password={applicationPassword};Database=haxus;Timeout=3"));
    builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (_, cancellationToken) =>
    {
        var url = await openbao.GetEndpoint("https").GetValueAsync(cancellationToken);
        var processInfo = new ProcessStartInfo("dotnet") { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { Path.Combine(repository, "tests/InfrastructureProbes/bin/Release/net10.0/InfrastructureProbes.dll"), "bao-bootstrap", secretRoot, url! })
            processInfo.ArgumentList.Add(value);
        using var process = Process.Start(processInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var outputText = await output; var errorText = await errors;
        File.WriteAllText(Path.Combine(secretRoot, "openbao/bootstrap.log"), outputText + errorText);
        if (process.ExitCode != 0) Console.Error.WriteLine("Development OpenBao bootstrap failed; application startup remains independent. Preserve the existing store and inspect local bootstrap material.");
    });
}
else
{
    ugnay.WithEnvironment("HealthChecks__Enabled", "true")
        .WithEnvironment("HAXUS_APP_SECRETS", "/run/secrets/ugnay")
        .PublishAsDockerFile(container => container.WithDockerfile(repository, "deploy/Ugnay.Dockerfile")
            .WithBindMount(Path.Combine(secretRoot, "ugnay"), "/run/secrets/ugnay", isReadOnly: true));
}
builder.AddDockerComposeEnvironment("compose");
builder.Build().Run();
