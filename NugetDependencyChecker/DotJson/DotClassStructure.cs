namespace NugetDependencyChecker.ConsoleApp.DotJson;

public class RootObject
{
    public Nodes[] nodes { get; set; }
    public Edges[] edges { get; set; }
}

public class Nodes
{
    public string Id { get; set; }
    public string Label { get; set; }

    public Nodes(string name)
    {
        Id = name;
        Label = name;
    }
}

public class Edges
{
    public string Source { get; set; }
    public string Target { get; set; }
    public string Label { get; set; }

    public Edges(string sourceName, string targetName)
    {
        Source = sourceName;
        Target = targetName;
    }
}