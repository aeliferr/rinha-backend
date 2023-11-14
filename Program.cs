using System.Collections.Concurrent;
using Npgsql;
using rinha_backend;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var db_host = builder.Configuration.GetValue<string>("DB_HOST");
var db_user = builder.Configuration.GetValue<string>("DB_USER");
var db_password = builder.Configuration.GetValue<string>("DB_PASSWORD");
var db_name = builder.Configuration.GetValue<string>("DB_NAME");

var connectionString = $"Host={db_host}:5432;Username={db_user};Password={db_password};Database={db_name};Pooling=true;Minimum Pool Size=20;Maximum Pool Size=200;Timeout=60";


builder.Services.AddSingleton(_ => new ConcurrentQueue<Person>());
builder.Services.AddSingleton(_ => new ConcurrentQueue<string>());
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

builder.Services.AddHostedService<InsertPerson>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

static string PersonValidator(Person person) {
    if (string.IsNullOrEmpty(person.Apelido))
        return "O apelido deve ser informado";

    if (person.Apelido.Length > 32)
        return "O apelido deve conter até 32 caracteres";

    if (string.IsNullOrEmpty(person.Nome))
        return "O nome deve ser informado";

    if (person.Nome.Length > 100)
        return "O nome deve no máximo 100 caracteres";

    if (string.IsNullOrEmpty(person.Nascimento))
        return "A data de nascimento deve ser informada";

    if (!DateOnly.TryParse(person.Nascimento, out var _))
        return "A data informada deve estar no format AAAA-MM-DD (ano, mes e dia)";

    if (person.Stack != null)
    {
        var item = person.Stack.FirstOrDefault(x => x.Length > 32);
        
        if (item != default)
            return "Os itens da stack devem ter no máximo 32 caracteres";
    }

    return string.Empty;
}

app.MapPost("/pessoas", async (ConcurrentQueue<Person> concurrentQueue, NpgsqlDataSource dataSource, Person person) => {
    var err = PersonValidator(person);

    if (!string.IsNullOrEmpty(err))
        return Results.UnprocessableEntity(err);

    await using (var connection = await dataSource.OpenConnectionAsync())
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM people where nickname = $1";
        cmd.Parameters.AddWithValue(person.Apelido);
        await using (var reader = await cmd.ExecuteReaderAsync()){
            if (reader.HasRows)
                return Results.UnprocessableEntity("Já existe uma pessoa cadastrada com este apelido");
        };
    
        person.Id = Guid.NewGuid();
    }       

    concurrentQueue.Enqueue(person); 

    return Results.Created($"/pessoas/{person.Id}", person);
});

app.MapGet("/pessoas/{id}", async (NpgsqlDataSource dataSource, Guid id) => {
    Person person = null;

    await using (var connection = await dataSource.OpenConnectionAsync())
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM people where id = $1";
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            person = new Person
            {
                Id = reader.GetGuid(0),
                Apelido = reader.GetString(1),
                Nome = reader.GetString(2),
                Nascimento = reader.GetString(3),
                Stack = await reader.IsDBNullAsync(4) ? null : await reader.GetFieldValueAsync<string[]>(4)
            };
        }
    }

    return Results.Ok(person);
});

app.MapGet("/pessoas", async (NpgsqlDataSource dataSource, string t) => {
    if (string.IsNullOrEmpty(t))
        return Results.BadRequest("Termo de consulta não foi informado");

    t = $"%{t}%";

    var people = new List<Person>();
    await using (var connection = await dataSource.OpenConnectionAsync())
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM people where nickname ilike $1 or name ilike $1 or array_to_string(stack, ' ') ilike $1";
        cmd.Parameters.AddWithValue(t);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var person = new Person
            {
                Id = reader.GetGuid(0),
                Apelido = reader.GetString(1),
                Nome = reader.GetString(2),
                Nascimento = reader.GetString(3),
                Stack = await reader.IsDBNullAsync(4) ? null : await reader.GetFieldValueAsync<string[]>(4)
            };

            people.Add(person);
        }
    }
    
    return Results.Ok(people);
});

app.MapGet("/contagem-pessoas", async (NpgsqlDataSource dataSource) => {
    
    int amount;
    await using (var cmd = dataSource.CreateCommand("SELECT COUNT(id) FROM people"))
    {
        var result = await cmd.ExecuteScalarAsync();
        amount = Convert.ToInt32(result);
    };

    return Results.Ok(amount);
});

app.Run();


