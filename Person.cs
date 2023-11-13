namespace rinha_backend;

public class Person
{
    public Guid Id { get; set; }

    public string Apelido { get; set; }

    public string Nome { get; set; }

    public string Nascimento { get; set; }

    public string[]? Stack { get; set; }
}
