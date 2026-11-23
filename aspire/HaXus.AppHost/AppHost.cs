var builder = DistributedApplication.CreateBuilder(args);
builder.AddDotnetProject("ugnay", "../../src/HaXus.Ugnay/HaXus.Ugnay.csproj");
builder.Build().Run();
