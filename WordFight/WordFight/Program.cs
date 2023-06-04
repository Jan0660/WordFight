using System.Text.Json;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.WebSockets;
using WordFight;
using Console = Log73.Console;
using LogLevel = Log73.LogLevel;

Console.Options.LogLevel = LogLevel.Debug;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors((options) =>
{
    options.AddPolicy(name: "cors",
        policy  =>
        {
            policy.AllowAnyOrigin();
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
        });
});

Globals.Data = JsonSerializer.Deserialize<DataFile>(File.ReadAllText("data.json"), Globals.JsonOptions)!;
// create invalid irregular variants
{
    var ls = new List<string>();
    for (var i = 0; i < Globals.Data.Words.Length; i++)
    {
        var word = Globals.Data.Words[i];
        if(word is not { Class: VerbClass.Irregular } or { Irregular: null }) continue;
        var split = word.Irregular.Split(' ');
        var opposite = split[1] == "hat" ? "ist" : "hat";
        ls.Add($"{split[0]} {opposite} {split[2]}");
        // if (split[1] == "hat")
        //     ls.Add($"{split[0]} ist {split[2]}");
        // if (split[1] == "ist")
        //     ls.Add($"{split[0]} hat {split[2]}");
        if (word.Irregular.EndsWith("en"))
        {
            ls.Add(word.Irregular[..^2] + "t");
            ls.Add($"{split[0]} {opposite} {split[2][..^2]}t");
        }
        else if (word.Irregular.EndsWith("et"))
        {
            ls.Add(word.Irregular[..^2] + "en");
            ls.Add($"{split[0]} {opposite} {split[2][..^2]}en");
        }
        else if (word.Irregular.EndsWith("t"))
        {
            ls.Add(word.Irregular[..^1] + "en");
            ls.Add($"{split[0]} {opposite} {split[2][..^1]}en");
        }
        
        Globals.Data.Words[i] = word with { IncorrectIrregular = ls.ToArray()};
        ls.Clear();
    }
}

builder.Services.AddWebSockets(_ => new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(1),
});

var app = builder.Build();

app.UseWebSockets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("cors");

app.UseAuthorization();

app.MapControllers();

app.Run();
