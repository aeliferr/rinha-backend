
using System.Collections.Concurrent;
using Npgsql;

namespace rinha_backend;

public class InsertPerson : BackgroundService
{
  private readonly ConcurrentQueue<Person> _personQueue;
  private readonly NpgsqlDataSource _dataSource;
  private readonly ILogger<InsertPerson> _logger;
    public InsertPerson(ConcurrentQueue<Person> personQueue, NpgsqlDataSource dataSource, ILogger<InsertPerson> logger)
    {
        _personQueue = personQueue;
        _dataSource = dataSource;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(() => ExecuteAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await using var conn = await _dataSource.OpenConnectionAsync(stoppingToken);

    while(!stoppingToken.IsCancellationRequested) {
      var people = new List<Person>();
      while(_personQueue.TryDequeue(out var person) && people.Count < 100)
        people.Add(person);

      var batch = conn.CreateBatch();

      foreach (var person in people)
      {
        var batchCmd = new NpgsqlBatchCommand(@"INSERT INTO people (id, nickname, name, birth_date, stack) VALUES ($1, $2, $3, $4, $5)");
        
        batchCmd.Parameters.AddWithValue(person.Id);
        batchCmd.Parameters.AddWithValue(person.Apelido);
        batchCmd.Parameters.AddWithValue(person.Nome);
        batchCmd.Parameters.AddWithValue(person.Nascimento);
        batchCmd.Parameters.AddWithValue((object)person.Stack ?? DBNull.Value);
        batch.BatchCommands.Add(batchCmd);
      }

      await batch.ExecuteNonQueryAsync(stoppingToken);
    }
  }
}