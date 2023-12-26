// See https://aka.ms/new-console-template for more information
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Options;
using ksqlDB.RestApi.Client.KSql.Linq;
using Basics.Models;
using System.Runtime.CompilerServices;

var ksqlDbUrl = @"http://localhost:8088";

var contextOptions = new KSqlDBContextOptions(ksqlDbUrl)
{
    ShouldPluralizeFromItemName = false
};

await using var context = new KSqlDBContext(contextOptions);

using var subscription = context.CreateQueryStream<TweetStream>()
    .WithOffsetResetPolicy(AutoOffsetReset.Latest)
    .Where(p => p.User.Equals("Amen"))
      .Select(l => new { l.Id, l.User, l.Message })
      .Subscribe(
          tweetMessage =>
          {
              Console.WriteLine($"{nameof(TweetStream)}: {tweetMessage.Id} - {tweetMessage.User} - {tweetMessage.Message}");
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