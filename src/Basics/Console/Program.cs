// See https://aka.ms/new-console-template for more information
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Options;
using ksqlDB.RestApi.Client.KSql.Linq;
using Basics.Models;

var ksqlDbUrl = @"http://localhost:8088";

var contextOptions = new KSqlDBContextOptions(ksqlDbUrl)
{
    ShouldPluralizeFromItemName = true
};

await using var context = new KSqlDBContext(contextOptions);

using var subscription = context.CreateQueryStream<Tweet>()
    .WithOffsetResetPolicy(AutoOffsetReset.Latest)
    .Where(p => p.Message == "Hi" )
      .Select(l => new { l.Message, l.Id })
      .Take(2)
      .Subscribe(
          tweetMessage =>
          {
              Console.WriteLine($"{nameof(Tweet)}: {tweetMessage.Id} - {tweetMessage.Message}");
          }, 
          error => 
          { 
              Console.WriteLine($"Exception: {error.Message}"); 
          }, 
          () =>
          {
              Console.WriteLine("Completed");
          }
      );

Console.WriteLine("Press any key to stop the subscription");

Console.ReadKey();