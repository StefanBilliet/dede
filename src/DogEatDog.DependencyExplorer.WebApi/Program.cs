using DogEatDog.DependencyExplorer.WebApi;

var graphPath = args.SkipWhile(arg => arg != "--graph").Skip(1).FirstOrDefault();
var rootPath = args.SkipWhile(arg => arg != "--root").Skip(1).FirstOrDefault();
var url = args.SkipWhile(arg => arg != "--url").Skip(1).FirstOrDefault();

await DependencyExplorerComposition.RunAsync(args, graphPath, rootPath, url);
