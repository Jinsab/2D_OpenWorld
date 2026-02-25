#if !UNITY_5_3_OR_NEWER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System;
using MemoryPack;

// Minimal Crystal Save web API for ASP.NET Core/Blazor Server.
// Routes: /save, /load, /delete, /metadata, /list
// This sample lacks robust error handling and authentication; extend as needed.

var builder = WebApplication.CreateBuilder(args);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin() // For development; restrict in production
            .AllowAnyMethod()
            .WithHeaders("Content-Type", "X-API-KEY"));
});

var app = builder.Build();

app.UseCors();

string connString = builder.Configuration.GetConnectionString("MySql")
    ?? "Server=127.0.0.1;Database=game;Uid=user;Pwd=secret;";
string apiKey = builder.Configuration["ApiKey"] ?? "SUPER_SECRET_TOKEN";
string imgRoot = Path.Combine(AppContext.BaseDirectory, "img");

// simple gate
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Method == HttpMethods.Options)
    {
        await next.Invoke();
        return;
    }

    if (apiKey != string.Empty &&
        ctx.Request.Headers["X-API-KEY"] != apiKey)
    {   ctx.Response.StatusCode = 401; return; }

    await next.Invoke();
});

app.MapPost("/save", async (SavePayload payload) =>
{
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("INSERT INTO CrystalSaveData (UserID,Slot,Data) VALUES (@uid,@slot,@data) ON DUPLICATE KEY UPDATE Data=@data", conn);
    cmd.Parameters.AddWithValue("@uid", payload.uid);
    cmd.Parameters.AddWithValue("@slot", payload.slot);
    cmd.Parameters.AddWithValue("@data", Convert.FromBase64String(payload.data));
    await cmd.ExecuteNonQueryAsync();
});

app.MapPost("/metadata", async (MetadataPayload payload) =>
{
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("UPDATE CrystalSaveData SET SlotName=@name, LastSavedTicks=@ticks, LastActiveScene=@scene, ScreenshotFileName=@shot, CustomMetadata=@meta WHERE UserID=@uid AND Slot=@slot", conn);
    cmd.Parameters.AddWithValue("@name", payload.name);
    cmd.Parameters.AddWithValue("@ticks", payload.ticks);
    cmd.Parameters.AddWithValue("@scene", payload.scene);
    cmd.Parameters.AddWithValue("@shot", payload.shot);
    cmd.Parameters.AddWithValue("@meta", JsonSerializer.Serialize(payload.meta ?? new Dictionary<string,string>()));
    cmd.Parameters.AddWithValue("@uid", payload.uid);
    cmd.Parameters.AddWithValue("@slot", payload.slot);
    await cmd.ExecuteNonQueryAsync();
});

app.MapGet("/load", async (string uid, int slot) =>
{
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("SELECT Data FROM CrystalSaveData WHERE UserID=@uid AND Slot=@slot", conn);
    cmd.Parameters.AddWithValue("@uid", uid);
    cmd.Parameters.AddWithValue("@slot", slot);
    var result = await cmd.ExecuteScalarAsync();
    return result != null ? Results.Text(Convert.ToBase64String((byte[])result)) : Results.NotFound();
});

app.MapPost("/delete", async (SaveKey key) =>
{
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("DELETE FROM CrystalSaveData WHERE UserID=@uid AND Slot=@slot", conn);
    cmd.Parameters.AddWithValue("@uid", key.uid);
    cmd.Parameters.AddWithValue("@slot", key.slot);
    await cmd.ExecuteNonQueryAsync();
});

app.MapGet("/list", async (string uid) =>
{
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("SELECT Slot AS slot, SlotName AS name, LastSavedTicks AS ticks, LastActiveScene AS scene, ScreenshotFileName AS shot, CustomMetadata AS meta FROM CrystalSaveData WHERE UserID=@uid", conn);
    cmd.Parameters.AddWithValue("@uid", uid);
    using var reader = await cmd.ExecuteReaderAsync();
    var rows = new List<SlotMeta>();
    while (await reader.ReadAsync())
    {
        var metaJson = reader.GetString("meta");
        var meta = string.IsNullOrEmpty(metaJson) ? new Dictionary<string,string>() : JsonSerializer.Deserialize<Dictionary<string,string>>(metaJson);
        rows.Add(new SlotMeta(
            reader.GetInt32("slot"),
            reader.GetString("name"),
            reader.GetInt64("ticks"),
            reader.GetString("scene"),
            reader.GetString("shot"),
            meta
        ));
    }
    return Results.Json(rows);
});

app.MapPost("/uploadImage", async (HttpRequest req) =>
{
    if (!req.HasFormContentType) return Results.BadRequest();

    var form = await req.ReadFormAsync();
    IFormFile file = form.Files["shot"];
    string uid   = form["uid"];
    string slot  = form["slot"];
    string nameF = form["name"];

    if (file is null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(slot))
        return Results.BadRequest();

    string dir = Path.Combine(imgRoot, uid);
    Directory.CreateDirectory(dir);
    string ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
    string name     = !string.IsNullOrEmpty(nameF) ? Path.GetFileName(nameF) : $"{slot}{ext}";
    string path     = Path.Combine(dir, name);

    await using var stream = File.Create(path);
    await file.CopyToAsync(stream);

    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand(
        "UPDATE CrystalSaveData " +
        "SET ScreenshotFileName=@n, LastSavedTicks=UNIX_TIMESTAMP()*1000 " +
        "WHERE UserID=@u AND Slot=@s", conn);
    cmd.Parameters.AddWithValue("@n", name);
    cmd.Parameters.AddWithValue("@u", uid);
    cmd.Parameters.AddWithValue("@s", int.Parse(slot));
    await cmd.ExecuteNonQueryAsync();

    return Results.Ok();
});

app.Run();

[MemoryPackable]
public partial record SavePayload(string uid, int slot, string data);
[MemoryPackable]
public partial record MetadataPayload(string uid, int slot, string name, long ticks, string scene, string shot, Dictionary<string,string>? meta);
[MemoryPackable]
public partial record SaveKey(string uid, int slot);
[MemoryPackable]
public partial record SlotMeta(int slot, string name, long ticks, string scene, string shot, Dictionary<string,string> meta);
#endif
